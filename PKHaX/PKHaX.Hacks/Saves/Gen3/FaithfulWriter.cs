using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Saves.Gen3;

public sealed record BlockEdit(SaveBlock Block, int Offset, byte[] Bytes);

public readonly record struct BlockWindow(SaveBlock Block, int Offset, int Length);

public sealed record RawEdit(int Sector, int Offset, byte[] Bytes);

public readonly record struct ByteRange(int Start, int Length)
{
    public int End => Start + Length;

    public bool Contains(int offset) => offset >= Start && offset < End;

    public override string ToString() => $"0x{Start:X5}+{Length}";
}

public sealed record WriteResult(byte[] Bytes, IReadOnlyList<int> TouchedSections, IReadOnlyList<ByteRange> DiffRanges, IReadOnlyList<ByteRange> Allowed, IReadOnlyList<int>? RawSectors = null)
{
    public IReadOnlyList<int> TouchedRawSectors => RawSectors ?? [];

    public IReadOnlyList<ByteRange> Outside { get; } = Uncovered(DiffRanges, Allowed);

    public bool Confined => Outside.Count == 0;

    private static IReadOnlyList<ByteRange> Uncovered(IReadOnlyList<ByteRange> diff, IReadOnlyList<ByteRange> allowed)
    {
        var merged = new List<ByteRange>();
        foreach (var a in allowed.Where(a => a.Length > 0).OrderBy(a => a.Start))
        {
            if (merged.Count > 0 && a.Start <= merged[^1].End)
                merged[^1] = new ByteRange(merged[^1].Start, Math.Max(merged[^1].End, a.End) - merged[^1].Start);
            else
                merged.Add(a);
        }
        var result = new List<ByteRange>();
        foreach (var d in diff)
        {
            var at = d.Start;
            foreach (var m in merged)
            {
                if (m.End <= at || m.Start >= d.End)
                    continue;
                if (m.Start > at)
                    result.Add(new ByteRange(at, m.Start - at));
                at = Math.Max(at, m.End);
                if (at >= d.End)
                    break;
            }
            if (at < d.End)
                result.Add(new ByteRange(at, d.End - at));
        }
        return result;
    }
}

public sealed class FaithfulWriteException(string message) : Exception(message);

public static class FaithfulWriter
{
    public static IReadOnlyList<int> UnchecksummedSectors { get; } = [30, 31];

    public static WriteResult Patch(GbaSectorContainer container, IReadOnlyList<BlockEdit> edits, IReadOnlyList<BlockWindow> intended) => Patch(container, edits, intended, []);

    public static WriteResult Patch(GbaSectorContainer container, IReadOnlyList<BlockEdit> edits, IReadOnlyList<BlockWindow> intended, IReadOnlyList<RawEdit> raw)
    {
        var original = container.File.Span;
        var bytes = original.ToArray();
        var touched = new SortedSet<int>();
        var allowed = new List<ByteRange>();
        var rawSectors = new SortedSet<int>();
        foreach (var edit in edits)
        {
            var size = container.Layout.BlockSize(edit.Block);
            if (edit.Offset < 0 || edit.Offset + edit.Bytes.Length > size)
                throw new FaithfulWriteException($"an edit at {edit.Block} 0x{edit.Offset:X}+{edit.Bytes.Length} lies outside the block's 0x{size:X} bytes");
            for (var i = 0; i < edit.Bytes.Length; i++)
            {
                if (!container.TryLocate(edit.Block, edit.Offset + i, out var fileOffset, out var id))
                    throw new FaithfulWriteException($"{edit.Block} offset 0x{edit.Offset + i:X} maps to no section payload");
                bytes[fileOffset] = edit.Bytes[i];
                touched.Add(id);
            }
        }
        foreach (var edit in raw)
        {
            if (!UnchecksummedSectors.Contains(edit.Sector))
                throw new FaithfulWriteException($"a raw edit in sector {edit.Sector} is refused: only sectors {string.Join(" and ", UnchecksummedSectors)} are written raw; sectors 0-{(2 * GbaSectorContainer.SlotSectors) - 1} hold the save slots' checksummed sections and 28-29 the checksummed Hall of Fame");
            if (edit.Offset < 0 || edit.Offset + edit.Bytes.Length > GbaSectorContainer.RawPayload)
                throw new FaithfulWriteException($"a raw edit at sector {edit.Sector} 0x{edit.Offset:X}+{edit.Bytes.Length} lies outside the sector's 0x{GbaSectorContainer.RawPayload:X} payload bytes");
            var at = (edit.Sector * SectionLayout.SectorSize) + edit.Offset;
            edit.Bytes.CopyTo(bytes, at);
            allowed.Add(new ByteRange(at, edit.Bytes.Length));
            rawSectors.Add(edit.Sector);
        }
        foreach (var window in intended)
            AddRuns(allowed, window, container);
        foreach (var id in touched)
        {
            var at = container.SectorOffset(id);
            var sector = bytes.AsSpan(at, SectionLayout.SectorSize);
            if (sector.SequenceEqual(original.Slice(at, SectionLayout.SectorSize)))
                continue;
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.ChecksumOffset..], container.Policy.Compute(id, sector));
            allowed.Add(new ByteRange(at + GbaSectorContainer.ChecksumOffset, 2));
        }
        var diff = Diff(original, bytes);
        var changedSections = new List<int>();
        foreach (var id in touched)
        {
            var at = container.SectorOffset(id);
            if (!bytes.AsSpan(at, SectionLayout.SectorSize).SequenceEqual(original.Slice(at, SectionLayout.SectorSize)))
                changedSections.Add(id);
        }
        var changedRaw = new List<int>();
        foreach (var sector in rawSectors)
        {
            var at = sector * SectionLayout.SectorSize;
            if (!bytes.AsSpan(at, SectionLayout.SectorSize).SequenceEqual(original.Slice(at, SectionLayout.SectorSize)))
                changedRaw.Add(sector);
        }
        return new WriteResult(bytes, changedSections, diff, allowed, changedRaw);
    }

    private static void AddRuns(List<ByteRange> allowed, BlockWindow window, GbaSectorContainer container)
    {
        var start = -1;
        var last = -2;
        for (var i = 0; i < window.Length; i++)
        {
            if (!container.TryLocate(window.Block, window.Offset + i, out var fileOffset, out _))
                continue;
            if (fileOffset != last + 1)
            {
                if (start >= 0)
                    allowed.Add(new ByteRange(start, last - start + 1));
                start = fileOffset;
            }
            last = fileOffset;
        }
        if (start >= 0)
            allowed.Add(new ByteRange(start, last - start + 1));
    }

    public static IReadOnlyList<ByteRange> Diff(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        var result = new List<ByteRange>();
        var start = -1;
        for (var i = 0; i < before.Length; i++)
        {
            if (before[i] != after[i])
            {
                if (start < 0)
                    start = i;
            }
            else if (start >= 0)
            {
                result.Add(new ByteRange(start, i - start));
                start = -1;
            }
        }
        if (start >= 0)
            result.Add(new ByteRange(start, before.Length - start));
        return result;
    }
}
