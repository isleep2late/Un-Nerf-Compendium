using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public sealed record TableCandidate(int Table, int Stride, bool Pointer = false, int NameLength = 0);

public sealed record TableHit(string Table, int Offset, int Stride, int References, bool FromHeader, bool Pointer = false, int NameLength = 0);

public static class FrTableFollower
{
    public static readonly string[] SpeciesAnchor = ["BULBASAUR", "IVYSAUR"];
    public static readonly string[] MoveAnchor = ["POUND", "KARATE CHOP"];
    public static readonly int[] MoveNameStrides = [Gen3Tables.MoveNameStride, 17];
    public static readonly string[] AbilityAnchor = ["STENCH", "DRIZZLE"];
    public static readonly int[] SpeciesNameStrides = [Gen3Tables.SpeciesNameStride, 13];
    public static readonly int[] AbilityNameStrides = [Gen3Tables.AbilityNameStride, 17];
    public static readonly int[] ItemNameLengths = [Gen3Tables.ItemNameLength, 17, 20];
    public const int MinInfoStride = 24;
    public const int MaxInfoStride = 256;
    public const int MinItemStride = 32;
    public const int MaxItemStride = 64;
    public const int PointerNameLength = 32;
    public const int MaxHits = 64;

    private static readonly byte[] BulbasaurStats = [45, 49, 49, 45, 65, 65];
    private static readonly byte[] IvysaurStats = [60, 62, 63, 60, 80, 80];
    private static readonly byte[] PoundMove = [0, 40, 0, 100, 35];

    public static byte[] Encode(string text)
    {
        var buffer = new byte[text.Length + 1];
        var n = StringConverter3.SetString(buffer, text, text.Length, (int)LanguageID.English);
        return buffer.AsSpan(0, n).ToArray();
    }

    private static string Title(string upper) => string.Join(" ", upper.Split(' ').Select(w => w.Length == 0 ? w : w[0] + w[1..].ToLowerInvariant()));

    public static List<int> Hits(GbaImage rom, ReadOnlySpan<byte> needle)
    {
        var hits = new List<int>();
        var span = rom.Span;
        var from = 0;
        while (from < span.Length && hits.Count < MaxHits)
        {
            var at = span[from..].IndexOf(needle);
            if (at < 0)
                break;
            hits.Add(from + at);
            from += at + 1;
        }
        return hits;
    }

    public static Dictionary<int, int> References(GbaImage rom, IReadOnlyCollection<int> targets)
    {
        var counts = targets.Distinct().ToDictionary(t => t, _ => 0);
        if (counts.Count == 0)
            return counts;
        var end = GbaImage.RomBase + (uint)rom.Length;
        for (var at = 0; at + 4 <= rom.Length; at += 4)
        {
            if (at is >= GfRomHeader.At and < GfRomHeader.At + GfRomHeader.Size + 4)
                continue;
            var v = rom.U32(at);
            if (v < GbaImage.RomBase || v >= end)
                continue;
            var off = (int)(v - GbaImage.RomBase);
            if (counts.TryGetValue(off, out var c))
                counts[off] = c + 1;
        }
        return counts;
    }

    private static bool NameIs(GbaImage rom, int at, int stride, string expected) =>
        Gen3Tables.Name(rom, at, stride) is { } n && rom.U8(at) != 0x00 && IdMapper.Normalise(n) == IdMapper.Normalise(expected);

    public static bool PointerNameIs(GbaImage rom, int slot, string expected) =>
        rom.PointerAt(slot) is { } at && NameIs(rom, at, Math.Min(PointerNameLength, rom.Length - at), expected);

    public static string? PointerName(GbaImage rom, int slot) =>
        rom.PointerAt(slot) is { } at && rom.U8(at) != 0x00 ? Gen3Tables.Name(rom, at, Math.Min(PointerNameLength, rom.Length - at)) : null;

    public static List<string> PointerNames(GbaImage rom, int table, int max)
    {
        var names = new List<string>();
        for (var i = 0; i < Math.Min(max, Gen3Tables.MaxEntries) && rom.Has(table + i * 4, 4); i++)
        {
            var name = PointerName(rom, table + i * 4);
            if (name is null && i > 0)
                break;
            names.Add(name ?? "");
        }
        return names;
    }

