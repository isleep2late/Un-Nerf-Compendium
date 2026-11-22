using System;
using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Analysis;

public enum LearnsetFormat
{
    Packed,
    Wide,
    Triple,
}

public sealed record LearnsetTable(int Offset, int Count, LearnsetFormat Format, int? FieldOffset = null)
{
    public int References { get; init; }

    public int Run { get; init; }

    public LearnsetTable? Passed { get; init; }
}

public static class Gen3Learnsets
{
    public const int MaxMoves = 64;
    public const int MaxLevel = 100;
    public const int MinRun = 16;
    public const int TripleEndLevel = 0xFF;
    public const int StartWindow = 4;
    public const int ReferencedShareOfSpecies = 4;

    private static readonly LearnsetFormat[] TableFormats = [LearnsetFormat.Packed, LearnsetFormat.Wide, LearnsetFormat.Triple];

    public static int EntrySize(LearnsetFormat format) => format switch
    {
        LearnsetFormat.Packed => 2,
        LearnsetFormat.Triple => 3,
        _ => 4,
    };

    public static List<HackLearn>? Decode(GbaImage rom, int at, LearnsetFormat format, int moveCount)
    {
        var list = new List<HackLearn>();
        var size = EntrySize(format);
        for (var i = 0; i <= MaxMoves; i++)
        {
            var p = at + i * size;
            if (!rom.Has(p, size))
                return null;
            int move, level;
            if (format == LearnsetFormat.Packed)
            {
                var v = rom.U16(p);
                if (v == 0xFFFF)
                    return list;
                move = v & 0x1FF;
                level = v >> 9;
            }
            else if (format == LearnsetFormat.Triple)
            {
                move = rom.U16(p);
                level = rom.U8(p + 2);
                if (move == 0 && level == TripleEndLevel)
                    return list;
            }
            else
            {
                move = rom.U16(p);
                if (move == 0xFFFF)
                    return list;
                level = rom.U16(p + 2);
            }
            if (move == 0 || move >= moveCount || level is < 0 or > MaxLevel)
                return null;
            list.Add(new HackLearn(level, move));
        }
        return null;
    }

    private sealed record Run(int Start, int Length, LearnsetFormat Format, int Lists);

    public static LearnsetTable? FindTable(GbaImage rom, int speciesCount, int moveCount)
    {
        ArgumentNullException.ThrowIfNull(rom);
        var runs = new List<Run>();
        foreach (var format in TableFormats)
        {
            var valid = new Dictionary<int, int>();
            var lists = new HashSet<int>();
            var runStart = -1;
            var run = 0;
            for (var at = 0; at + 4 <= rom.Length; at += 4)
            {
                var length = -1;
                if (rom.Offset(rom.U32(at)) is { } target && (format == LearnsetFormat.Triple || target % 2 == 0))
                {
                    if (!valid.TryGetValue(target, out length))
                        valid[target] = length = Decode(rom, target, format, moveCount)?.Count ?? -1;
                    if (length > 0)
                        lists.Add(target);
                }
                if (length >= 0)
                {
                    if (run == 0)
                        runStart = at;
                    run++;
                    continue;
                }
                Close();
            }
            Close();

            void Close()
            {
                if (run >= MinRun)
                    runs.Add(new Run(runStart, run, format, lists.Count));
                run = 0;
                lists.Clear();
            }
        }
        if (runs.Count == 0)
            return null;

        var longest = runs.Where(r => r.Format != LearnsetFormat.Triple || r.Lists >= MinRun).OrderByDescending(r => r.Length).FirstOrDefault();
        var starts = runs.Where(r => r.Lists >= MinRun).SelectMany(r => Enumerable.Range(0, Math.Min(StartWindow, r.Length)).Select(k => r.Start + k * 4)).ToHashSet();
        if (longest is not null)
            starts.Add(longest.Start);
        var refs = FrTableFollower.References(rom, starts);
        var need = Math.Max(MinRun, speciesCount / ReferencedShareOfSpecies);
        var referenced = runs
            .Where(r => r.Lists >= MinRun)
            .Select(r =>
            {
                var skip = Enumerable.Range(0, Math.Min(StartWindow, r.Length)).OrderByDescending(k => refs.GetValueOrDefault(r.Start + k * 4)).ThenBy(k => k).First();
                var start = r.Start + skip * 4;
                return (Start: start, Length: r.Length - skip, r.Format, References: refs.GetValueOrDefault(start));
            })
            .Where(c => c.References > 0 && c.Length >= need)
            .OrderByDescending(c => c.Length)
            .ThenByDescending(c => c.References)
            .FirstOrDefault();

        LearnsetTable? best;
        if (referenced.References > 0)
        {
            var passed = longest is not null && (longest.Start > referenced.Start || longest.Start + longest.Length * 4 <= referenced.Start) ? new LearnsetTable(longest.Start, longest.Length, longest.Format) { Run = longest.Length, References = refs.GetValueOrDefault(longest.Start) } : null;
            best = new LearnsetTable(referenced.Start, referenced.Length, referenced.Format) { References = referenced.References, Run = referenced.Length, Passed = passed };
        }
        else
        {
            best = longest is null ? null : new LearnsetTable(longest.Start, longest.Length, longest.Format) { Run = longest.Length };
        }
        return best is not null && best.Count >= Math.Min(speciesCount, MinRun) ? best with { Count = Math.Min(best.Count, speciesCount) } : null;
    }

    public static string? Why(LearnsetTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.References == 0)
            return null;
        var passed = table.Passed is { } p ? $"; the {(p.Run > table.Run ? "longer " : "")}run of {p.Run} {p.Format.ToString().ToLowerInvariant()} list pointers at 0x{p.Offset:X}, {(p.References == 0 ? "which no ROM word outside the GF header points at" : $"which {p.References} ROM word{(p.References == 1 ? "" : "s")} outside the GF header also point{(p.References == 1 ? "s" : "")} at")}, is not used" : "";
        return $"the level-up table is the one the game code loads: {table.References} ROM word{(table.References == 1 ? "" : "s")} outside the GF header point at 0x{table.Offset:X}{passed}";
    }

    public static LearnsetTable? FindField(GbaImage rom, int speciesInfo, int stride, int speciesCount, int moveCount)
    {
        for (var field = 0; field + 4 <= stride; field += 4)
        {
            foreach (var format in new[] { LearnsetFormat.Wide, LearnsetFormat.Packed })
            {
                var ok = true;
                for (var i = 1; i <= 3 && ok; i++)
                    ok = rom.PointerAt(speciesInfo + i * stride + field) is { } t && Decode(rom, t, format, moveCount) is not null;
                if (ok)
                    return new LearnsetTable(speciesInfo, speciesCount, format, field);
            }
        }
        return null;
    }

    public static Dictionary<int, IReadOnlyList<HackLearn>> Read(GbaImage rom, LearnsetTable table, int stride, int moveCount)
    {
        var result = new Dictionary<int, IReadOnlyList<HackLearn>>();
        for (var id = 0; id < table.Count; id++)
        {
            var slot = table.FieldOffset is { } f ? table.Offset + id * stride + f : table.Offset + id * 4;
            if (rom.PointerAt(slot) is { } t && Decode(rom, t, table.Format, moveCount) is { } list)
                result[id] = list;
        }
        return result;
    }
}
