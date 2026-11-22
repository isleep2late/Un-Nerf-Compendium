using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PKHaX.Hacks.Analysis;

public static class HackTableKey
{
    public static string Of(HackData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(Canonical(data))));
    }

    public static string Canonical(HackData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new StringBuilder(1 << 16);
        sb.Append("platform ").Append(data.Platform ?? "").Append('\n');
        sb.Append("engine ").Append(data.Engine ?? "").Append('\n');
        foreach (var s in data.Species)
            Species(sb, 'S', s);
        foreach (var s in data.Forms)
            Species(sb, 'F', s);
        foreach (var m in data.Moves)
        {
            sb.Append('M').Append('|').Append(Int(m.Id)).Append('|').Append(Text(m.Name)).Append('|').Append(Int(m.Power)).Append('|').Append(Int(m.Type))
                .Append('|').Append(Int(m.Accuracy)).Append('|').Append(Int(m.Pp)).Append('|').Append(Int(m.Category)).Append('|').Append(m.CategoryByType ? '1' : '0').Append('\n');
        }
        foreach (var i in data.Items)
            sb.Append('I').Append('|').Append(Int(i.Id)).Append('|').Append(Text(i.Name)).Append('|').Append(Int(i.Pocket)).Append('|').Append(Int(i.Price)).Append('\n');
        foreach (var a in data.Abilities)
            sb.Append('A').Append('|').Append(Int(a.Id)).Append('|').Append(Text(a.Name)).Append('\n');
        foreach (var kv in data.LevelUp.OrderBy(kv => kv.Key))
        {
            sb.Append('L').Append('|').Append(Int(kv.Key)).Append('|');
            foreach (var l in kv.Value)
                sb.Append(Int(l.Level)).Append(':').Append(Int(l.Move)).Append(',');
            sb.Append('\n');
        }
        for (var t = 0; t < data.TypeNames.Count; t++)
            sb.Append('T').Append('|').Append(Int(t)).Append('|').Append(Text(data.TypeNames[t])).Append('\n');
        return sb.ToString();
    }

    private static void Species(StringBuilder sb, char kind, HackSpecies s)
    {
        sb.Append(kind).Append('|').Append(Int(s.Id)).Append('|').Append(Text(s.Name)).Append('|').Append(Int(s.Form)).Append('|').Append(Ints(s.Types))
            .Append('|').Append(Ints(s.BaseStats)).Append('|').Append(Ints(s.Abilities)).Append('|').Append(Int(s.Growth)).Append('|').Append(Int(s.GenderRatio))
            .Append('|').Append(s.GenderRead ? '1' : '0').Append('|').Append(Int(s.CatchRate)).Append('|').Append(Int(s.ExpYield)).Append('|').Append(Int(s.Dex))
            .Append('|').Append(Int(s.BaseSpecies)).Append('|').Append(Text(s.FormName ?? "")).Append('\n');
    }

    private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Int(int? value) => value is { } v ? v.ToString(CultureInfo.InvariantCulture) : "-";

    private static string Ints(IReadOnlyList<int> values) => string.Join(",", values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    private static string Text(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("|", "\\p", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);
}

public sealed record HackTableShare(int Same, int Of)
{
    public double Share => Of == 0 ? 1.0 : (double)Same / Of;

    public override string ToString() => $"{Same.ToString(CultureInfo.InvariantCulture)}/{Of.ToString(CultureInfo.InvariantCulture)}";
}

public sealed record HackTableShares(HackTableShare Species, HackTableShare Moves, HackTableShare Items, HackTableShare Abilities);

public static class HackTableCompare
{
    public static bool Twins(HackData a, HackData b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return string.Equals(a.TableKey ?? HackTableKey.Of(a), b.TableKey ?? HackTableKey.Of(b), StringComparison.Ordinal);
    }

    public static HackTableShares Shares(HackData a, HackData b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new HackTableShares(
            Share(a.Species.Select(s => (s.Id, s.Name)), b.Species.Select(s => (s.Id, s.Name))),
            Share(a.Moves.Select(m => (m.Id, m.Name)), b.Moves.Select(m => (m.Id, m.Name))),
            Share(a.Items.Select(i => (i.Id, i.Name)), b.Items.Select(i => (i.Id, i.Name))),
            Share(a.Abilities.Select(x => (x.Id, x.Name)), b.Abilities.Select(x => (x.Id, x.Name))));
    }

    private static HackTableShare Share(IEnumerable<(int Id, string Name)> a, IEnumerable<(int Id, string Name)> b)
    {
        var left = ById(a);
        var right = ById(b);
        var (small, large) = left.Count <= right.Count ? (left, right) : (right, left);
        var same = 0;
        foreach (var kv in small)
        {
            if (large.TryGetValue(kv.Key, out var name) && string.Equals(name, kv.Value, StringComparison.Ordinal))
                same++;
        }
        return new HackTableShare(same, small.Count);
    }

    private static Dictionary<int, string> ById(IEnumerable<(int Id, string Name)> rows)
    {
        var result = new Dictionary<int, string>();
        foreach (var (id, name) in rows)
            result[id] = name;
        return result;
    }
}