    public static List<TableCandidate> NameTables(GbaImage rom, IReadOnlyList<string> anchor, IEnumerable<int> strides)
    {
        var found = new List<TableCandidate>();
        var strings = new List<int>();
        foreach (var casing in new[] { anchor[0], Title(anchor[0]) }.Distinct())
            strings.AddRange(Hits(rom, Encode(casing)).Where(h => h == 0 || rom.U8(h - 1) is 0xFF or 0x00));
        foreach (var stride in strides)
        {
            foreach (var hit in strings)
            {
                var table = hit - stride;
                if (table < 0 || !NameIs(rom, hit, stride, anchor[0]))
                    continue;
                var ok = true;
                for (var i = 1; i < anchor.Count && ok; i++)
                    ok = NameIs(rom, hit + i * stride, stride, anchor[i]);
                var c = new TableCandidate(table, stride);
                if (ok && !found.Contains(c))
                    found.Add(c);
            }
        }
        var targets = strings.ToHashSet();
        var end = GbaImage.RomBase + (uint)rom.Length;
        for (var at = 4; at + 4 * anchor.Count <= rom.Length && targets.Count > 0; at += 4)
        {
            var v = rom.U32(at);
            if (v < GbaImage.RomBase || v >= end || !targets.Contains((int)(v - GbaImage.RomBase)))
                continue;
            var ok = true;
            for (var i = 1; i < anchor.Count && ok; i++)
                ok = PointerNameIs(rom, at + i * 4, anchor[i]);
            var c = new TableCandidate(at - 4, 4, true, PointerNameLength);
            if (ok && !found.Contains(c) && found.Count < MaxHits)
                found.Add(c);
        }
        return found;
    }

    public static List<TableCandidate> SpeciesInfoTables(GbaImage rom)
    {
        var found = new List<TableCandidate>();
        foreach (var hit in Hits(rom, BulbasaurStats))
        {
            foreach (var stride in Gen3Tables.SpeciesInfoStrides.Concat(Enumerable.Range(MinInfoStride, MaxInfoStride - MinInfoStride + 1)).Distinct())
            {
                if (hit - stride >= 0 && rom.Has(hit + stride, IvysaurStats.Length) && rom.Slice(hit + stride, IvysaurStats.Length).SequenceEqual(IvysaurStats))
                {
                    found.Add(new TableCandidate(hit - stride, stride));
                    break;
                }
            }
        }
        return found;
    }

    public static List<TableCandidate> MoveTables(GbaImage rom)
    {
        var found = new List<TableCandidate>();
        foreach (var hit in Hits(rom, PoundMove))
        {
            foreach (var stride in Gen3Tables.MoveStrides)
            {
                if (hit - stride >= 0 && rom.Has(hit + stride, 5) && rom.U8(hit + stride + 1) is > 0 and <= 250 && rom.U8(hit + stride + 4) is > 0 and <= 64)
                    found.Add(new TableCandidate(hit - stride, stride));
            }
        }
        return found;
    }

    public static List<TableCandidate> ItemTables(GbaImage rom)
    {
        var found = new List<TableCandidate>();
        foreach (var casing in new[] { "MASTER BALL", "Master Ball" })
        {
            foreach (var hit in Hits(rom, Encode(casing)))
            {
                if (hit > 0 && rom.U8(hit - 1) is not (0xFF or 0x00))
                    continue;
                foreach (var length in ItemNameLengths)
                {
                    if (!NameIs(rom, hit, length, "MASTER BALL"))
                        continue;
                    var vanilla = length == Gen3Tables.ItemNameLength && rom.Has(hit, 0x10) && rom.U16(hit + 0x0E) == 1;
                    foreach (var stride in Gen3Tables.ItemStrides.Concat(Enumerable.Range(MinItemStride, MaxItemStride - MinItemStride + 1)).Distinct())
                    {
                        if (stride < length || hit - stride < 0 || (stride == Gen3Tables.ItemStrides[0] && length == Gen3Tables.ItemNameLength && !vanilla))
                            continue;
                        if (NameIs(rom, hit + stride, length, "ULTRA BALL") && NameIs(rom, hit + 2 * stride, length, "GREAT BALL"))
                        {
                            found.Add(new TableCandidate(hit - stride, stride, false, vanilla && stride == Gen3Tables.ItemStrides[0] ? length : Math.Min(stride, ItemNameLengths.Max())));
                            break;
                        }
                    }
                }
            }
        }
        return found;
    }

    public static TableHit? Choose(GbaImage rom, string table, IReadOnlyList<TableCandidate> candidates, int? headerPointer)
    {
        var all = candidates.Distinct().ToList();
        if (all.Count == 0)
            return null;
        var refs = References(rom, all.Select(c => c.Table).ToList());
        var best = all
            .OrderByDescending(c => refs[c.Table])
            .ThenByDescending(c => c.Table == headerPointer)
            .ThenBy(c => c.Pointer)
            .ThenBy(c => c.Table)
            .First();
        return new TableHit(table, best.Table, best.Stride, refs[best.Table], best.Table == headerPointer, best.Pointer, best.NameLength);
    }
}
