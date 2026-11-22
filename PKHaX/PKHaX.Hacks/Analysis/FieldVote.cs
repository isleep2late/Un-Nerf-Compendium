using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public readonly record struct BitField(int Bit, int Width)
{
    public int End => Bit + Width;

    public bool ByteAligned => Width == 8 && Bit % 8 == 0;

    public bool Overlaps(BitField other) => Bit < other.End && other.Bit < End;

    public int Read(ReadOnlySpan<byte> row) => FieldVote.Bits(row, Bit, Width);

    public string Where => ByteAligned ? $"+0x{Bit / 8:X}" : $"+0x{Bit / 8:X} bit {Bit % 8} width {Width}";
}

public sealed record FieldVoteResult(BitField Field, int Agree, int Compared, IReadOnlyDictionary<int, int>? Map = null)
{
    public double Share => Compared == 0 ? 0 : (double)Agree / Compared;

    public int? Value(ReadOnlySpan<byte> row)
    {
        var raw = Field.Read(row);
        if (Map is null)
            return raw;
        return Map.TryGetValue(raw, out var mapped) ? mapped : null;
    }
}

public static class FieldVote
{
    public const int MinCompared = 100;
    public const double MinShare = 0.6;

    public static int Bits(ReadOnlySpan<byte> row, int bit, int width)
    {
        var value = 0;
        for (var k = 0; k < width; k++)
        {
            var b = bit + k;
            if (((row[b >> 3] >> (b & 7)) & 1) != 0)
                value |= 1 << k;
        }
        return value;
    }

    public static IEnumerable<BitField> Positions(int rowBits, IReadOnlyList<int> widths, IReadOnlyCollection<BitField>? taken = null)
    {
        foreach (var width in widths)
        {
            for (var bit = 0; bit + width <= rowBits; bit += 8)
            {
                var f = new BitField(bit, width);
                if (Free(f, taken))
                    yield return f;
            }
            for (var bit = 0; bit + width <= rowBits; bit++)
            {
                if (bit % 8 == 0)
                    continue;
                var f = new BitField(bit, width);
                if (Free(f, taken))
                    yield return f;
            }
        }
    }

    public static bool Free(BitField f, IReadOnlyCollection<BitField>? taken)
    {
        if (taken is null)
            return true;
        foreach (var t in taken)
        {
            if (f.Overlaps(t))
                return false;
        }
        return true;
    }

    private static int RowBits(IReadOnlyList<byte[]> rows, IReadOnlyList<int?> expected)
    {
        var bits = int.MaxValue;
        var n = Math.Min(rows.Count, expected.Count);
        for (var i = 0; i < n; i++)
        {
            if (expected[i] is not null)
                bits = Math.Min(bits, rows[i].Length * 8);
        }
        return bits == int.MaxValue ? 0 : bits;
    }

    public static int Compared(IReadOnlyList<byte[]> rows, IReadOnlyList<int?> expected)
    {
        var n = Math.Min(rows.Count, expected.Count);
        var c = 0;
        for (var i = 0; i < n; i++)
        {
            if (expected[i] is not null)
                c++;
        }
        return c;
    }

    public static FieldVoteResult? Top(IReadOnlyList<byte[]> rows, IReadOnlyList<int?> expected, IReadOnlyList<int> widths, IReadOnlyCollection<BitField>? taken = null)
    {
        var compared = Compared(rows, expected);
        if (compared == 0)
            return null;
        var n = Math.Min(rows.Count, expected.Count);
        FieldVoteResult? best = null;
        foreach (var f in Positions(RowBits(rows, expected), widths, taken))
        {
            var agree = 0;
            for (var i = 0; i < n; i++)
            {
                if (expected[i] is { } e && f.Read(rows[i]) == e)
                    agree++;
            }
            if (best is null || agree > best.Agree)
                best = new FieldVoteResult(f, agree, compared);
        }
        return best;
    }

