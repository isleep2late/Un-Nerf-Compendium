using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using PKHaX.Hacks.Records;

namespace PKHaX.Hacks.Editing;

public enum HackEditRowKind
{
	Number,
	Picker,
	Choice,
	Flag,
	Text,
}

public sealed record HackEditField(string Label, string Field, Func<HackMonEditModel, string> Read, Action<HackMonEditModel, int> Write)
{
	public HackEditRowKind Kind { get; init; } = HackEditRowKind.Number;

	public HackNameKind? NameKind { get; init; }

	public HackNames? Names { get; init; }

	public IReadOnlyList<string> Choices { get; init; } = [];

	public EditFieldSpec? Spec { get; init; }

	public string? Fixed { get; init; }

	public Func<HackMonEditModel, long?>? Current { get; init; }

	public Action<HackMonEditModel, string>? WriteText { get; init; }

	public bool Named => NameKind is { } kind && Names is { } names && names.Has(kind);

	public string Placeholder => Kind switch
	{
		HackEditRowKind.Picker when Named => HackEditFields.PickerPlaceholder,
		HackEditRowKind.Flag => HackEditFields.FlagYes + " or " + HackEditFields.FlagNo,
		_ => Spec?.Range ?? "a number of zero or more",
	};

	public bool CanEdit(HackMonEditModel edit) => Fixed is null && edit.CanEdit(Field);

	public string? WhyLocked(HackMonEditModel edit) => Fixed ?? edit.LockedReason(Field);
}

public static class HackEditFields
{
	public const string FlagYes = "yes";

	public const string FlagNo = "no";

	public const string None = "none";

	public const string PickerPlaceholder = "type a name, or #id";

	public const int EvTotalNote = 510;

	internal static bool ProofFixedRows { get; set; }

	internal static bool ProofNamesIgnored { get; set; }

	internal static bool ProofNoBanner { get; set; }

	internal static bool ProofNoEvNote { get; set; }

	internal static bool ProofFlagUnnamed { get; set; }

	internal static bool ProofNoRange { get; set; }

	private static readonly string[] StatLabels = ["HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed"];

	private static readonly Regex IdAtEnd = new(@"\(?#(\d+)\)?\s*$", RegexOptions.CultureInvariant);

	private static readonly Regex IdAtStart = new(@"^#(\d+)", RegexOptions.CultureInvariant);

	public static string NatureName(int nature) => nature is >= 0 and < 25 ? ((PKHeX.Core.Nature)nature).ToString() : $"nature {nature}";

	public static IReadOnlyList<string> Natures { get; } = [.. Enumerable.Range(0, 25).Select(NatureName)];

	public static IReadOnlyList<HackEditField> All { get; } =
	[
		new("Species", "Species", e => e.Species.ToString(CultureInfo.InvariantCulture), (e, v) => e.Species = checked((ushort)v)),
		new("Held item", "HeldItem", e => e.HeldItem.ToString(CultureInfo.InvariantCulture), (e, v) => e.HeldItem = checked((ushort)v)),
		new("Friendship", "Friendship", e => e.Friendship?.ToString(CultureInfo.InvariantCulture) ?? "", (e, v) => e.Friendship = checked((byte)v)),
		new("Ball", "Ball", e => e.Ball?.ToString(CultureInfo.InvariantCulture) ?? "", (e, v) => e.Ball = checked((byte)v)),
		new("Move 1", "Moves", e => e.Moves[0].ToString(CultureInfo.InvariantCulture), (e, v) => e.SetMove(0, checked((ushort)v))),
		new("Move 2", "Moves", e => e.Moves[1].ToString(CultureInfo.InvariantCulture), (e, v) => e.SetMove(1, checked((ushort)v))),
		new("Move 3", "Moves", e => e.Moves[2].ToString(CultureInfo.InvariantCulture), (e, v) => e.SetMove(2, checked((ushort)v))),
		new("Move 4", "Moves", e => e.Moves[3].ToString(CultureInfo.InvariantCulture), (e, v) => e.SetMove(3, checked((ushort)v))),
		new("HP EVs", "Evs", e => e.Evs.Hp.ToString(CultureInfo.InvariantCulture), (e, v) => e.Evs = e.Evs with { Hp = v }),
		new("Attack EVs", "Evs", e => e.Evs.Atk.ToString(CultureInfo.InvariantCulture), (e, v) => e.Evs = e.Evs with { Atk = v }),
		new("Defense EVs", "Evs", e => e.Evs.Def.ToString(CultureInfo.InvariantCulture), (e, v) => e.Evs = e.Evs with { Def = v }),
		new("Sp. Atk EVs", "Evs", e => e.Evs.SpA.ToString(CultureInfo.InvariantCulture), (e, v) => e.Evs = e.Evs with { SpA = v }),
		new("Sp. Def EVs", "Evs", e => e.Evs.SpD.ToString(CultureInfo.InvariantCulture), (e, v) => e.Evs = e.Evs with { SpD = v }),
		new("Speed EVs", "Evs", e => e.Evs.Spe.ToString(CultureInfo.InvariantCulture), (e, v) => e.Evs = e.Evs with { Spe = v }),
	];

