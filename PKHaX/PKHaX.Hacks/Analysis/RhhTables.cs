using System;
using System.Collections.Generic;

namespace PKHaX.Hacks.Analysis;

public sealed record NamedLayout(int Stride, int NameOffset, bool Pointer, int NameLength);

public static class RhhTables
{
    public const int SpeciesNameLength = 13;
    public const int MinStride = 8;
    public const int MaxStride = 512;
    public const int DisabledTail = 64;
    public const int MaxSpeciesNameOffset = 160;

    public static readonly string[] SpeciesProbe = ["Bulbasaur", "Ivysaur", "Venusaur"];
    public static readonly string[] MoveProbe = ["Pound", "Karate Chop", "Double Slap"];
    public static readonly string[] ItemProbe = ["Master Ball", "Ultra Ball"];
    public static readonly string[] AbilityProbe = ["Stench", "Drizzle", "Speed Boost"];
    public const int ItemProbeWindow = 8;

    private static string? NameAt(GbaImage rom, int at, bool pointer, int length)
    {
        if (pointer)
        {
            if (rom.PointerAt(at) is not { } target)
                return null;
            at = target;
        }
        if (!rom.Has(at, 1) || rom.U8(at) == 0x00)
            return null;
        return Gen3Tables.Name(rom, at, length);
    }

    private static bool Same(string? a, string b) => a is not null && IdMapper.Normalise(a) == IdMapper.Normalise(b);

    public static NamedLayout? Probe(GbaImage rom, int table, IReadOnlyList<string> expected, int[] inlineLengths, int maxNameOffset = 64)
    {
        for (var stride = MinStride; stride <= MaxStride; stride++)
        {
            if (!rom.Has(table + expected.Count * stride, 4))
                break;
            for (var offset = 0; offset <= Math.Min(maxNameOffset, stride - 1); offset++)
            {
                foreach (var length in inlineLengths)
                {
                    if (offset + length <= stride && Matches(rom, table, stride, offset, false, length, expected))
                        return new NamedLayout(stride, offset, false, length);
                }
                if (offset % 4 == 0 && offset + 4 <= stride && Matches(rom, table, stride, offset, true, 32, expected))
                    return new NamedLayout(stride, offset, true, 32);
            }
        }
        return null;
    }

    public static NamedLayout? ProbeAmong(GbaImage rom, int table, IReadOnlyList<string> expected, int[] inlineLengths, int window, int maxNameOffset = 64)
    {
        for (var stride = MinStride; stride <= MaxStride; stride++)
        {
            if (!rom.Has(table + (window + 1) * stride, 4))
                break;
            for (var offset = 0; offset <= Math.Min(maxNameOffset, stride - 1); offset++)
            {
                foreach (var length in inlineLengths)
                {
                    if (offset + length <= stride && AllAmong(rom, table, stride, offset, false, length, expected, window))
                        return new NamedLayout(stride, offset, false, length);
                }
                if (offset % 4 == 0 && offset + 4 <= stride && AllAmong(rom, table, stride, offset, true, 32, expected, window))
                    return new NamedLayout(stride, offset, true, 32);
            }
        }
        return null;
    }

    private static bool AllAmong(GbaImage rom, int table, int stride, int offset, bool pointer, int length, IReadOnlyList<string> expected, int window)
    {
        if (NameAt(rom, table + stride + offset, pointer, length) is null)
            return false;
        var found = 0;
        var seen = new bool[expected.Count];
        for (var i = 1; i <= window && found < expected.Count; i++)
        {
            var name = NameAt(rom, table + i * stride + offset, pointer, length);
            if (name is null)
                return false;
            for (var e = 0; e < expected.Count; e++)
            {
                if (!seen[e] && Same(name, expected[e]))
                {
                    seen[e] = true;
                    found++;
                }
            }
        }
        return found == expected.Count;
    }

    private static bool Matches(GbaImage rom, int table, int stride, int offset, bool pointer, int length, IReadOnlyList<string> expected)
    {
        for (var i = 0; i < expected.Count; i++)
        {
            if (!Same(NameAt(rom, table + (i + 1) * stride + offset, pointer, length), expected[i]))
                return false;
        }
        return true;
    }

    public static List<string> Names(GbaImage rom, int table, NamedLayout layout, int count)
    {
        var names = new List<string>(count);
        for (var i = 0; i < count; i++)
            names.Add(NameAt(rom, table + i * layout.Stride + layout.NameOffset, layout.Pointer, layout.NameLength) ?? "");
        return names;
    }

    public static (int Plausible, int Named) PlausibleNamed(GbaImage rom, int table, NamedLayout layout, int rows)
    {
        var plausible = 0;
        var named = 0;
        var names = Names(rom, table, layout, rows);
        for (var i = 1; i < names.Count; i++)
        {
            if (names[i].Length == 0)
                continue;
            named++;
            var b = table + (i * layout.Stride);
            if (rom.Has(b, 6) && Gen3LayoutFinder.PlausibleStats(rom.Slice(b, 6)))
                plausible++;
        }
        return (plausible, named);
    }

    public static BitField NameBits(NamedLayout layout) => new(layout.NameOffset * 8, (layout.Pointer ? 4 : layout.NameLength) * 8);

    public static bool HasBulbasaurStats(GbaImage rom, int table, int stride)
    {
        var b = table + stride;
        return rom.Has(b, 6) && rom.U8(b) == 45 && rom.U8(b + 1) == 49 && rom.U8(b + 2) == 49 && rom.U8(b + 3) == 45 && rom.U8(b + 4) == 65 && rom.U8(b + 5) == 65;
    }
}
