using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// Edits the battle-volatile type bytes of a Gen 3-5 battle inside an emulator save state.
/// Types outside battle are derived from the species, so this page only exists for states
/// taken mid-battle; edits are written back into the state container on save.
/// </summary>
public sealed class BattleTypingPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 8 };
	private readonly List<(int Battler, Picker Type1, Picker Type2)> rows = [];

	public BattleTypingPage(SaveManager saves)
	{
		this.saves = saves;
		Title = "Battle typing";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Build();
	}

	private void Build()
	{
		root.Clear();
		rows.Clear();
		var battle = saves.StateSession?.Analysis.Battle;
		if (battle is null)
		{
			root.Add(Ui.Caption("No battle was found in this state. Take the save state during a battle to edit typing."));
			return;
		}

		root.Add(Ui.SectionHeader($"Gen {battle.Generation} battle"));
		root.Add(Ui.Caption("Types here are the battle engine's live copies - the edit applies the moment the state is loaded and lasts until the Pokémon leaves the field."));

		var table = battle.TypeTable;
		var names = table.Select(t => t.Name).ToList();
		for (int battler = 0; battler < battle.Battlers; battler++)
		{
			if (!battle.IsPresent(battler))
				continue;
			string who = battler switch
			{
				0 => "You",
				1 => "Foe",
				2 => "Ally",
				_ => "Foe 2",
			};
			var (t1, t2) = battle.GetTypes(battler);
			var pick1 = MakePicker(table, names, t1);
			var pick2 = MakePicker(table, names, t2);
			var row = new VerticalStackLayout { Spacing = 4 };
			row.Add(Ui.Caption($"{who} — raw species #{battle.GetSpecies(battler)}"));
			var line = new HorizontalStackLayout { Spacing = 12 };
			line.Add(pick1);
			line.Add(pick2);
			row.Add(line);
			root.Add(Ui.Card(row));
			rows.Add((battler, pick1, pick2));
		}

		var save = new Button
		{
			Text = "Save changes into the state file",
			BackgroundColor = Ui.Positive,
			TextColor = Ui.Text,
		};
		save.Clicked += async (_, _) => await SaveChanges();
		root.Add(save);
	}

	private static Picker MakePicker((byte Value, string Name)[] table, List<string> names, byte value)
	{
		var picker = new Picker { ItemsSource = names, TextColor = Ui.Text, WidthRequest = 150 };
		int index = Array.FindIndex(table, t => t.Value == value);
		picker.SelectedIndex = index >= 0 ? index : 0;
		return picker;
	}

	private async Task SaveChanges()
	{
		var battle = saves.StateSession?.Analysis.Battle;
		if (battle is null)
			return;
		foreach (var (battler, pick1, pick2) in rows)
		{
			var table = battle.TypeTable;
			byte t1 = table[Math.Max(0, pick1.SelectedIndex)].Value;
			byte t2 = table[Math.Max(0, pick2.SelectedIndex)].Value;
			battle.SetTypes(battler, t1, t2);
		}
		var error = await saves.SaveBackAsync();
		await DisplayAlert("Battle typing", error ?? "Save state updated.", "OK");
	}
}