	public static IReadOnlyList<HackEditField> For(HackMonEditModel edit, HackNames? names)
	{
		if (ProofFixedRows)
			return All;
		var catalog = names ?? HackNames.Empty;
		var specs = new Dictionary<string, EditFieldSpec>(StringComparer.Ordinal);
		foreach (var spec in edit.Specs)
			specs[spec.Field] = spec;
		var rows = new List<HackEditField>();
		if (specs.TryGetValue("Species", out var species))
			rows.Add(Picker("Species", "Species", HackNameKind.Species, catalog, species, e => e.Species, (e, v) => e.Species = checked((ushort)v)));
		if (specs.TryGetValue(EditFieldSpecs.Nickname, out var nick))
		{
			rows.Add(new HackEditField("Nickname", EditFieldSpecs.Nickname, e => e.Nickname ?? "", (_, _) => { })
			{
				Kind = HackEditRowKind.Text,
				Spec = nick,
				WriteText = (e, t) => e.Nickname = t,
			});
		}
		else if (EditFieldSpecs.Find(edit.Original.Codec.Id, edit.Original.Shape, EditFieldSpecs.Nickname) is { } nickname)
		{
			var why = nickname.Why ?? EditFieldSpecs.NicknameNotWritten;
			rows.Add(new HackEditField("Nickname", EditFieldSpecs.Nickname, e => e.Original.Nickname ?? "", (_, _) => throw new InvalidOperationException(why))
			{
				Kind = HackEditRowKind.Text,
				Spec = nickname,
				Fixed = why,
			});
		}
		if (specs.TryGetValue("HeldItem", out var item))
			rows.Add(Picker("Held item", "HeldItem", HackNameKind.Item, catalog, item, e => e.HeldItem, (e, v) => e.HeldItem = checked((ushort)v)));
		if (specs.TryGetValue("Moves", out var moves))
		{
			for (var i = 0; i < 4; i++)
			{
				var slot = i;
				rows.Add(Picker($"Move {slot + 1}", "Moves", HackNameKind.Move, catalog, moves, e => slot < e.Moves.Count ? e.Moves[slot] : 0, (e, v) => e.SetMove(slot, checked((ushort)v))));
			}
		}
		if (specs.TryGetValue(EditableFields.Level, out var level))
			rows.Add(Number("Level", EditableFields.Level, level, e => e.Level, (e, v) => e.Level = checked((byte)v)));
		if (specs.TryGetValue("Exp", out var exp))
			rows.Add(Number("EXP", "Exp", exp, e => e.Exp, (e, v) => e.Exp = checked((uint)v)));
		if (specs.TryGetValue(EditFieldSpecs.Nature, out var nature))
			rows.Add(Choice("Nature", EditFieldSpecs.Nature, nature, edit, _ => Natures, e => e.Nature, (e, v) => e.Nature = checked((byte)v)));
		if (specs.TryGetValue(EditFieldSpecs.AbilitySlot, out var ability))
			rows.Add(Choice("Ability", EditFieldSpecs.AbilitySlot, ability, edit, e => Slots(catalog, e.Species, ability.Max), e => e.AbilitySlot, (e, v) => e.AbilitySlot = checked((byte)v)));
		if (specs.TryGetValue("HiddenAbility", out var hidden))
		{
			var named = !ProofFlagUnnamed && !specs.ContainsKey(EditFieldSpecs.AbilitySlot) && edit.Original.AbilitySlot is null;
			rows.Add(Flag("Hidden ability", "HiddenAbility", hidden, named ? HiddenChoices(catalog, edit.Original) : [FlagNo, FlagYes], e => e.HiddenAbility, (e, v) => e.HiddenAbility = v != 0));
		}
		if (specs.TryGetValue("Ivs", out var ivs))
		{
			for (var k = 0; k < StatLabels.Length; k++)
			{
				var stat = k;
				rows.Add(Number($"{StatLabels[stat]} IV", "Ivs", ivs, e => e.Ivs is { } set ? set[stat] : null,
					(e, v) => e.Ivs = With(e.Ivs ?? throw new InvalidOperationException("this record holds no IVs"), stat, v)));
			}
		}
		if (specs.TryGetValue("Evs", out var evs))
		{
			for (var k = 0; k < StatLabels.Length; k++)
			{
				var stat = k;
				rows.Add(Number($"{StatLabels[stat]} EVs", "Evs", evs, e => e.Evs[stat], (e, v) => e.Evs = With(e.Evs, stat, v)));
			}
		}
		if (specs.TryGetValue("Friendship", out var friendship))
			rows.Add(Number("Friendship", "Friendship", friendship, e => e.Friendship, (e, v) => e.Friendship = checked((byte)v)));
		if (specs.TryGetValue("Ball", out var ball))
			rows.Add(Number("Ball", "Ball", ball, e => e.Ball, (e, v) => e.Ball = checked((byte)v)));
		if (specs.TryGetValue("IsEgg", out var egg))
			rows.Add(Flag("Egg", "IsEgg", egg, [FlagNo, FlagYes], e => e.IsEgg, (e, v) => e.IsEgg = v != 0));
		return rows;
	}

