using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Saves.Gen3;

public sealed class GbaSectorContainer
{
    public const int FileSize = 0x20000;
    public const int SlotSectors = SectionLayout.SectionCount;
    public const int IdOffset = 0xFF4;
    public const int ChecksumOffset = 0xFF6;
    public const int SignatureOffset = 0xFF8;
    public const int CounterOffset = 0xFFC;
    public const int RawPayload = 0xFF0;
    public const uint RetailSignature = 0x08012025;
    public const int FooterAllowance = 0x40;
    public const int ScannedSectors = 30;

    public static ReadOnlyMemory<byte> TrimFooter(ReadOnlyMemory<byte> file) =>
        file.Length is > FileSize and <= FileSize + FooterAllowance ? file[..FileSize] : file;

    public static int FooterLength(ReadOnlyMemory<byte> file) =>
        file.Length is > FileSize and <= FileSize + FooterAllowance ? file.Length - FileSize : 0;

    public static uint? UniformSignature(ReadOnlySpan<byte> file) => NewestGroup(file, null, true) is { Ids: >= SlotSectors } group ? group.Signature : null;

    public static int? NewestSectionIds(ReadOnlySpan<byte> file, IReadOnlyCollection<uint> signatures) => NewestGroup(file, signatures, true)?.Ids;

    private static (uint Signature, int Ids)? NewestGroup(ReadOnlySpan<byte> file, IReadOnlyCollection<uint>? signatures, bool completeFirst)
    {
        if (file.Length < FileSize)
            return null;
        var groups = new Dictionary<(uint Signature, uint Counter), HashSet<int>>();
        for (var sector = 0; sector < ScannedSectors; sector++)
        {
            var data = file.Slice(sector * SectionLayout.SectorSize, SectionLayout.SectorSize);
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(data[SignatureOffset..]);
            if (signature is 0 or uint.MaxValue || (signatures is not null && !signatures.Contains(signature)))
                continue;
            var key = (signature, BinaryPrimitives.ReadUInt32LittleEndian(data[CounterOffset..]));
            if (!groups.TryGetValue(key, out var ids))
                groups[key] = ids = [];
            ids.Add(BinaryPrimitives.ReadUInt16LittleEndian(data[IdOffset..]));
        }
        if (groups.Count == 0)
            return null;
        var pool = groups.ToList();
        if (completeFirst && pool.Where(g => g.Value.Count >= SlotSectors).ToList() is { Count: > 0 } full)
            pool = full;
        var best = pool[0];
        foreach (var g in pool.Skip(1))
        {
            var a = best.Key.Counter;
            var b = g.Key.Counter;
            var newer = (a == uint.MaxValue && b == 0) || (!(a == 0 && b == uint.MaxValue) && b > a);
            if (newer || (b == a && g.Value.Count > best.Value.Count))
                best = g;
        }
        return (best.Key.Signature, best.Value.Count);
    }

    private readonly int[] sectorOf;

    private GbaSectorContainer(ReadOnlyMemory<byte> file, SectionLayout layout, ChecksumPolicy policy, int slot, uint counter, uint signature, int[] sectorOf)
    {
        File = file;
        Layout = layout;
        Policy = policy;
        ActiveSlot = slot;
        Counter = counter;
        Signature = signature;
        this.sectorOf = sectorOf;
    }

    public ReadOnlyMemory<byte> File { get; }
    public SectionLayout Layout { get; }
    public ChecksumPolicy Policy { get; }
    public int ActiveSlot { get; }
    public uint Counter { get; }
    public uint Signature { get; }

    public int SectorOf(int id) => sectorOf[id];

    public int SectorOffset(int id) => sectorOf[id] * SectionLayout.SectorSize;

    public ReadOnlySpan<byte> Sector(int id) => File.Span.Slice(SectorOffset(id), SectionLayout.SectorSize);

    public ReadOnlySpan<byte> Section(int id) => Sector(id)[..Layout.Slots[id].Size];

    public ReadOnlySpan<byte> RawSector(int sector) => File.Span.Slice(sector * SectionLayout.SectorSize, RawPayload);

