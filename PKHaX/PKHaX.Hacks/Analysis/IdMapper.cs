using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public sealed class IdMapper
{
    public const int Gen3LastMove = 354;
    public const int Gen3LastAbility = 77;

    public const int MaxSpellingDistance = 2;

    private readonly IReadOnlyList<string> moveNames;
    private readonly IReadOnlyList<string> abilityNames;
    private readonly Dictionary<string, int> species;
    private readonly Dictionary<string, int> moves;
    private readonly Dictionary<string, int> abilities;
    private readonly Dictionary<string, int> items;
    private readonly string[] gen3Items;
    private readonly string[] moveKeys;

    public IdMapper(GameStrings strings)
    {
        moveNames = strings.movelist;
        abilityNames = strings.abilitylist;
        species = Index(strings.specieslist);
        moveKeys = [.. strings.movelist.Select(n => Normalise(WithoutZSuffix(n)))];
        moves = Index([.. strings.movelist.Select(WithoutZSuffix)]);
        abilities = Index(strings.abilitylist);
        items = Index(strings.itemlist);
        gen3Items = strings.GetItemStrings(EntityContext.Gen3, GameVersion.E);
    }

    public static IdMapper English() => new(GameInfo.GetStrings("en"));

    public static string Normalise(string name)
    {
        var decomposed = WellFormed(name).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(c) || c is '♀' or '♂')
                sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    private static string WellFormed(string name)
    {
        StringBuilder? sb = null;
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            var pair = char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]);
            if (pair || (!char.IsSurrogate(c) && c is not ('\uFFFE' or '\uFFFF')))
            {
                sb?.Append(c);
                if (pair)
                {
                    i++;
                    sb?.Append(name[i]);
                }
                continue;
            }
            sb ??= new StringBuilder(name.Length).Append(name, 0, i);
        }
        return sb?.ToString() ?? name;
    }

    private static Dictionary<string, int> Index(IReadOnlyList<string> names)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 1; i < names.Count; i++)
        {
            var key = Normalise(names[i]);
            if (key.Length != 0)
                map.TryAdd(key, i);
        }
        return map;
    }

    public int? Species(string name) => species.TryGetValue(Normalise(name), out var id) ? id : null;

    public static int Distance(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    private static int? SameSlot(int id, string name, int last, IReadOnlyList<string> own)
    {
        if (id < 1 || id > last || id >= own.Count)
            return null;
        var key = Normalise(name);
        return key.Length > 0 && Distance(key, Normalise(own[id])) <= MaxSpellingDistance ? id : null;
    }

    public int? Move(int id, string name)
    {
        var key = Normalise(name);
        if (key.Length > 0 && id > 0 && id < moveKeys.Length && moveKeys[id] == key)
            return id;
        return moves.TryGetValue(key, out var national) ? national : SameSlot(id, name, Gen3LastMove, moveNames);
    }

    public static string WithoutZSuffix(string name) =>
        name.EndsWith(" (P)", StringComparison.Ordinal) || name.EndsWith(" (S)", StringComparison.Ordinal) ? name[..^4] : name;

    public int? Ability(int id, string name) =>
        abilities.TryGetValue(Normalise(name), out var national) ? national : SameSlot(id, name, Gen3LastAbility, abilityNames);

    public int? ItemByName(string name) => items.TryGetValue(Normalise(name), out var national) ? national : null;

    public int? Item(int id, string name)
    {
        var key = Normalise(name);
        if (key.Length == 0)
            return null;
        if (id > 0 && id < gen3Items.Length && Normalise(gen3Items[id]) == key)
        {
            var future = ItemConverter.GetItemFuture3((ushort)id);
            if (ItemConverter.IsItemTransferable34(future))
                return future;
        }
        return items.TryGetValue(key, out var national) ? national : null;
    }
}