	private static StatSet With(StatSet set, int stat, int value) => stat switch
	{
		0 => set with { Hp = value },
		1 => set with { Atk = value },
		2 => set with { Def = value },
		3 => set with { SpA = value },
		4 => set with { SpD = value },
		_ => set with { Spe = value },
	};

	private static IReadOnlyList<string> Slots(HackNames names, int species, int max) => [.. names.AbilitySlots(species).Take(Math.Max(0, max) + 1)];

	public static IReadOnlyList<string> HiddenChoices(HackNames names, HackMon mon)
	{
		var slots = names.AbilitySlots(mon.Species);
		if (slots.Count <= 2)
			return [FlagNo, FlagYes];
		return [$"{FlagNo} ({slots[(int)(mon.Pid & 1)]})", $"{FlagYes} ({slots[2]})"];
	}

	public static string? AbilityShown(HackNames names, HackMon mon)
	{
		var slots = names.AbilitySlots(mon.Species);
		if (mon.AbilitySlot is { } slot)
			return slot < slots.Count ? slots[slot] : $"slot {slot + 1}";
		if (ProofFlagUnnamed || mon.HiddenAbility is not { } hidden)
			return null;
		var at = hidden ? 2 : (int)(mon.Pid & 1);
		return at < slots.Count ? slots[at] : null;
	}

	private static string Show(HackNames names, HackNameKind kind, int id) =>
		!names.Has(kind) ? id.ToString(CultureInfo.InvariantCulture)
		: id == 0 && (kind is HackNameKind.Move or HackNameKind.Item) ? None
		: names.Shown(kind, id);

	private static HackEditField Picker(string label, string field, HackNameKind kind, HackNames names, EditFieldSpec spec, Func<HackMonEditModel, int> value, Action<HackMonEditModel, int> write) =>
		new(label, field, e => Show(names, kind, value(e)), write)
		{
			Kind = HackEditRowKind.Picker,
			NameKind = kind,
			Names = names,
			Spec = spec,
			Current = e => value(e),
		};

