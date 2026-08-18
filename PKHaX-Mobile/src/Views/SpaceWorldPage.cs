using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// The party of a Space World '97 save state. The 1997 prototype has no PKHeX SaveFile — it is edited
/// through SW97Save in PKHeX.Core, so this page stands in for the whole normal editor.
/// </summary>
public sealed class SpaceWorldPage : ContentPage
{
	private readonly SaveManager saves;
	private readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 8 };

	public SpaceWorldPage(SaveManager saves)
	{
		this.saves = saves;
		Title = "SpaceWorld party";
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
		var sav = saves.SpaceWorld;
		if (sav is null)
		{
			root.Add(Ui.Caption("No SpaceWorld state is open."));
			return;
		}

		root.Add(Ui.SectionHeader("Space World '97"));
		root.Add(Ui.Caption(sav.IsBattery
			? "Battery file. The prototype writes this but can never read it back, so edits here are for reference and for flashing."
			: $"Save state · player {sav.PlayerName} · party block 0x{sav.PartyOffset:X}"));

		for (int i = 0; i < sav.PartyCount; i++)
		{
			int slot = i;
			root.Add(SlotCard(sav, slot));
		}

		if (sav.PartyCount == 0)
			root.Add(Ui.Caption("The party is empty."));

		if (sav.PartyCount < SW97Save.PartyLength)
		{
			var add = Ui.Action("Add a party slot");
			add.Clicked += (_, _) =>
			{
				int slot = sav.PartyCount;
				sav.PartyCount = slot + 1;
				sav.InitializeSlot(slot);
				var mon = sav.GetMon(slot);
				mon.ApplyDisguise(mon.Species, true);
				mon.ApplyCalculatedStats();
				sav.SyncSpeciesList();
				Build();
			};
			root.Add(add);
		}

		root.Add(BattleTypesCard(sav));
		root.Add(Ui.Caption("Use \"Save changes to file\" on the main screen to write the state back."));
	}

	private View SlotCard(SW97Save sav, int slot)
	{
		var mon = sav.GetMon(slot);
		var info = new VerticalStackLayout { Spacing = 1 };
		var nick = mon.Nickname;
		var species = SW97Data.GetSpeciesName(mon.Species);
		info.Add(new Label
		{
			Text = $"{slot + 1}.  {(string.IsNullOrWhiteSpace(nick) ? species : nick)}",
			FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text,
		});
		var types = mon.Type1 == mon.Type2
			? SW97Data.GetTypeName(mon.Type1)
			: $"{SW97Data.GetTypeName(mon.Type1)}/{SW97Data.GetTypeName(mon.Type2)}";
		info.Add(new Label
		{
			Text = $"#{mon.Species} {species}  ·  Lv {mon.Level}  ·  {types}",
			FontSize = 11, TextColor = Ui.Muted,
		});
		info.Add(new Label
		{
			Text = $"HP {mon.CurrentHP}/{mon.MaxHP}   DVs {mon.DVs:X4}",
			FontSize = 10, TextColor = Ui.Muted,
		});

		var card = Ui.Card(info);
		var tap = new TapGestureRecognizer();
		tap.Tapped += async (_, _) =>
		{
			await Shell.Current.Navigation.PushAsync(new SpaceWorldMonPage(saves, slot));
		};
		card.GestureRecognizers.Add(tap);
		return card;
	}

	private static View BattleTypesCard(SW97Save sav)
	{
		var body = new VerticalStackLayout { Spacing = 6 };
		body.Add(new Label
		{
			Text = "Battle types", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text,
		});

		if (!sav.IsBattleActive)
		{
			body.Add(Ui.Caption("Typing lives only in the battle structure and is rebuilt from the species on every " +
				"send-out, so it can only be edited in a state captured during a battle."));
			return Ui.Card(body);
		}

		body.Add(Ui.Caption("Applies to the Pokemon currently out. It is rebuilt on the next send-out."));
		AddTypeRow(body, sav, enemy: false, label: "Yours");
		AddTypeRow(body, sav, enemy: true, label: "Opponent");
		return Ui.Card(body);
	}

	private static void AddTypeRow(Layout body, SW97Save sav, bool enemy, string label)
	{
		var (first, second) = ReadTypes(sav, enemy);
		var name = SW97Data.GetSpeciesName(ReadSpecies(sav, enemy));
		var (row, button) = Ui.PickerRow($"{label} ({name})", TypeText(first, second));
		button.Clicked += async (_, _) =>
		{
			var (t1, t2) = ReadTypes(sav, enemy);
			var pick1 = await PickerPage.ShowAsync($"{label}: first type", TypeList(), t1);
			if (pick1 is null) return;
			var pick2 = await PickerPage.ShowAsync($"{label}: second type", TypeList(), t2);
			int applied1 = pick1.Value.Value;
			int applied2 = pick2?.Value ?? applied1;
			WriteTypes(sav, enemy, applied1, applied2);
			button.Text = TypeText(applied1, applied2);
		};
		body.Add(row);
	}

	private static (int First, int Second) ReadTypes(SW97Save sav, bool enemy)
	{
		var battler = sav.GetBattleMon(enemy);
		return (battler[SW97Save.BattleType1], battler[SW97Save.BattleType2]);
	}

	private static void WriteTypes(SW97Save sav, bool enemy, int first, int second)
	{
		var battler = sav.GetBattleMon(enemy);
		battler[SW97Save.BattleType1] = (byte)first;
		battler[SW97Save.BattleType2] = (byte)second;
	}

	private static int ReadSpecies(SW97Save sav, bool enemy) => sav.GetBattleMon(enemy)[SW97Save.BattleSpecies];

	private static string TypeText(int t1, int t2) => t1 == t2
		? SW97Data.GetTypeName(t1)
		: $"{SW97Data.GetTypeName(t1)} / {SW97Data.GetTypeName(t2)}";

	internal static IReadOnlyList<NamedValue> TypeList()
	{
		var list = new List<NamedValue>();
		for (int i = 0; i < SW97Data.TypeNames.Length; i++)
		{
			var name = SW97Data.TypeNames[i];
			if (name.Length == 0 || char.IsDigit(name[0])) continue;
			list.Add(new NamedValue(i, name));
		}
		return list;
	}

	internal static IReadOnlyList<NamedValue> SpeciesList()
	{
		var list = new List<NamedValue>(SW97Data.MaxSpeciesID);
		for (int i = 1; i <= SW97Data.MaxSpeciesID; i++)
			list.Add(new NamedValue(i, $"{i:000} {SW97Data.GetSpeciesName(i)}"));
		return list;
	}

	internal static IReadOnlyList<NamedValue> MoveList()
	{
		var list = new List<NamedValue> { new(0, "(none)") };
		for (int i = 1; i <= SW97Data.MaxMoveID; i++)
			list.Add(new NamedValue(i, SW97Data.GetMoveName(i)));
		return list;
	}
}
