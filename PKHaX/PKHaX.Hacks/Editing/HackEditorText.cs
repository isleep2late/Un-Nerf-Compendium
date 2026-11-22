using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Records;

namespace PKHaX.Hacks.Editing;

public sealed record HackSlotRow(HackSlotRef Where, string Text)
{
	public override string ToString() => Text;
}

public static class HackEditorText
{
	internal static bool ProofDetailNumbers { get; set; }

	public static string Title(HackSaveModel hack) => $"{hack.Variant} (hack save)";

	public static string Summary(HackSaveModel hack) =>
		$"Trainer: {hack.TrainerLine}\nMoney: {hack.MoneyLine}\nParty: {hack.Party.Count(s => s.Mon is not null)} | Boxes: {hack.Boxes.Count} ({hack.Boxes.Sum(b => b.Count)} Pokemon)";

	public static IReadOnlyList<HackSlotRow> Rows(HackSaveModel hack)
	{
		var rows = new List<HackSlotRow>();
		foreach (var slot in hack.Party.Where(s => !s.IsEmpty))
			rows.Add(new HackSlotRow(slot.Where, $"Party {slot.Where.Slot + 1}: {slot.Title}"));
		foreach (var box in hack.Boxes)
		{
			foreach (var slot in box.Slots.Where(s => s.Mon is not null))
				rows.Add(new HackSlotRow(slot.Where, $"Box {box.Number}, slot {slot.Where.Slot + 1}: {slot.Title}"));
			var refused = box.Slots.Where(s => s.Mon is null && s.Refused is not null).ToList();
			if (refused.Count > 0)
				rows.Add(new HackSlotRow(refused[0].Where, $"Box {box.Number}: {refused.Count} unreadable record{(refused.Count == 1 ? "" : "s")} (slot{(refused.Count == 1 ? "" : "s")} {string.Join(", ", refused.Select(s => s.Where.Slot + 1))})"));
		}
		return rows;
	}

	public static string? EditNotes(HackMonEditModel edit)
	{
		var notes = new List<string>(edit.Notes);
		if (edit.Original.Party is null && edit.Changed.Contains("Species") && edit.LevelFromExp is { } level)
			notes.Add($"Level {level} by EXP.");
		return notes.Count == 0 ? null : string.Join(" ", notes);
	}

	public static string? AppliedNote(HackMonEditModel edit)
	{
		var notes = new[] { HackEditFields.EvNote(edit), EditNotes(edit) }.OfType<string>().ToList();
		return notes.Count == 0 ? null : string.Join(" ", notes);
	}

	public static string Detail(HackSaveModel hack, HackSlotRef where) => Detail(hack, where, null);

	public static string Detail(HackSaveModel hack, HackSlotRef where, string? appRefusal)
	{
		if (hack.Slot(where) is not { } slot)
			return $"{where}: no such slot in this save.";
		if (slot.Mon is not { } mon)
			return slot.Refused is { } why ? $"{where}: this record does not read ({why}), so it is shown and never written." : $"{where}: empty.";
		var names = hack.Names;
		bool Named(HackNameKind kind) => !ProofDetailNumbers && names.Has(kind);
		var national = mon.National is { } n && n != mon.Species ? $" (National #{n})" : "";
		var lines = new List<string>
		{
			$"{where}: {slot.Title}",
			$"Record: {mon.Codec}",
			Named(HackNameKind.Species) ? $"Species: {names.Shown(HackNameKind.Species, mon.Species)}{national}{(mon.IsEgg ? ", egg" : "")}" : $"Species #{mon.Species}{national}{(mon.IsEgg ? ", egg" : "")}",
			Named(HackNameKind.Item) ? $"Held item: {(mon.HeldItem == 0 ? HackEditFields.None : names.Shown(HackNameKind.Item, mon.HeldItem))}" : $"Held item #{mon.HeldItem}",
			$"EXP {mon.Exp}{((mon.Party?.Level ?? mon.Level) is { } l ? $", level {l}" : "")}",
			Named(HackNameKind.Move) ? $"Moves: {string.Join(", ", mon.Moves.Select(m => m == 0 ? "-" : names.Shown(HackNameKind.Move, m)))}" : $"Moves: {string.Join(", ", mon.Moves.Select(m => m == 0 ? "-" : $"#{m}"))}",
			$"EVs (HP/Atk/Def/SpA/SpD/Spe): {mon.Evs}",
			$"IVs: {(mon.Ivs is { } ivs ? ivs.ToString() : "not in this record")}",
		};
		if (mon.Nature is { } nature)
			lines.Add($"Nature: {HackEditFields.NatureName(nature)}");
		if (HackEditFields.AbilityShown(names, mon) is { } ability)
			lines.Add($"Ability: {ability}");
		if (mon.Ball is { } ball)
			lines.Add($"Ball #{ball}");
		if (mon.Friendship is { } friendship)
			lines.Add($"Friendship {friendship}");
		if (mon.HiddenAbility is { } hidden)
			lines.Add($"Hidden ability: {(hidden ? "yes" : "no")}");
		lines.Add(EditableFields.WhyNone(mon.Codec.Id) ?? $"Fields an editable save of this format can change: {string.Join(", ", EditableFields.Writable(mon.Codec.Id, mon.Shape))}");
		if (!hack.CanWrite)
			lines.Add("Read-only: " + (appRefusal is { } app && hack.Banner is { } codec && !app.Contains(codec) ? app : hack.Banner ?? "this save is not written."));
		else if (appRefusal is not null)
			lines.Add(appRefusal);
		return string.Join("\n", lines);
	}
}