    public static FieldVoteResult? Find(IReadOnlyList<byte[]> rows, IReadOnlyList<int?> expected, IReadOnlyList<int> widths, IReadOnlyCollection<BitField>? taken = null, int minCompared = MinCompared, double minShare = MinShare)
    {
        if (Compared(rows, expected) < minCompared)
            return null;
        var best = Top(rows, expected, widths, taken);
        return best is not null && best.Agree >= minShare * best.Compared ? best : null;
    }

    public static FieldVoteResult? TopMapped(IReadOnlyList<byte[]> rows, IReadOnlyList<int?> expected, IReadOnlyList<int> widths, IReadOnlyCollection<BitField>? taken = null)
    {
        var compared = Compared(rows, expected);
        if (compared == 0)
            return null;
        var n = Math.Min(rows.Count, expected.Count);
        FieldVoteResult? best = null;
        foreach (var f in Positions(RowBits(rows, expected), widths, taken))
        {
            var tally = new Dictionary<int, Dictionary<int, int>>();
            for (var i = 0; i < n; i++)
            {
                if (expected[i] is not { } e)
                    continue;
                var raw = f.Read(rows[i]);
                if (!tally.TryGetValue(e, out var counts))
                {
                    counts = new Dictionary<int, int>();
                    tally[e] = counts;
                }
                counts[raw] = counts.TryGetValue(raw, out var c) ? c + 1 : 1;
            }
            if (tally.Count < 2)
                continue;
            var map = new Dictionary<int, int>();
            var agree = 0;
            var modes = tally.Select(kv => (Expected: kv.Key, Mode: kv.Value.OrderByDescending(c => c.Value).ThenBy(c => c.Key).First()))
                .OrderByDescending(x => x.Mode.Value).ThenBy(x => x.Expected);
            foreach (var (e, mode) in modes)
            {
                if (map.TryAdd(mode.Key, e))
                    agree += mode.Value;
            }
            if (map.Count >= 2 && (best is null || agree > best.Agree || (agree == best.Agree && !best.Field.ByteAligned && f.Width < best.Field.Width && f.Bit >= best.Field.Bit && f.End <= best.Field.End)))
                best = new FieldVoteResult(f, agree, compared, map);
        }
        return best;
    }

    public static FieldVoteResult? FindMapped(IReadOnlyList<byte[]> rows, IReadOnlyList<int?> expected, IReadOnlyList<int> widths, IReadOnlyCollection<BitField>? taken = null, int minCompared = MinCompared, double minShare = MinShare)
    {
        if (Compared(rows, expected) < minCompared)
            return null;
        var best = TopMapped(rows, expected, widths, taken);
        return best is not null && best.Agree >= minShare * best.Compared ? best : null;
    }

    public const int MaxShift = 3;

    public static int BestShift(IReadOnlyList<byte[]> rows, IReadOnlyList<int> ids, Func<int, int?> expectedAt, FieldVoteResult vote, int maxShift = MaxShift)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(expectedAt);
        ArgumentNullException.ThrowIfNull(vote);
        var best = 0;
        var bestAgree = -1;
        for (var step = 0; step <= 2 * maxShift; step++)
        {
            var k = step == 0 ? 0 : step % 2 == 1 ? -((step + 1) / 2) : step / 2;
            var agree = 0;
            for (var j = 0; j < Math.Min(rows.Count, ids.Count); j++)
            {
                if (expectedAt(ids[j] + k) is { } e && vote.Value(rows[j]) == e)
                    agree++;
            }
            if (agree > bestAgree)
            {
                best = k;
                bestAgree = agree;
            }
        }
        return best;
    }

    public static int? StockType1(int national)
    {
        if (national <= 0)
            return null;
        if (national <= PersonalTable.StockUSUM.MaxSpeciesID && national < PersonalTable.StockUSUM.Count)
            return PersonalTable.StockUSUM[national].Type1;
        if (national < PersonalTable.StockSV.Count)
            return PersonalTable.StockSV[national].Type1;
        return null;
    }

    public const int LastGen3National = 386;

    public static int? BaseType1(int national) =>
        national is >= 1 and <= LastGen3National ? PersonalTable.StockE[national].Type1 : StockType1(national);

    public static int? BaseType2(int national) =>
        national is >= 1 and <= LastGen3National ? PersonalTable.StockE[national].Type2 : StockType2(national);

    public static int? StockType2(int national)
    {
        if (national <= 0)
            return null;
        if (national <= PersonalTable.StockUSUM.MaxSpeciesID && national < PersonalTable.StockUSUM.Count)
            return PersonalTable.StockUSUM[national].Type2;
        if (national < PersonalTable.StockSV.Count)
            return PersonalTable.StockSV[national].Type2;
        return null;
    }
}

