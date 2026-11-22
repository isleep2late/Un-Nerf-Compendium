using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using PKHaX.Linux.Session;

namespace PKHaX.Linux.Views;

public sealed class SlotEditorView : UserControl
{
	private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

	private readonly LinuxSaveSession session;
	private readonly LinuxSlotRef where;
	private readonly Action applied;
	private readonly PKHeX.Core.PKM original;
	private readonly TextBlock status = new() { Foreground = DropPanel.Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap };
	private readonly TextBox species;
	private readonly TextBox nickname;
	private readonly TextBox level;
	private readonly CheckBox shiny;
	private readonly ComboBox? nature;
	private readonly ComboBox? ability;
	private readonly TextBox? heldItem;
	private readonly TextBox[] moves = new TextBox[4];
	private readonly TextBox[] ivs = new TextBox[6];
	private readonly TextBox[] evs = new TextBox[6];
	private readonly TextBox showdown;

	public SlotEditorView(LinuxSaveSession session, LinuxSlotRef where, PKHeX.Core.PKM pk, Action applied)
	{
		this.session = session;
		this.where = where;
		this.applied = applied;
		original = pk;
		var strings = PKHeX.Core.GameInfo.Strings;
		var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*"), RowSpacing = 4 };
		species = Box(LinuxSaveSession.NameOf(strings.Species, pk.Species));
		Row(grid, "Species", species);
		nickname = Box(pk.Nickname);
		Row(grid, "Nickname", nickname);
		level = Box(pk.CurrentLevel.ToString(Invariant));
		Row(grid, "Level", level);
		shiny = new CheckBox { IsChecked = pk.IsShiny, Foreground = DropPanel.Ink, Content = "Shiny" };
		Row(grid, "Shiny", shiny);
		if (pk.Format >= 3)
		{
			nature = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Foreground = DropPanel.Ink, Background = DropPanel.Well, ItemsSource = Enumerable.Range(0, 25).Select(n => LinuxSaveSession.NameOf(strings.Natures, n)).ToList(), SelectedIndex = (int)pk.Nature is var nat and >= 0 and < 25 ? nat : -1 };
			Row(grid, "Nature", nature);
			var count = pk.PersonalInfo.AbilityCount;
			if (count > 0)
			{
				var names = Enumerable.Range(0, count).Select(i => $"{i + 1}: {LinuxSaveSession.NameOf(strings.Ability, pk.PersonalInfo.GetAbilityAtIndex(i))}").ToList();
				var at = pk.PersonalInfo.GetIndexOfAbility(pk.Ability);
				ability = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Foreground = DropPanel.Ink, Background = DropPanel.Well, ItemsSource = names, SelectedIndex = at };
				Row(grid, "Ability", ability);
			}
		}
		if (pk.Format >= 2)
		{
			heldItem = Box(pk.HeldItem == 0 ? "none" : LinuxSaveSession.NameOf(strings.Item, pk.HeldItem));
			Row(grid, "Held item", heldItem);
		}
		ushort[] current = [pk.Move1, pk.Move2, pk.Move3, pk.Move4];
		for (var i = 0; i < 4; i++)
		{
			moves[i] = Box(current[i] == 0 ? "none" : LinuxSaveSession.NameOf(strings.Move, current[i]));
			Row(grid, $"Move {i + 1}", moves[i]);
		}
		int[] ivNow = [pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE];
		int[] evNow = [pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPA, pk.EV_SPD, pk.EV_SPE];
		for (var k = 0; k < 6; k++)
		{
			ivs[k] = Box(ivNow[k].ToString(Invariant));
			Row(grid, $"{LinuxSaveSession.StatLabels[k]} IV", ivs[k]);
		}
		for (var k = 0; k < 6; k++)
		{
			evs[k] = Box(evNow[k].ToString(Invariant));
			Row(grid, $"{LinuxSaveSession.StatLabels[k]} EVs", evs[k]);
		}
		var apply = DropPanel.MakeButton("Apply to this slot", DropPanel.Good);
		apply.Click += (_, _) => Apply();
		showdown = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, Foreground = DropPanel.Ink, Background = DropPanel.Well, PlaceholderText = "Showdown set text" };
		var copyOut = DropPanel.MakeButton("Showdown text of this slot", DropPanel.Blue);
		copyOut.Click += (_, _) => showdown.Text = session.ShowdownOf(where) ?? "";
		var copyIn = DropPanel.MakeButton("Apply the Showdown text", DropPanel.Blue);
		copyIn.Click += (_, _) => Done(session.ApplyShowdown(where, showdown.Text ?? ""));
		var panel = new StackPanel { Spacing = 8 };
		panel.Children.Add(DropPanel.Title($"Edit {where}"));
		panel.Children.Add(grid);
		panel.Children.Add(apply);
		panel.Children.Add(DropPanel.Title("Showdown"));
		panel.Children.Add(showdown);
		panel.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { copyOut, copyIn } });
		panel.Children.Add(status);
		Content = panel;
	}

	private static TextBox Box(string text) => new() { Text = text, Foreground = DropPanel.Ink, Background = DropPanel.Well };

	private static void Row(Grid grid, string label, Control view)
	{
		var row = grid.RowDefinitions.Count;
		grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
		var text = new TextBlock { Text = label, Foreground = DropPanel.Muted, VerticalAlignment = VerticalAlignment.Center };
		Grid.SetRow(text, row);
		Grid.SetRow(view, row);
		Grid.SetColumn(view, 1);
		grid.Children.Add(text);
		grid.Children.Add(view);
	}

	private void Done(string? refusal)
	{
		if (refusal is not null)
		{
			status.Text = refusal;
			return;
		}
		status.Text = "Applied. Use Save Changes Back to write the file.";
		applied();
	}

	private void Apply()
	{
		if (Read(out var why) is not { } edit)
		{
			status.Text = why;
			return;
		}
		if (edit == new LinuxSlotEdit())
		{
			status.Text = "Nothing changed.";
			return;
		}
		Done(session.Apply(where, edit));
	}

	private static int? Number(string label, string? text, out string? why)
	{
		why = null;
		if (int.TryParse((text ?? "").Trim(), System.Globalization.NumberStyles.Integer, Invariant, out var value))
			return value;
		why = $"{label}: \"{text}\" is not a number";
		return null;
	}

	private static int? Named(string label, IReadOnlyList<string> names, string? text, int max, bool noneAllowed, out string? why)
	{
		var t = (text ?? "").Trim();
		if (noneAllowed && (t.Length == 0 || t == "-" || string.Equals(t, "none", StringComparison.OrdinalIgnoreCase)))
		{
			why = null;
			return 0;
		}
		var id = LinuxSaveSession.IdOf(names, t, max, out var inner);
		why = inner is null ? null : $"{label}: {inner}";
		return id;
	}

	private LinuxSlotEdit? Read(out string why)
	{
		why = "";
		var strings = PKHeX.Core.GameInfo.Strings;
		var sav = session.Result.Stock;
		if (sav is null)
		{
			why = "This save is not open in the stock editor.";
			return null;
		}
		var edit = new LinuxSlotEdit();
		string? bad;
		if (Named("Species", strings.Species, species.Text, sav.MaxSpeciesID, false, out bad) is not { } speciesId)
			return Fail(bad, out why);
		if (speciesId != original.Species)
			edit = edit with { Species = (ushort)speciesId };
		if ((nickname.Text ?? "") != original.Nickname)
			edit = edit with { Nickname = nickname.Text ?? "" };
		if (Number("Level", level.Text, out bad) is not { } levelValue)
			return Fail(bad, out why);
		if (levelValue != original.CurrentLevel)
		{
			if (levelValue is < 1 or > 100)
				return Fail($"Level: {levelValue} is outside 1-100", out why);
			edit = edit with { Level = (byte)levelValue };
		}
		if (shiny.IsChecked is { } wantShiny && wantShiny != original.IsShiny)
			edit = edit with { Shiny = wantShiny };
		if (nature is { SelectedIndex: >= 0 } chosen && chosen.SelectedIndex != (int)original.Nature)
			edit = edit with { Nature = chosen.SelectedIndex };
		if (ability is { SelectedIndex: >= 0 } slot && slot.SelectedIndex != original.PersonalInfo.GetIndexOfAbility(original.Ability))
			edit = edit with { AbilityIndex = slot.SelectedIndex };
		if (heldItem is not null)
		{
			if (Named("Held item", strings.Item, heldItem.Text, sav.MaxItemID, true, out bad) is not { } item)
				return Fail(bad, out why);
			if (item != original.HeldItem)
				edit = edit with { HeldItem = item };
		}
		var chosenMoves = new ushort[4];
		for (var i = 0; i < 4; i++)
		{
			if (Named($"Move {i + 1}", strings.Move, moves[i].Text, sav.MaxMoveID, true, out bad) is not { } move)
				return Fail(bad, out why);
			chosenMoves[i] = (ushort)move;
		}
		if (!chosenMoves.SequenceEqual(new[] { original.Move1, original.Move2, original.Move3, original.Move4 }))
			edit = edit with { Moves = chosenMoves };
		var chosenIvs = new int[6];
		var chosenEvs = new int[6];
		for (var k = 0; k < 6; k++)
		{
			if (Number($"{LinuxSaveSession.StatLabels[k]} IV", ivs[k].Text, out bad) is not { } iv)
				return Fail(bad, out why);
			if (Number($"{LinuxSaveSession.StatLabels[k]} EVs", evs[k].Text, out bad) is not { } ev)
				return Fail(bad, out why);
			chosenIvs[k] = iv;
			chosenEvs[k] = ev;
		}
		if (!chosenIvs.SequenceEqual(new[] { original.IV_HP, original.IV_ATK, original.IV_DEF, original.IV_SPA, original.IV_SPD, original.IV_SPE }))
			edit = edit with { Ivs = chosenIvs };
		if (!chosenEvs.SequenceEqual(new[] { original.EV_HP, original.EV_ATK, original.EV_DEF, original.EV_SPA, original.EV_SPD, original.EV_SPE }))
			edit = edit with { Evs = chosenEvs };
		return edit;
	}

	private static LinuxSlotEdit? Fail(string? bad, out string why)
	{
		why = bad ?? "a field could not be read";
		return null;
	}
}
