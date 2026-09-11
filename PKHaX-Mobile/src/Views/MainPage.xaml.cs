using PKHeX.Core;
using PKHaX.Mobile.Services;

namespace PKHaX.Mobile.Views;

public partial class MainPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly UpdateService updates;
	private readonly GameLists lists;
	private UpdateInfo? pendingUpdate;
	private bool checkedForUpdate;

	public MainPage(SaveManager saves, UpdateService updates, GameLists lists, ISaveFileGateway gateway)
	{
		InitializeComponent();
		this.saves = saves;
		this.updates = updates;
		this.lists = lists;
		this.gateway = gateway;
	}

	private readonly ISaveFileGateway gateway;

	private async void OnTransferClicked(object? sender, EventArgs e) =>
		await Shell.Current.Navigation.PushAsync(new TransferPage(saves, gateway));

	protected override void OnAppearing()
	{
		base.OnAppearing();
		HaxSwitch.IsToggled = saves.IllegalMode;
		VersionLabel.Text = $"v{updates.CurrentVersion} (build {updates.CurrentBuild})";
		RefreshSummary();

		if (!checkedForUpdate)
		{
			checkedForUpdate = true;
			_ = CheckForUpdateAsync();
		}
	}

	private async Task CheckForUpdateAsync()
	{
		var info = await updates.CheckAsync();
		if (info is null) return;

		pendingUpdate = info;
		UpdateTitle.Text = $"Update available: v{info.Version}";
		UpdateNotes.Text = string.IsNullOrWhiteSpace(info.Notes) ? "" : info.Notes;
		UpdateCard.IsVisible = true;
	}

	private async void OnUpdateClicked(object? sender, EventArgs e)
	{
		if (pendingUpdate is null) return;

		UpdateButton.IsEnabled = false;
		UpdateButton.Text = "Downloading…";
		UpdateProgress.IsVisible = true;
		var progress = new Progress<double>(p => UpdateProgress.Progress = p);

		try
		{
			await updates.InstallAsync(pendingUpdate, progress);
			UpdateButton.Text = "Confirm the install prompt";
		}
		catch (Exception ex)
		{
			UpdateButton.IsEnabled = true;
			UpdateButton.Text = "Update now";
			UpdateProgress.IsVisible = false;
			StatusLabel.Text = $"Update failed: {ex.Message}";
		}
	}

	private void OnHaxToggled(object? sender, ToggledEventArgs e) => saves.IllegalMode = e.Value;

	private async void OnOpenClicked(object? sender, EventArgs e)
	{
		OpenButton.IsEnabled = false;
		StatusLabel.Text = "Opening…";
		try
		{
			var error = await saves.OpenAsync();
			if (saves.Save is not null) lists.Build(saves.Save);
			StatusLabel.Text = error ?? (saves.IsLoaded ? "Loaded." : "Cancelled.");
			RefreshSummary();
		}
		finally
		{
			OpenButton.IsEnabled = true;
		}
	}

	private async void OnBoxesClicked(object? sender, EventArgs e)
	{
		if (saves.Save is null) return;
		await Shell.Current.GoToAsync("box");
	}

	private async void OnPartyClicked(object? sender, EventArgs e)
	{
		if (saves.Save is null) return;
		await Shell.Current.Navigation.PushAsync(new PartyPage(saves, lists));
	}

	private async void OnTeamsClicked(object? sender, EventArgs e)
	{
		if (saves.Save is null) return;
		await Shell.Current.Navigation.PushAsync(new BattleTeamPage(saves, lists));
	}

	private async void OnBoxToolsClicked(object? sender, EventArgs e)
	{
		if (saves.Save is null) return;
		await Shell.Current.Navigation.PushAsync(new BoxToolsPage(saves, 0));
	}

	private async void OnBagClicked(object? sender, EventArgs e)
	{
		if (saves.Save is null) return;
		await Shell.Current.Navigation.PushAsync(new BagPage(saves));
	}

	private async void OnTrainerClicked(object? sender, EventArgs e)
	{
		if (saves.Save is null) return;
		await Shell.Current.Navigation.PushAsync(new TrainerPage(saves, lists));
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		SaveButton.IsEnabled = false;
		StatusLabel.Text = "Writing…";
		var error = await saves.SaveBackAsync();
		StatusLabel.Text = error ?? "Saved to file.";
		SaveButton.IsEnabled = true;
	}

	private async void OnCorruptGen1Clicked(object? sender, EventArgs e) // PKHaX: Gen 1 save corrupter (Any% 2-swap)
	{
		if (!saves.CanCorruptGen1 || saves.Handle is not { } handle)
			return;

		// The file is corrupted exactly as it was opened (or last saved back): the in-memory save is never serialized,
		// so unsaved edits are not part of it. Say so in plain words. Gate on the editor's own edit flag (State.Edited,
		// set by every mobile edit path), never on a Write()-vs-disk byte compare: SAV1.Write() re-packs an unedited save
		// into different bytes and mutates the live buffer.
		var unsavedNote = saves.HasUnsavedEdits // PKHaX
			? Gen1SaveCorrupter.UnsavedEditsWarning + "\n(\"Save changes to file\" writes them; then tap this again.)\n\n"
			: "";
		bool go = await DisplayAlertAsync("Corrupt this Gen 1 save for the Any% 2-swap route?",
			$"File: {handle.DisplayName}\n\n" + unsavedNote +
			"- The file is corrupted EXACTLY AS IT WAS OPENED (or last saved back), byte for byte the same as the standalone corrupt-save-gen1.py: the party count byte becomes 255 (0xFF), the species-list terminator after it becomes 0xFF, and the checksum is recomputed so CONTINUE still loads the file.\n" +
			"- Every other byte, the Trainer ID included, is left untouched. Nothing edited in this app is written to the file.\n" +
			"- A backup of those same bytes is written first, always, into the app's backups folder and offered through the share sheet so you can keep it in Files / Drive. If the backup fails, nothing is corrupted.\n\n" +
			"PKHaX cannot reopen a party-count-255 file, so the app keeps the current (pre-corruption) save loaded; open the backup for further editing.",
			"Corrupt it", "Cancel");
		if (!go)
			return;

		CorruptGen1Button.IsEnabled = false;
		try
		{
			StatusLabel.Text = "Writing backup…";
			string backup;
			try
			{
				backup = await saves.WriteGen1BackupAsync();
			}
			catch (Exception ex)
			{
				StatusLabel.Text = $"Backup failed, so nothing was corrupted: {ex.Message}";
				return;
			}

			try
			{
				await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new Microsoft.Maui.ApplicationModel.DataTransfer.ShareFileRequest
				{
					Title = "Keep the PKHaX backup somewhere safe",
					File = new Microsoft.Maui.ApplicationModel.DataTransfer.ShareFile(backup),
				});
			}
			catch
			{
				// Share sheet unavailable or dismissed - the app-local backup still exists and is reported below.
			}

			StatusLabel.Text = "Writing corrupted save…";
			var error = await saves.CorruptGen1Async();
			StatusLabel.Text = error is not null
				? $"{error}\nBackup: {backup}"
				: $"Save corrupted for the Any% 2-swap route.\nBackup: {backup}\nThe app still shows the pre-corruption save (PKHaX cannot reload a party-count-255 file); open the backup for further edits.\n{Gen1SaveCorrupter.RouteReminder}";
			RefreshSummary();
		}
		finally
		{
			CorruptGen1Button.IsEnabled = true;
		}
	}

	private async void OnSpaceWorldClicked(object? sender, EventArgs e)
	{
		if (saves.SpaceWorld is null) return;
		await Shell.Current.Navigation.PushAsync(new SpaceWorldPage(saves));
	}

	private async void OnBattleTypingClicked(object? sender, EventArgs e)
	{
		if (saves.StateSession?.Analysis.Battle is null) return;
		await Shell.Current.Navigation.PushAsync(new BattleTypingPage(saves));
	}

	private void RefreshSummary()
	{
		var sw97 = saves.SpaceWorld;
		SpaceWorldButton.IsVisible = sw97 is not null;
		CorruptGen1Button.IsVisible = saves.CanCorruptGen1; // PKHaX: Gen 1 save file only (never a save state)
		BattleTypingButton.IsVisible = saves.StateSession?.Analysis.Battle is not null;

		if (sw97 is not null)
		{
			SummaryCard.IsVisible = true;
			GameLabel.Text = "Space World '97 · Gen 2 prototype";
			TrainerLabel.Text = sw97.IsBattery ? "Battery file" : $"Save state · player {sw97.PlayerName}";
			CountsLabel.Text = $"party {sw97.PartyCount} of 6" + (sw97.IsBattleActive ? " · battle in progress" : "");
			SetSaveFilePagesVisible(false);
			return;
		}

		SetSaveFilePagesVisible(true);
		var sav = saves.Save;
		if (sav is null)
		{
			SummaryCard.IsVisible = false;
			return;
		}

		SummaryCard.IsVisible = true;
		GameLabel.Text = $"{sav.Version} · Gen {sav.Generation}";
		TrainerLabel.Text = $"OT {sav.OT}   ID {sav.DisplayTID}/{sav.DisplaySID}";
		CountsLabel.Text = $"{sav.BoxCount} boxes · party {sav.PartyCount}";
		if (saves.StateSession is { } session)
		{
			GameLabel.Text += " · inside a save state";
			CountsLabel.Text = session.Facet switch
			{
				SaveStateFacet.EmbeddedSave => CountsLabel.Text + " · edits write back into the state",
				SaveStateFacet.GBParty => $"live RAM party of {sav.PartyCount} · party edits write back into the state",
				_ => $"live RAM party of {sav.PartyCount} · party edits write back into the state",
			};
			bool full = session.Facet == SaveStateFacet.EmbeddedSave;
			BoxesButton.IsVisible = full;
			TeamsButton.IsVisible = full;
			BoxToolsButton.IsVisible = full;
			BagButton.IsVisible = full;
			TrainerButton.IsVisible = full;
		}
	}

	private void SetSaveFilePagesVisible(bool visible)
	{
		PartyButton.IsVisible = visible;
		BoxesButton.IsVisible = visible;
		TeamsButton.IsVisible = visible;
		BoxToolsButton.IsVisible = visible;
		BagButton.IsVisible = visible;
		TransferButton.IsVisible = visible;
		TrainerButton.IsVisible = visible;
	}
}
