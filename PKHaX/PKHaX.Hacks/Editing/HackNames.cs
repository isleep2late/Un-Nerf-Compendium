using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using PKHaX.Hacks.Analysis;

namespace PKHaX.Hacks.Editing;

public enum HackNameKind
{
    Species,
    Move,
    Item,
    Ability,
}

public sealed record HackNameEntry(int Id, string Name, string Label);

public sealed class HackNames
{
    public const string Banner = "Names come from the hack's ROM. Open or import the ROM once and they appear here.";
    public const int SearchCap = 50;

    private sealed record Catalog(IReadOnlyDictionary<int, HackNameEntry> ById, IReadOnlyList<HackNameEntry> Ordered, int Limit);

    private static readonly Catalog None = new(new Dictionary<int, HackNameEntry>(), [], 0);

    private readonly Catalog[] catalogs;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<int>> speciesAbilities;

    private HackNames(Catalog[] catalogs, IReadOnlyDictionary<int, IReadOnlyList<int>> speciesAbilities)
    {
        this.catalogs = catalogs;
        this.speciesAbilities = speciesAbilities;
    }

    public static HackNames Empty { get; } = new([None, None, None, None], new Dictionary<int, IReadOnlyList<int>>());

    public static HackNames From(HackData? data)
    {
        if (data is null)
            return Empty;
        var abilities = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var s in data.Species)
            abilities[s.Id] = s.Abilities;
        return new HackNames(
        [
            Build(data.Species.Select(s => (s.Id, s.Name))),
            Build(data.Moves.Select(m => (m.Id, m.Name))),
            Build(data.Items.Select(i => (i.Id, i.Name))),
            Build(data.Abilities.Select(a => (a.Id, a.Name))),
        ], abilities);
    }

    private static Catalog Build(IEnumerable<(int Id, string Name)> rows)
    {
        var named = new Dictionary<int, string>();
        var limit = 0;
        foreach (var (id, name) in rows)
        {
            if (id < 0)
                continue;
            limit = Math.Max(limit, id + 1);
            if (!string.IsNullOrWhiteSpace(name))
                named[id] = name.Trim();
        }
        if (limit == 0)
            return None;
        var repeated = named.Values.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byId = new Dictionary<int, HackNameEntry>();
        foreach (var (id, name) in named)
            byId[id] = new HackNameEntry(id, name, repeated.Contains(name) ? $"{name} #{id}" : name);
        return new Catalog(byId, [.. byId.Values.OrderBy(e => e.Id)], limit);
    }

    private Catalog Of(HackNameKind kind) => catalogs[(int)kind];

    public bool IsEmpty => catalogs.All(c => c.Limit == 0);

    public bool Has(HackNameKind kind) => Of(kind).Limit > 0;

    public string? NumberBanner => IsEmpty ? Banner : null;

    public int Limit(HackNameKind kind) => Of(kind).Limit;

    public int Count(HackNameKind kind) => Of(kind).Ordered.Count;

    public string? Plain(HackNameKind kind, int id) => Of(kind).ById.TryGetValue(id, out var e) ? e.Name : null;

    public string Name(HackNameKind kind, int id)
    {
        var c = Of(kind);
        if (c.Limit == 0)
            return $"#{id}";
        if (id < 0 || id >= c.Limit)
            return $"#{id} (outside this hack's table of {c.Limit})";
        return c.ById.TryGetValue(id, out var e) ? e.Label : $"#{id} (no name read from the ROM)";
    }

    public string Shown(HackNameKind kind, int id) => Of(kind).ById.TryGetValue(id, out var e) ? $"{e.Label} (#{id})" : Name(kind, id);

    public string? WhyRefused(HackNameKind kind, int id)
    {
        var c = Of(kind);
        if (c.Limit == 0)
            return null;
        return id < 0 || id >= c.Limit ? $"{kind} #{id} is outside this hack's table of {c.Limit}, so it is not written" : null;
    }

    public static bool TryParseId(string? text, out int id)
    {
        id = 0;
        var t = text?.Trim();
        return t is { Length: > 1 } && t[0] == '#' && int.TryParse(t.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }

    public IReadOnlyList<HackNameEntry> Search(HackNameKind kind, string? text)
    {
        var c = Of(kind);
        var t = text?.Trim() ?? "";
        if (TryParseId(t, out var id))
            return id < c.Limit || c.Limit == 0 ? [c.ById.TryGetValue(id, out var hit) ? hit : new HackNameEntry(id, "", Name(kind, id))] : [];
        if (t.Length == 0)
            return [.. c.Ordered.Take(SearchCap)];
        var exact = new List<HackNameEntry>();
        var prefix = new List<HackNameEntry>();
        var inside = new List<HackNameEntry>();
        foreach (var e in c.Ordered)
        {
            if (string.Equals(e.Name, t, StringComparison.OrdinalIgnoreCase))
                exact.Add(e);
            else if (e.Name.StartsWith(t, StringComparison.OrdinalIgnoreCase))
                prefix.Add(e);
            else if (e.Name.Contains(t, StringComparison.OrdinalIgnoreCase))
                inside.Add(e);
        }
        return [.. exact.Concat(prefix).Concat(inside).Take(SearchCap)];
    }

    public int? Resolve(HackNameKind kind, string? text)
    {
        if (TryParseId(text, out var id))
            return id;
        var found = Search(kind, text);
        var t = text?.Trim() ?? "";
        return t.Length > 0 && found.Count > 0 && (found.Count == 1 || string.Equals(found[0].Name, t, StringComparison.OrdinalIgnoreCase) || string.Equals(found[0].Label, t, StringComparison.OrdinalIgnoreCase)) ? found[0].Id : null;
    }

    public IReadOnlyList<string> AbilitySlots(int species)
    {
        string[] labels = ["Slot 1", "Slot 2", "Hidden"];
        if (!speciesAbilities.TryGetValue(species, out var row) || row.Count == 0)
            return labels;
        var result = new List<string>();
        for (var i = 0; i < Math.Max(labels.Length, row.Count); i++)
        {
            var label = i < labels.Length ? labels[i] : $"Slot {i + 1}";
            result.Add(i < row.Count ? $"{label}: {Name(HackNameKind.Ability, row[i])}" : label);
        }
        return result;
    }
}