	private static HackEditField Number(string label, string field, EditFieldSpec spec, Func<HackMonEditModel, long?> value, Action<HackMonEditModel, int> write) =>
		new(label, field, e => value(e)?.ToString(CultureInfo.InvariantCulture) ?? "", write)
		{
			Spec = spec,
			Current = value,
		};

	private static string ChoiceText(IReadOnlyList<string> choices, long? value) =>
		value is not { } v ? "" : v >= 0 && v < choices.Count ? choices[(int)v] : $"#{v}";

	private static HackEditField Choice(string label, string field, EditFieldSpec spec, HackMonEditModel edit, Func<HackMonEditModel, IReadOnlyList<string>> choices, Func<HackMonEditModel, byte?> value, Action<HackMonEditModel, int> write) =>
		new(label, field, e => ChoiceText(choices(e), value(e)), write)
		{
			Kind = HackEditRowKind.Choice,
			Spec = spec,
			Choices = choices(edit),
			Current = e => value(e),
		};

	private static HackEditField Flag(string label, string field, EditFieldSpec spec, IReadOnlyList<string> choices, Func<HackMonEditModel, bool?> value, Action<HackMonEditModel, int> write) =>
		new(label, field, e => value(e) is { } on ? choices[on ? 1 : 0] : "", write)
		{
			Kind = HackEditRowKind.Flag,
			Spec = spec,
			Choices = choices,
			Current = e => value(e) is { } on ? (on ? 1 : 0) : null,
		};

	private static string Banner(HackEditField field) =>
		!ProofNoBanner && field.NameKind is { } kind && field.Names is { } names && !names.Has(kind) ? " " + HackNames.Banner : "";

	private static string Noun(HackNameKind kind) => kind switch
	{
		HackNameKind.Species => "species",
		HackNameKind.Move => "move",
		HackNameKind.Item => "item",
		_ => "ability",
	};

	private static string? Outside(HackEditField field, HackNameKind kind, HackNames names, int id) =>
		names.WhyRefused(kind, id) is { } why ? $"{field.Label}: {why}" : null;

	public static string? Parse(HackEditField field, string text, out int value)
	{
		value = 0;
		var t = text.Trim();
		switch (field.Kind)
		{
			case HackEditRowKind.Text:
				return $"{field.Label}: {field.Fixed ?? "this field is not written"}";
			case HackEditRowKind.Flag:
				return FlagValue(field, t, out value);
			case HackEditRowKind.Choice when !ProofNamesIgnored:
				return ChoiceValue(field, t, out value);
			case HackEditRowKind.Picker when !ProofNamesIgnored && field.NameKind is { } kind:
				return PickerValue(field, kind, field.Names ?? HackNames.Empty, t, out value);
		}
		if (!int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0)
			return $"{field.Label}: \"{text}\" is not a number of zero or more" + Banner(field);
		return null;
	}

