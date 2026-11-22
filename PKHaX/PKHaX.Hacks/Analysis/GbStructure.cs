using System;
using System.Collections.Generic;

namespace PKHaX.Hacks.Analysis;

public sealed record GbIndexMap(int Offset, IReadOnlyList<int> Dex, int Distinct)
{
    public int Length => Dex.Count;
}

public sealed record GbBaseRows(int Offset, int Stride, int Count);

public sealed record GbNameRun(int Offset, int Count, int Terminated, int Distinct);

public sealed record GbNameList(int Offset, IReadOnlyList<string> Names)
{
    public int Count => Names.Count;

    public List<string> Indexed(int skip = 0)
    {
        var list = new List<string>(Math.Max(1, Names.Count - skip + 1)) { "" };
        for (var i = skip; i < Names.Count; i++)
            list.Add(Names[i]);
        return list;
    }
}

public sealed record GbRowRun(int Offset, int Stride, int Count, int Renumbered);

public static class GbStructure
{
    public const int MinBaseRows = 100;
    public const double MinCover = 0.9;
    public const int MaxSlots = 255;
    public const int MinFixedSlots = 150;
    public const int MinListNames = 100;
    public const int MinShortListNames = 40;
    public const int MaxListChars = 12;
    public const int MaxStartSkip = 64;
    public const double MinTerminatedShare = 0.5;
    public const double MinDistinctShare = 0.5;
    public const double MaxRenumberedShare = 0.1;
    public const int MinRenumbered = 2;
    public const int MaxMovePp = 64;

    public static bool IsLetter(byte b) => b is >= 0x80 and <= 0x99 or >= 0xA0 and <= 0xB9;

    public static bool IsNameChar(byte b) => b is 0x7F or >= 0x80 and <= 0xBF or >= 0xE0 and <= 0xFF;

    public static bool FixedSlot(ReadOnlySpan<byte> slot, out bool terminated)
    {
        terminated = false;
        if (slot.Length == 0 || !IsLetter(slot[0]))
            return false;
        for (var k = 1; k < slot.Length; k++)
        {
            var b = slot[k];
            if (terminated)
            {
                if (b != GbHackData.Terminator)
                    return false;
            }
            else if (b == GbHackData.Terminator)
            {
                terminated = true;
            }
            else if (!IsNameChar(b))
            {
                return false;
            }
        }
        return true;
    }

    public static List<GbNameRun> FixedNameRuns(ReadOnlySpan<byte> rom, int minSlots = MinFixedSlots, int width = GbHackData.NameWidth)
    {
        var runs = new List<GbNameRun>();
        if (width <= 0)
            return runs;
        for (var residue = 0; residue < width; residue++)
        {
            var start = -1;
            var n = 0;
            var terminated = 0;
            for (var p = residue; p + width <= rom.Length; p += width)
            {
                if (FixedSlot(rom.Slice(p, width), out var t))
                {
                    if (n == 0)
                        start = p;
                    n++;
                    if (t)
                        terminated++;
                    continue;
                }
                Flush(rom, runs, start, n, terminated, minSlots, width);
                n = 0;
                terminated = 0;
            }
            Flush(rom, runs, start, n, terminated, minSlots, width);
        }
        runs.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        return runs;
    }