public sealed record MoveAnchor(int Move, string Name, int Power, int Accuracy, int Pp);

public sealed record MoveFields(BitField Power, BitField Accuracy, BitField Pp, FieldVoteResult? Type, BitField? Category, IReadOnlyDictionary<int, int>? CategoryMap, int Anchors, string Note)
{
    public int? CategoryOf(ReadOnlySpan<byte> row) =>
        Category is { } c && CategoryMap is { } map && map.TryGetValue(c.Read(row), out var v) ? v : null;
}

public static class MoveAnchors
{
    public static IReadOnlyList<MoveAnchor> All { get; } =
    [
        new(1, "Pound", 40, 100, 35),
        new(2, "Karate Chop", 50, 100, 25),
        new(3, "Double Slap", 15, 85, 10),
        new(4, "Comet Punch", 18, 85, 15),
        new(5, "Mega Punch", 80, 85, 20),
        new(6, "Pay Day", 40, 100, 20),
        new(7, "Fire Punch", 75, 100, 15),
        new(8, "Ice Punch", 75, 100, 15),
        new(9, "Thunder Punch", 75, 100, 15),
        new(10, "Scratch", 40, 100, 35),
    ];

    public const int MinAgree = 7;
    public const int Candidates = 6;
    public const double ShapeShare = 0.98;
    public const double PowerFiveShare = 0.85;
    public const double CategoryShare = 0.9;
    public const int MaxPp = 64;

    public static readonly int[] PowerWidths = [8, 9];
    public static readonly int[] AccuracyWidths = [8, 7];
    public static readonly int[] PpWidths = [8];
    public static readonly int[] TypeWidths = [8, 5];
    public static readonly int[] CategoryWidths = [8, 2];

    public static List<(MoveAnchor Anchor, int Row)> Rows(IReadOnlyList<int?> nationals, int count)
    {
        var found = new List<(MoveAnchor, int)>();
        foreach (var a in All)
        {
            for (var i = 1; i < Math.Min(count, nationals.Count); i++)
            {
                if (nationals[i] == a.Move)
                {
                    found.Add((a, i));
                    break;
                }
            }
        }
        return found;
    }

    private static List<BitField> Candidate(int rowBits, IReadOnlyList<int> widths, IReadOnlyCollection<BitField>? taken, IReadOnlyList<(MoveAnchor Anchor, byte[] Row)> anchors, Func<MoveAnchor, int> value) =>
        [.. FieldVote.Positions(rowBits, widths, taken)
            .Select(f => (F: f, Agree: anchors.Count(x => f.Read(x.Row) == value(x.Anchor))))
            .Where(x => x.Agree >= MinAgree)
            .OrderByDescending(x => x.Agree)
            .Take(Candidates)
            .Select(x => x.F)];

