using PKHeX.Core;

namespace PKHaX.Mobile.Services;

/// <summary>An entry in a picker: what the user sees, and the raw index PKHeX.Core stores.</summary>
public readonly record struct NamedValue(int Value, string Name)
{
	public override string ToString() => Name;
}

/// <summary>
/// Builds the selection lists. PKHeX.Core exposes these as arrays indexed BY ID, which is why binding one
/// directly shows entries in internal order; everything here is sorted by name so a human can find things.
/// Blank slots are dropped; index 0 (the real "none" value) is kept and pinned to the top.
/// </summary>
public sealed class GameLists
{
	private readonly GameStrings s;

	public GameLists(SaveManager saves) => s = saves.Strings;

	public IReadOnlyList<NamedValue> Species { get; private set; } = [];
	public IReadOnlyList<NamedValue> Abilities { get; private set; } = [];
	public IReadOnlyList<NamedValue> Moves { get; private set; } = [];
	public IReadOnlyList<NamedValue> Items { get; private set; } = [];
	public IReadOnlyList<NamedValue> Natures { get; private set; } = [];
	public IReadOnlyList<NamedValue> Balls { get; private set; } = [];
	public IReadOnlyList<NamedValue> Games { get; private set; } = [];
	public IReadOnlyList<NamedValue> Languages { get; private set; } = [];

	/// <summary>Rebuilds every list, clamped to what the loaded save's format supports.</summary>
	public void Build(SaveFile sav)
	{
		var pk = sav.BlankPKM;
		Species = Sorted(s.specieslist, pk.MaxSpeciesID);
		Abilities = Sorted(s.abilitylist, pk.MaxAbilityID);
		Moves = Sorted(s.movelist, pk.MaxMoveID);
		Items = Sorted(s.itemlist, pk.MaxItemID);
		Balls = Sorted(s.balllist, s.balllist.Length - 1);
		Games = Sorted(s.gamelist, s.gamelist.Length - 1);
		Natures = Sorted(s.natures, s.natures.Length - 1, sort: false);
		Languages = Sorted(s.languageNames, s.languageNames.Length - 1, sort: false);
	}

	private static IReadOnlyList<NamedValue> Sorted(IReadOnlyList<string> src, int max, bool sort = true)
	{
		if (src.Count == 0) return [];
		var cap = max <= 0 || max >= src.Count ? src.Count - 1 : max;

		var list = new List<NamedValue>(cap + 1);
		for (int i = 1; i <= cap; i++)
		{
			var name = src[i];
			if (string.IsNullOrWhiteSpace(name)) continue;
			list.Add(new NamedValue(i, name));
		}
		if (sort)
			list.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

		var none = !string.IsNullOrWhiteSpace(src[0]) ? src[0] : "(None)";
		list.Insert(0, new NamedValue(0, none));
		return list;
	}

	/// <summary>Form names for a species, or empty when it has no alternate formes.</summary>
	public IReadOnlyList<NamedValue> FormsFor(PKM pk)
	{
		try
		{
			var names = FormConverter.GetFormList(pk.Species, s.types, s.forms, GameInfo.GenderSymbolUnicode, pk.Context);
			if (names.Length <= 1) return [];
			var list = new List<NamedValue>(names.Length);
			for (int i = 0; i < names.Length; i++)
				list.Add(new NamedValue(i, string.IsNullOrWhiteSpace(names[i]) ? $"Form {i}" : names[i]));
			return list;
		}
		catch
		{
			return [];
		}
	}

	public string SpeciesName(int id) => Name(s.specieslist, id);
	public string AbilityName(int id) => Name(s.abilitylist, id);
	public string MoveName(int id) => Name(s.movelist, id);
	public string ItemName(int id) => Name(s.itemlist, id);
	public string NatureName(int id) => Name(s.natures, id);

	private static string Name(IReadOnlyList<string> src, int id) =>
		id >= 0 && id < src.Count ? src[id] : $"#{id}";
}