    private static void Flush(ReadOnlySpan<byte> rom, List<GbNameRun> runs, int start, int n, int terminated, int minSlots, int width)
    {
        if (n < minSlots || terminated < MinTerminatedShare * n)
            return;
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < n; i++)
            distinct.Add(Convert.ToHexString(rom.Slice(start + (i * width), width)));
        if (distinct.Count < MinDistinctShare * n)
            return;
        runs.Add(new GbNameRun(start, n, terminated, distinct.Count));
    }

    public static int StringAt(ReadOnlySpan<byte> rom, int at, int maxChars = MaxListChars)
    {
        if (at < 0 || at >= rom.Length || rom[at] is GbHackData.Terminator or 0x7F || !IsNameChar(rom[at]))
            return -1;
        for (var k = 1; k <= maxChars && at + k < rom.Length; k++)
        {
            var b = rom[at + k];
            if (b == GbHackData.Terminator)
                return k;
            if (!IsNameChar(b))
                return -1;
        }
        return -1;
    }

    public static List<GbNameList> NameLists(ReadOnlySpan<byte> rom, int minNames = MinListNames, int maxChars = MaxListChars)
    {
        var lists = new List<GbNameList>();
        var p = 0;
        while (p < rom.Length)
        {
            var at = p;
            var names = new List<string>();
            while (StringAt(rom, at, maxChars) is > 0 and var length)
            {
                names.Add(GbHackData.Text(rom.Slice(at, length + 1)) ?? "");
                at += length + 1;
            }
            if (names.Count >= minNames)
                lists.Add(new GbNameList(p, names));
            p = names.Count == 0 ? p + 1 : at + 1;
        }
        return lists;
    }

    public static int BestStart(IReadOnlyList<string> names, IReadOnlyList<string> stock, int maxSkip = MaxStartSkip)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(stock);
        var best = 0;
        var bestAgree = Agree(names, stock, 0);
        for (var skip = 1; skip <= maxSkip && names.Count - skip >= NameVote.MinAgree; skip++)
        {
            var agree = Agree(names, stock, skip);
            if (agree > bestAgree)
                (best, bestAgree) = (skip, agree);
        }
        return best;
    }

    private static int Agree(IReadOnlyList<string> names, IReadOnlyList<string> stock, int skip)
    {
        var agree = 0;
        for (var i = skip; i < names.Count && i - skip + 1 < stock.Count && i - skip < NameVote.Window; i++)
        {
            var key = IdMapper.Normalise(names[i]);
            if (key.Length > 0 && key == IdMapper.Normalise(stock[i - skip + 1]))
                agree++;
        }
        return agree;
    }

    public static List<GbRowRun> MoveRows(ReadOnlySpan<byte> rom, int stride, int need)
    {
        var runs = new List<GbRowRun>();
        if (stride < 6)
            return runs;
        for (var p = 0; p + (stride * 3) <= rom.Length; p++)
        {
            if (rom[p] != 1 || rom[p + stride] != 2 || rom[p + (2 * stride)] != 3)
                continue;
            var n = 0;
            var lastCounted = 0;
            var off = 0;
            while (n < byte.MaxValue && p + ((n + 1) * stride) <= rom.Length && rom[p + (n * stride) + 5] is > 0 and <= MaxMovePp)
            {
                if (rom[p + (n * stride)] == n + 1)
                {
                    lastCounted = n + 1;
                }
                else if (++off > Math.Max(MinRenumbered, (int)((n + 1) * MaxRenumberedShare)))
                {
                    break;
                }
                n++;
            }
            if (lastCounted < need)
                continue;
            var renumbered = 0;
            for (var i = 0; i < lastCounted; i++)
            {
                if (rom[p + (i * stride)] != i + 1)
                    renumbered++;
            }
            runs.Add(new GbRowRun(p, stride, lastCounted, renumbered));
        }
        return runs;
    }

    public static bool MoveShape(ReadOnlySpan<byte> rom, GbRowRun run, int gen)
    {
        ArgumentNullException.ThrowIfNull(run);
        var typed = 0;
        var five = 0;
        for (var i = 0; i < run.Count; i++)
        {
            var at = run.Offset + (i * run.Stride);
            if (GbType(gen, rom[at + 3]) is not null || (gen == 2 && rom[at + 3] == CurseType))
                typed++;
            if (rom[at + 2] % 5 == 0)
                five++;
        }
        return typed >= MoveAnchors.ShapeShare * run.Count && five >= MoveAnchors.PowerFiveShare * run.Count;
    }

    public const byte CurseType = 0x13;

    public static int? GbType(int gen, int raw) => raw switch
    {
        >= 0 and <= 5 => raw,
        7 => 6,
        8 => 7,
        9 when gen == 2 => 8,
        >= 0x14 and <= 0x1A => raw - 0x14 + 9,
        0x1B when gen == 2 => 16,
        _ => null,
    };

    public static GbBaseRows? BaseRows(ReadOnlySpan<byte> rom, int stride, int statCount)
    {
        GbBaseRows? best = null;
        if (stride <= statCount)
            return null;
        for (var p = 0; p + (stride * MinBaseRows) <= rom.Length; p++)
        {
            if (rom[p] != 1 || rom[p + stride] != 2)
                continue;
            var n = 0;
            while (n < MaxSlots && p + ((n + 1) * stride) <= rom.Length && rom[p + (n * stride)] == n + 1 && Plausible(rom.Slice(p + (n * stride) + 1, statCount)))
                n++;
            if (n >= MinBaseRows && (best is null || n > best.Count))
                best = new GbBaseRows(p, stride, n);
        }
        return best;
    }

    public static bool Plausible(ReadOnlySpan<byte> stats)
    {
        var sum = 0;
        foreach (var s in stats)
        {
            if (s == 0)
                return false;
            sum += s;
        }
        return sum >= 15 * stats.Length;
    }

    public const int MaxMapCandidates = 8;

    public static GbIndexMap? IndexMap(ReadOnlySpan<byte> rom, int baseRows, int maxLength = MaxSlots) =>
        IndexMaps(rom, baseRows, maxLength) is [var first, ..] ? first : null;

    public static List<GbIndexMap> IndexMaps(ReadOnlySpan<byte> rom, int baseRows, int maxLength = MaxSlots)
    {
        var found = new List<GbIndexMap>();
        if (baseRows < MinBaseRows)
            return found;
        var max = baseRows + 1;
        var last = new int[256];
        Array.Fill(last, -1);
        var left = 0;
        var distinct = 0;
        var bestDistinct = 0;
        var windows = new List<(int Left, int Right)>();
        for (var r = 0; r < rom.Length; r++)
        {
            var v = rom[r];
            if (v > max)
            {
                left = r + 1;
                distinct = 0;
                continue;
            }
            if (v != 0)
            {
                if (last[v] >= left)
                {
                    for (var k = left; k <= last[v]; k++)
                    {
                        if (rom[k] != 0)
                            distinct--;
                    }
                    left = last[v] + 1;
                }
                last[v] = r;
                distinct++;
            }
            while (r - left + 1 > maxLength)
            {
                if (rom[left] != 0)
                    distinct--;
                left++;
            }
            if (distinct > bestDistinct)
            {
                bestDistinct = distinct;
                windows.Clear();
                windows.Add((left, r));
            }
            else if (distinct == bestDistinct && distinct > 0 && windows.Count < MaxMapCandidates && windows[^1].Left != left)
            {
                windows.Add((left, r));
            }
        }
        if (bestDistinct < MinCover * baseRows)
            return found;
        foreach (var (windowLeft, windowRight) in windows)
        {
            var from = windowLeft;
            var to = windowRight;
            while (from < to && rom[from] == 0)
                from++;
            while (to > from && rom[to] == 0)
                to--;
            if (found.Exists(m => m.Offset == from && m.Length == to - from + 1))
                continue;
            var dex = new List<int>(to - from + 1);
            for (var i = from; i <= to; i++)
                dex.Add(rom[i]);
            found.Add(new GbIndexMap(from, dex, bestDistinct));
        }
        return found;
    }
}