    public static MoveFields? Find(int count, Func<int, byte[]?> row, IReadOnlyList<int?> nationals, out string why, IReadOnlyCollection<BitField>? excluded = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(nationals);
        var cache = new byte[]?[Math.Max(count, 0)];
        byte[]? Row(int i) => i >= 0 && i < cache.Length ? cache[i] ??= row(i) : null;
        var anchorRows = new List<(MoveAnchor Anchor, byte[] Row)>();
        foreach (var (a, i) in Rows(nationals, count))
        {
            if (Row(i) is { } r)
                anchorRows.Add((a, r));
        }
        if (anchorRows.Count < MinAgree)
        {
            why = $"only {anchorRows.Count} of the ten anchor moves (Pound to Scratch) are named in the move table, {MinAgree} needed";
            return null;
        }
        var bits = anchorRows.Min(x => x.Row.Length) * 8;
        var powers = Candidate(bits, PowerWidths, excluded, anchorRows, a => a.Power);
        var accuracies = Candidate(bits, AccuracyWidths, excluded, anchorRows, a => a.Accuracy);
        var pps = Candidate(bits, PpWidths, excluded, anchorRows, a => a.Pp);
        if (powers.Count == 0 || accuracies.Count == 0 || pps.Count == 0)
        {
            why = $"no field of the {bits / 8}-byte move rows carries the {(powers.Count == 0 ? "power" : accuracies.Count == 0 ? "accuracy" : "PP")} of at least {MinAgree} of the {anchorRows.Count} named anchor moves";
            return null;
        }

        var mapped = new List<(int Id, byte[] Row, int National)>();
        for (var i = 1; i < Math.Min(count, nationals.Count); i++)
        {
            if (nationals[i] is { } n && Row(i) is { } r && r.Length * 8 >= bits)
                mapped.Add((i, r, n));
        }
        (BitField P, BitField A, BitField Pp)? chosen = null;
        foreach (var p in powers)
        {
            foreach (var a in accuracies.Where(a => !a.Overlaps(p)))
            {
                foreach (var pp in pps.Where(x => !x.Overlaps(p) && !x.Overlaps(a)))
                {
                    var accOk = mapped.Count(m => a.Read(m.Row) <= 100) >= ShapeShare * mapped.Count;
                    var fiveOk = mapped.Count(m => p.Read(m.Row) % 5 == 0) >= PowerFiveShare * mapped.Count;
                    var ppOk = mapped.Count(m => pp.Read(m.Row) is > 0 and <= MaxPp) >= ShapeShare * mapped.Count;
                    if (accOk && fiveOk && ppOk)
                    {
                        chosen = (p, a, pp);
                        break;
                    }
                }
                if (chosen is not null)
                    break;
            }
            if (chosen is not null)
                break;
        }
        if (chosen is not { } c)
        {
            why = $"fields agree with the anchor moves' power, accuracy and PP, but none passes the whole-table shape over {mapped.Count} named moves (accuracy at most 100 and PP 1-{MaxPp} on {ShapeShare * 100:0}%, power a multiple of 5 on {PowerFiveShare * 100:0}%)";
            return null;
        }

        var rows = mapped.Select(m => m.Row).ToList();
        int? PpOf(int national, EntityContext context)
        {
            var table = MoveInfo.GetPPTable(context);
            return national >= 0 && national < table.Length && table[national] > 0 ? table[national] : null;
        }
        var ppBase = mapped.Select(m => PpOf(m.National, EntityContext.Gen3)).ToList();
        var ppNew = mapped.Select(m => PpOf(m.National, EntityContext.Gen9) ?? PpOf(m.National, EntityContext.Gen8)).ToList();
        var ppAgree = Math.Max(Agree(rows, ppBase, c.Pp), Agree(rows, ppNew, c.Pp));
        var ppCompared = Math.Max(FieldVote.Compared(rows, ppBase), FieldVote.Compared(rows, ppNew));
        if (ppCompared >= FieldVote.MinCompared && ppAgree < FieldVote.MinShare * ppCompared)
        {
            why = $"the PP field the anchors give agrees with PKHeX's PP on only {ppAgree} of {ppCompared} named moves ({FieldVote.MinShare * 100:0}% needed), so the fields are not taken";
            return null;
        }

        var taken = new List<BitField>();
        if (excluded is not null)
            taken.AddRange(excluded);
        taken.Add(c.P);
        taken.Add(c.A);
        taken.Add(c.Pp);
        int? TypeOf(int national, EntityContext context)
        {
            var table = MoveInfo.GetTypeTable(context);
            return national < table.Length ? table[national] : null;
        }
        var typeOld = FieldVote.FindMapped(rows, [.. mapped.Select(m => TypeOf(m.National, EntityContext.Gen5))], TypeWidths, taken);
        var typeNew = FieldVote.FindMapped(rows, [.. mapped.Select(m => TypeOf(m.National, EntityContext.Gen9))], TypeWidths, taken);
        var type = typeOld is null ? typeNew : typeNew is null ? typeOld : typeNew.Agree > typeOld.Agree ? typeNew : typeOld;

        var beside = new List<BitField>(taken);
        if (type is not null)
            beside.Add(type.Field);
        var (category, categoryMap, categoryAgree) = FindCategory(rows, [.. mapped.Select(m => c.P.Read(m.Row))], anchorRows.Select(x => x.Row).ToList(), FieldVote.Positions(bits, CategoryWidths, beside));
        var note = $"move fields found from {anchorRows.Count} anchor moves and {mapped.Count} named rows: power {c.P.Where}, accuracy {c.A.Where}, PP {c.Pp.Where} ({ppAgree}/{ppCompared} agree with PKHeX), type {(type is null ? "unread" : $"{type.Field.Where} ({type.Agree}/{type.Compared})")}, category {(category is { } cf ? $"{cf.Where} ({categoryAgree}/{mapped.Count})" : "unread")}";
        why = "";
        return new MoveFields(c.P, c.A, c.Pp, type, category, categoryMap, anchorRows.Count, note);
    }