	private static string? PickerValue(HackEditField field, HackNameKind kind, HackNames names, string t, out int value)
	{
		value = 0;
		if (t.Length == 0 || t == "-" || string.Equals(t, None, StringComparison.OrdinalIgnoreCase))
			return kind is HackNameKind.Move or HackNameKind.Item ? null : $"{field.Label}: give a {Noun(kind)}";
		if (int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out value))
			return Outside(field, kind, names, value);
		var match = IdAtEnd.Match(t);
		if (!match.Success)
			match = IdAtStart.Match(t);
		if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out value))
			return Outside(field, kind, names, value);
		value = 0;
		if (!names.Has(kind))
			return $"{field.Label}: \"{t}\" is not a number of zero or more" + Banner(field);
		if (names.Resolve(kind, t) is { } id)
		{
			value = id;
			return Outside(field, kind, names, id);
		}
		var found = names.Search(kind, t);
		if (found.Count == 0)
			return $"{field.Label}: no {Noun(kind)} in this hack's table is called \"{t}\"";
		var some = string.Join(", ", found.Take(3).Select(e => e.Label));
		return $"{field.Label}: \"{t}\" fits {found.Count}{(found.Count >= HackNames.SearchCap ? " or more" : "")} {Noun(kind)} names ({some}{(found.Count > 3 ? ", ..." : "")}); pick one from the list or type #id";
	}

	private static string Head(string choice) => choice.Split(':')[0].Trim();

	private static string? Tail(string choice) => choice.IndexOf(':') is var at and >= 0 ? choice[(at + 1)..].Trim() : null;

	private static string? ChoiceValue(HackEditField field, string t, out int value)
	{
		for (var i = 0; i < field.Choices.Count; i++)
		{
			if (string.Equals(field.Choices[i], t, StringComparison.OrdinalIgnoreCase) || string.Equals(Head(field.Choices[i]), t, StringComparison.OrdinalIgnoreCase))
			{
				value = i;
				return null;
			}
		}
		for (var i = 0; i < field.Choices.Count; i++)
		{
			if (Tail(field.Choices[i]) is { Length: > 0 } tail && string.Equals(tail, t, StringComparison.OrdinalIgnoreCase))
			{
				value = i;
				return null;
			}
		}
		var number = t.StartsWith('#') ? t[1..] : t;
		if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value < Math.Max(field.Choices.Count, (field.Spec?.Max ?? -1) + 1))
			return null;
		value = 0;
		return $"{field.Label}: \"{t}\" is not one of {string.Join(", ", field.Choices)}";
	}

	private static string? FlagValue(HackEditField field, string t, out int value)
	{
		value = t.Split(' ', '(')[0].ToLowerInvariant() switch
		{
			FlagYes or "true" or "1" => 1,
			FlagNo or "false" or "0" => 0,
			_ => -1,
		};
		if (value >= 0)
			return null;
		value = 0;
		return $"{field.Label}: \"{t}\" is not {FlagYes} or {FlagNo}";
	}

	public static IReadOnlyList<string> Suggest(HackEditField field, string? text)
	{
		if (field.Kind != HackEditRowKind.Picker || field.NameKind is not { } kind || field.Names is not { } names || !names.Has(kind))
			return [];
		return [.. names.Search(kind, text).Select(e => e.Name.Length == 0 || e.Label != e.Name ? e.Label : $"{e.Label} (#{e.Id})")];
	}

	public static string? EvNote(HackMonEditModel edit)
	{
		var total = Enumerable.Range(0, StatLabels.Length).Sum(k => edit.Evs[k]);
		return ProofNoEvNote || total <= EvTotalNote ? null
			: $"The EVs add up to {total}, above the {EvTotalNote} the main-series games allow; this hack's own limit is not known, so they are written as typed.";
	}

	public static string? Fill(HackMonEditModel edit, IReadOnlyList<(HackEditField Field, string Text)> values)
	{
		foreach (var (field, text) in values)
		{
			if (text == field.Read(edit))
				continue;
			if (field.Fixed is { } fixedWhy)
				return $"{field.Label}: {fixedWhy}";
			if (field.Kind == HackEditRowKind.Text && field.WriteText is { } writeText)
			{
				if (!edit.CanEdit(field.Field))
					return $"{field.Label}: {edit.LockedReason(field.Field)}";
				try
				{
					writeText(edit, text);
				}
				catch (InvalidOperationException ex)
				{
					return ex.Message.StartsWith(field.Field + ": ", StringComparison.Ordinal) ? ex.Message : $"{field.Label}: {ex.Message}";
				}
				continue;
			}
			if (field.Current is null && !edit.CanEdit(field.Field))
				return $"{field.Label}: {edit.LockedReason(field.Field)}";
			if (Parse(field, text, out var value) is { } refused)
				return refused;
			if (field.Current is { } current && current(edit) == value)
				continue;
			if (!edit.CanEdit(field.Field))
				return $"{field.Label}: {edit.LockedReason(field.Field)}";
			if (!ProofNoRange && field.Spec is { } spec && (value < spec.Min || value > spec.Max))
				return $"{field.Label}: {value} is outside {spec.Range}";
			try
			{
				field.Write(edit, value);
			}
			catch (Exception ex) when (ex is OverflowException or InvalidOperationException or ArgumentOutOfRangeException)
			{
				return $"{field.Label}: {ex.Message}";
			}
		}
		return null;
	}
}
