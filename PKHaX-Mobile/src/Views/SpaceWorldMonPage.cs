using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>One Space World '97 party slot: species, level, DVs, stat experience, moves, names, disguise.</summary>
public sealed class SpaceWorldMonPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly int slot;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 8 };

	private Button speciesButton = null!;
	private Entry levelField = null!;
	private Entry dvField = null!;
	private Entry nicknameField = null!;
	private Entry otField = null!;
	private Switch disguiseToggle = null!;
	private Label statsLabel = null!;
	private readonly Button[] moveButtons = new Button[4];

	public SpaceWorldMonPage(SaveManager saves, int slot)
	{
		this.saves = saves;
		this.slot = slot;
		Title = $"Slot {slot + 1}";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
		Build();
	}

	private SW97Mon Mon => saves.SpaceWorld!.GetMon(slot);

	private void Build()
	{
		root.Clear();
		if (saves.SpaceWorld is null)
		{
			root.Add(Ui.Caption("No SpaceWorld state is open."));
			return;
		}

		var mon = Mon;

		var (speciesRow, speciesBtn) = Ui.PickerRow("Species", SW97Data.GetSpeciesName(mon.Species));
		speciesButton = speciesBtn;
		speciesButton.Clicked += OnSpeciesClicked;
		root.Add(speciesRow);

		var (disguiseRow, toggle) = Ui.SwitchRow("Disguise (keep stored stats)", true);
		disguiseToggle = toggle;
		root.Add(disguiseRow);
		root.Add(Ui.Caption("On: the slot shows the new species but keeps the stats it already had — the game " +
			"copies stored stats into battle and only derives types from the species. Off: stats are rebuilt."));

		var (levelRow, level) = Ui.NumberRow("Level", mon.Level, "1-255");
		levelField = level;
		root.Add(levelRow);

		var (dvRow, dv) = Ui.EntryRow("DVs (hex)", mon.DVs.ToString("X4"), maxLength: 4);
		dvField = dv;
		root.Add(dvRow);

		var (nickRow, nick) = Ui.EntryRow("Nickname", mon.Nickname, maxLength: 5);
		nicknameField = nick;
		root.Add(nickRow);

		var (otRow, ot) = Ui.EntryRow("OT", mon.OTName, maxLength: 5);
		otField = ot;
		root.Add(otRow);

		root.Add(Ui.SectionHeader("Moves"));
		for (int i = 0; i < moveButtons.Length; i++)
		{
			int index = i;
			var (row, button) = Ui.PickerRow($"Move {i + 1}", MoveText(mon, index));
			moveButtons[index] = button;
			button.Clicked += async (_, _) => await PickMove(index);
			root.Add(row);
		}

		statsLabel = new Label { FontSize = 12, TextColor = Ui.Muted };
		root.Add(Ui.Card(statsLabel));
		ShowStats();

		var maxButton = Ui.Action("Max DVs and stat experience");
		maxButton.Clicked += (_, _) =>
		{
			Commit();
			var m = Mon;
			m.DVs = 0xFFFF;
			for (int i = 0; i < 5; i++)
				m.SetStatExp(i, 0xFFFF);
			m.ApplyCalculatedStats();
			dvField.Text = "FFFF";
			ShowStats();
		};
		root.Add(maxButton);

		var recalcButton = Ui.Action("Recalculate stats");
		recalcButton.Clicked += (_, _) =>
		{
			Commit();
			Mon.ApplyCalculatedStats();
			ShowStats();
		};
		root.Add(recalcButton);

		var applyButton = Ui.Action("Apply", Ui.Positive);
		applyButton.Clicked += async (_, _) =>
		{
			Commit();
			saves.SpaceWorld!.SyncSpeciesList();
			await Shell.Current.Navigation.PopAsync();
		};
		root.Add(applyButton);
	}

	private static string MoveText(SW97Mon mon, int index)
	{
		int move = mon.GetMove(index);
		return move == 0 ? "(none)" : $"{SW97Data.GetMoveName(move)}  {mon.GetPP(index)}/{SW97Data.GetMaxPP(move, mon.GetPPUps(index))}";
	}

	private async void OnSpeciesClicked(object? sender, EventArgs e)
	{
		var picked = await PickerPage.ShowAsync("Species", SpaceWorldPage.SpeciesList(), Mon.Species);
		if (picked is null) return;

		Commit();
		var mon = Mon;
		if (disguiseToggle.IsToggled)
		{
			mon.ApplyDisguise(picked.Value.Value, true);
			nicknameField.Text = mon.Nickname;
		}
		else
		{
			mon.Species = picked.Value.Value;
			mon.ApplyCalculatedStats();
		}
		saves.SpaceWorld!.SyncSpeciesList();
		speciesButton.Text = SW97Data.GetSpeciesName(mon.Species);
		ShowStats();
	}

	private async Task PickMove(int index)
	{
		var picked = await PickerPage.ShowAsync($"Move {index + 1}", SpaceWorldPage.MoveList(), Mon.GetMove(index));
		if (picked is null) return;

		Commit();
		var mon = Mon;
		mon.SetMove(index, picked.Value.Value, mon.GetPPUps(index));
		moveButtons[index].Text = MoveText(mon, index);
	}

	private void ShowStats()
	{
		var mon = Mon;
		var types = mon.Type1 == mon.Type2
			? SW97Data.GetTypeName(mon.Type1)
			: $"{SW97Data.GetTypeName(mon.Type1)}/{SW97Data.GetTypeName(mon.Type2)}";
		statsLabel.Text = $"HP {mon.MaxHP}   Atk {mon.GetStat(0)}   Def {mon.GetStat(1)}   Spd {mon.GetStat(2)}   " +
			$"SpA {mon.GetStat(3)}   SpD {mon.GetStat(4)}\nTypes {types} (from the species, until a battle edit)";
	}

	private void Commit()
	{
		var mon = Mon;
		mon.Level = Ui.ParseInt(levelField.Text, mon.Level, 1, 255);
		if (int.TryParse(dvField.Text, System.Globalization.NumberStyles.HexNumber, null, out int dv))
			mon.DVs = dv;
		if (!string.IsNullOrEmpty(nicknameField.Text))
			SW97Data.TryEncodeName(nicknameField.Text, mon.NicknameRaw);
		if (!string.IsNullOrEmpty(otField.Text))
			SW97Data.TryEncodeName(otField.Text, mon.OTNameRaw);
	}
}