    public byte[] Block(SaveBlock block)
    {
        var result = new byte[Layout.BlockSize(block)];
        foreach (var id in SectionLayout.IdsOf(block))
            Section(id).CopyTo(result.AsSpan(Layout.Slots[id].ToAdd));
        return result;
    }

    public bool TryLocate(SaveBlock block, int offset, out int fileOffset, out int id)
    {
        foreach (var candidate in SectionLayout.IdsOf(block))
        {
            var slot = Layout.Slots[candidate];
            if (offset >= slot.ToAdd && offset < slot.ToAdd + slot.Size)
            {
                id = candidate;
                fileOffset = SectorOffset(candidate) + (offset - slot.ToAdd);
                return true;
            }
        }
        id = -1;
        fileOffset = -1;
        return false;
    }

    public static GbaSectorContainer? TryOpen(ReadOnlyMemory<byte> file, SectionLayout layout, ChecksumPolicy policy, IReadOnlyCollection<uint> signatures, out string? why)
    {
        if (file.Length != FileSize)
        {
            why = $"a Gen 3 cartridge save is 0x{FileSize:X} bytes, this file is 0x{file.Length:X}";
            return null;
        }
        var span = file.Span;
        var slots = new SlotScan[2];
        for (var slot = 0; slot < 2; slot++)
            slots[slot] = Scan(span, slot, policy, signatures);
        var ok = slots.Where(s => s.Complete).ToList();
        if (ok.Count == 0)
        {
            why = slots.All(s => s.Signed == 0)
                ? $"no sector carries an accepted signature ({string.Join(", ", signatures.Select(x => $"0x{x:X8}"))})"
                : $"neither save slot has all {SlotSectors} sections with a valid checksum under {policy.Name} (slot A: {slots[0].Summary}; slot B: {slots[1].Summary})";
            return null;
        }
        var pick = ok.Count == 1 ? ok[0] : Newer(ok[0], ok[1]);
        why = null;
        return new GbaSectorContainer(file, layout, policy, pick.Slot, pick.Counter, pick.Signature, pick.SectorOf);
    }

    private static SlotScan Newer(SlotScan a, SlotScan b)
    {
        if ((a.Counter == uint.MaxValue && b.Counter == 0) || (a.Counter == 0 && b.Counter == uint.MaxValue))
            return unchecked(a.Counter + 1) < unchecked(b.Counter + 1) ? b : a;
        return a.Counter < b.Counter ? b : a;
    }

    private static SlotScan Scan(ReadOnlySpan<byte> span, int slot, ChecksumPolicy policy, IReadOnlyCollection<uint> signatures)
    {
        var sectorOf = Enumerable.Repeat(-1, SlotSectors).ToArray();
        var signed = 0;
        var valid = 0;
        uint counter = 0;
        uint signature = 0;
        var problems = new List<string>();
        for (var i = 0; i < SlotSectors; i++)
        {
            var sector = (slot * SlotSectors) + i;
            var data = span.Slice(sector * SectionLayout.SectorSize, SectionLayout.SectorSize);
            var sig = BinaryPrimitives.ReadUInt32LittleEndian(data[SignatureOffset..]);
            if (!signatures.Contains(sig))
                continue;
            signed++;
            var id = BinaryPrimitives.ReadUInt16LittleEndian(data[IdOffset..]);
            if (id >= SlotSectors)
            {
                problems.Add($"sector {sector} id {id}");
                continue;
            }
            if (sectorOf[id] >= 0)
            {
                problems.Add($"id {id} twice");
                continue;
            }
            if (BinaryPrimitives.ReadUInt16LittleEndian(data[ChecksumOffset..]) != policy.Compute(id, data))
            {
                problems.Add($"id {id} checksum");
                continue;
            }
            sectorOf[id] = sector;
            valid++;
            counter = BinaryPrimitives.ReadUInt32LittleEndian(data[CounterOffset..]);
            signature = sig;
        }
        var summary = $"{signed} signed, {valid} valid{(problems.Count > 0 ? ", " + string.Join(", ", problems.Take(3)) : "")}";
        return new SlotScan(slot, valid == SlotSectors, signed, counter, signature, sectorOf, summary);
    }

    private sealed record SlotScan(int Slot, bool Complete, int Signed, uint Counter, uint Signature, int[] SectorOf, string Summary);
}