    private static int Agree(IReadOnlyList<byte[]> rows, IReadOnlyList<int?> expected, BitField f)
    {
        var agree = 0;
        for (var i = 0; i < Math.Min(rows.Count, expected.Count); i++)
        {
            if (expected[i] is { } e && f.Read(rows[i]) == e)
                agree++;
        }
        return agree;
    }

    public static (BitField? Field, IReadOnlyDictionary<int, int>? Map, int Agree) FindCategory(IReadOnlyList<byte[]> rows, IReadOnlyList<int> power, IReadOnlyList<byte[]> anchorRows, IEnumerable<BitField> candidates)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(power);
        ArgumentNullException.ThrowIfNull(anchorRows);
        ArgumentNullException.ThrowIfNull(candidates);
        BitField? best = null;
        IReadOnlyDictionary<int, int>? bestMap = null;
        var bestAgree = 0;
        var n = Math.Min(rows.Count, power.Count);
        if (n == 0 || anchorRows.Count < MinAgree)
            return (null, null, 0);
        foreach (var f in candidates)
        {
            var values = new int[n];
            var inRange = 0;
            for (var i = 0; i < n; i++)
            {
                values[i] = f.Read(rows[i]);
                if (values[i] <= 2)
                    inRange++;
            }
            if (inRange < 0.99 * n)
                continue;
            var physical = anchorRows.GroupBy(r => f.Read(r)).OrderByDescending(g => g.Count()).First();
            if (physical.Count() < MinAgree || physical.Key > 2)
                continue;
            var statusRows = Enumerable.Range(0, n).Where(i => power[i] == 0).Select(i => values[i]).ToList();
            if (statusRows.Count == 0)
                continue;
            var status = statusRows.GroupBy(v => v).OrderByDescending(g => g.Count()).First().Key;
            if (status == physical.Key || status > 2)
                continue;
            var special = 3 - status - physical.Key;
            var agree = Enumerable.Range(0, n).Count(i => (power[i] == 0) == (values[i] == status));
            if (agree < CategoryShare * n || agree <= bestAgree)
                continue;
            best = f;
            bestAgree = agree;
            bestMap = new Dictionary<int, int> { [physical.Key] = HackMove.Physical, [special] = HackMove.Special, [status] = HackMove.StatusCategory };
        }
        return (best, bestMap, bestAgree);
    }

    public static int CategoryByType(int pkhexType, int power) =>
        power == 0 ? HackMove.StatusCategory : pkhexType is >= (int)MoveType.Fire and <= (int)MoveType.Dark ? HackMove.Special : HackMove.Physical;
}
