using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// Battle team editing — the fork feature stock PKHeX does not have. Two storage models exist and the
/// editor reports which one is in play via <see cref="BattleTeamEditor.HasIndexedTeams"/>:
/// Gen 7 teams are index lists pointing INTO your boxes (editing a slot edits the box Pokemon it
/// references), while Gen 6's Battle Box is separate storage holding its own copies.
/// Writes to team-locked slots are what <c>SlotInfoBox.AllowBattleTeamWrites</c> unlocks.
/// </summary>
public sealed class BattleTeamPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly GameLists lists;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 2 };
	private BattleTeamEditor? editor;

	public BattleTeamPage(SaveManager saves, GameLists lists)
	{
		this.saves = saves;
		this.lists = lists;
		Title = "Battle teams";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		var sav = saves.Save;
		if (sav is null) { _ = Shell.Current.Navigation.PopAsync(); return; }
		editor = BattleTeamEditor.TryCreate(sav);
		Build();
	}

	private void Build()
	{
		root.Clear();
		if (editor is null)
		{
			root.Add(Ui.Caption("This save has no battle teams. Only Gen 6 (Battle Box) and Gen 7 (six registered teams) store them."));
			return;
		}

		// Team-locked slots are read-only in stock PKHeX; the fork gates that on this switch.
		var (lockRow, lockSw) = Ui.SwitchRow("Allow writes to team-locked slots", SlotInfoBox.AllowBattleTeamWrites);
		lockSw.Toggled += (_, e) => { SlotInfoBox.AllowBattleTeamWrites = e.Value; Build(); };
		root.Add(Ui.Card(lockRow));

		root.Add(Ui.Caption(editor.HasIndexedTeams
			? "Gen 7: each slot points at a box slot. Editing a team member edits that box Pokemon."
			: "Gen 6: the Battle Box is its own storage, separate from your PC boxes."));

		for (int t = 0; t < editor.TeamCount; t++)
			root.Add(TeamCard(t));
	}

	private View TeamCard(int team)
	{
		var ed = editor!;
		var v = new VerticalStackLayout { Spacing = 0 };

		var head = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
		head.Add(new Label
		{
			Text = ed.GetTeamName(team), FontSize = 16, FontAttributes = FontAttributes.Bold,
			TextColor = Ui.Text, VerticalOptions = LayoutOptions.Center,
		}, 0);
		var locked = ed.GetIsLocked(team);
		var lockBtn = new Button
		{
			Text = locked ? "🔒 Locked" : "Unlocked", FontSize = 12, HeightRequest = 36, CornerRadius = 9,
			BackgroundColor = locked ? Ui.Accent : Ui.SurfaceAlt, TextColor = locked ? Colors.White : Ui.Text,
			Padding = new Thickness(10, 0),
		};
		lockBtn.Clicked += (_, _) => { ed.SetIsLocked(team, !locked); Build(); };
		head.Add(lockBtn, 1);
		v.Add(head);

		for (int s = 0; s < BattleTeamEditor.SlotsPerTeam; s++)
		{
			var slot = s;
			PKM? mon = null;
			try { mon = ed.Read(team, slot); } catch { }
			var boxIndex = ed.GetTeamSlotIndex(team, slot);

			var name = mon is null || mon.Species == 0
				? "(empty)"
				: string.IsNullOrWhiteSpace(mon.Nickname) ? lists.SpeciesName(mon.Species) : mon.Nickname;
			var detail = mon is null || mon.Species == 0
				? (ed.HasIndexedTeams && boxIndex >= 0 ? $"→ slot {boxIndex}" : "unassigned")
				: $"Lv {mon.CurrentLevel}" + (ed.HasIndexedTeams && boxIndex >= 0 ? $"  → slot {boxIndex}" : "");

			var (row, btn) = Ui.PickerRow($"{slot + 1}.  {name}", detail);
			btn.Clicked += async (_, _) => await SlotMenu(team, slot, mon, boxIndex);
			v.Add(row);
		}

		return Ui.Card(v);
	}

	private async Task SlotMenu(int team, int slot, PKM? mon, int boxIndex)
	{
		var ed = editor!;
		var sav = saves.Save!;

		var options = new List<string>();
		if (mon is not null && mon.Species != 0) options.Add("Edit this Pokemon");
		if (ed.HasIndexedTeams) options.Add("Point at a box slot");
		options.Add("Clear slot");

		var choice = await DisplayActionSheetAsync($"Slot {slot + 1}", "Cancel", null, [.. options]);
		if (choice is null || choice == "Cancel") return;

		if (choice == "Edit this Pokemon")
		{
			// Gen 7 team slots reference a box slot; edit that box entry directly.
			if (ed.HasIndexedTeams && boxIndex >= 0)
			{
				sav.GetBoxSlotFromIndex(boxIndex, out var b, out var s);
				await Shell.Current.Navigation.PushAsync(new EntityEditorPage(saves, lists, b, s, isParty: false));
			}
			else
			{
				await DisplayAlertAsync("Battle Box", "Gen 6 Battle Box slots are separate storage; edit them from the box view after copying.", "OK");
			}
			return;
		}

		if (choice == "Point at a box slot")
		{
			var input = await DisplayPromptAsync("Box slot", $"Flat slot index (0 - {sav.SlotCount - 1})",
				initialValue: boxIndex >= 0 ? boxIndex.ToString() : "0", keyboard: Keyboard.Numeric);
			if (input is null) return;
			var idx = Ui.ParseInt(input, 0, 0, sav.SlotCount - 1);
			if (!ed.SetTeamSlotIndex(team, slot, idx))
				await DisplayAlertAsync("Rejected", "That slot is already used by this team, or is out of range.", "OK");
			Build();
			return;
		}

		if (choice == "Clear slot")
		{
			ed.ClearTeamSlot(team, slot);
			Build();
		}
	}
}
