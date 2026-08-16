using PKHaX.Mobile.Services;

namespace PKHaX.Mobile.Views;

public partial class MainPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly UpdateService updates;
	private readonly GameLists lists;
	private UpdateInfo? pendingUpdate;
	private bool checkedForUpdate;

	public MainPage(SaveManager saves, UpdateService updates, GameLists lists)
	{
		InitializeComponent();
		this.saves = saves;
		this.updates = updates;
		this.lists = lists;
	}

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
		if (!saves.IsLoaded) return;
		await Shell.Current.GoToAsync("box");
	}

	private async void OnPartyClicked(object? sender, EventArgs e)
	{
		if (!saves.IsLoaded) return;
		await Shell.Current.Navigation.PushAsync(new PartyPage(saves, lists));
	}

	private async void OnTeamsClicked(object? sender, EventArgs e)
	{
		if (!saves.IsLoaded) return;
		await Shell.Current.Navigation.PushAsync(new BattleTeamPage(saves, lists));
	}

	private async void OnBoxToolsClicked(object? sender, EventArgs e)
	{
		if (!saves.IsLoaded) return;
		await Shell.Current.Navigation.PushAsync(new BoxToolsPage(saves, 0));
	}

	private async void OnBagClicked(object? sender, EventArgs e)
	{
		if (!saves.IsLoaded) return;
		await Shell.Current.Navigation.PushAsync(new BagPage(saves));
	}

	private async void OnTrainerClicked(object? sender, EventArgs e)
	{
		if (!saves.IsLoaded) return;
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

	private void RefreshSummary()
	{
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
	}
}
