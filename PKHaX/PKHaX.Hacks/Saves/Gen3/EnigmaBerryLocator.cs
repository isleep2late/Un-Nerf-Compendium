using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using PKHaX.Hacks.Analysis;

namespace PKHaX.Hacks.Saves.Gen3;

public sealed record EnigmaBerryPlace(int Offset, int Size, int Shift);

public sealed record EnigmaBerryHoldItem(int Index, string Name, byte HoldEffect, byte Parameter);

public sealed record EnigmaBerryRead(byte[] Record, int FileOffset, int Slot, int Section, int Stride, bool StrideAmbiguous);

public static class EnigmaBerryLocator
{
    public const int RecordSize = 0x34;
    public const int EnigmaBerryItem = 175;
    public const int ItemEntrySize = 44;
    public const int ItemIdOffset = 0x0E;
    public const int ItemHoldEffectOffset = 0x12;
    public const int ItemParameterOffset = 0x13;
    public const int ItemDescriptionOffset = 0x14;
    public const int FooterAllowance = GbaSectorContainer.FooterAllowance;
    public const int LargeSections = 4;
    public const int FirstLargeSection = 1;

    public const string EngineCaveat =
        "This game's ROM declares the record; whether its battle engine still reads it has not been checked.";

    public static IReadOnlyList<int> Strides { get; } = [SectionLayout.VanillaStride, SectionLayout.CfruStride];

    public static EnigmaBerryPlace? Locate(GbaImage rom, out GfRomHeader? header, out string? why)
    {
        header = GfRomHeader.Read(rom, out var headerWhy);
        if (header is null)
        {
            why = headerWhy ?? "the ROM has no GF header that reads as sane, so nothing says where the record is";
            return null;
        }
        return Locate(header, out why);
    }

    public static EnigmaBerryPlace? Locate(GfRomHeader? header, out string? why)
    {
        if (header is null)
        {
            why = "the ROM has no GF header that reads as sane, so nothing says where the record is";
            return null;
        }
        if (!header.SaneSizes)
        {
            why = $"the ROM's GF header does not read as a save layout (SaveBlock2 0x{header.SaveBlock2Size:X}, SaveBlock1 0x{header.SaveBlock1Size:X}), so its record offset is not trusted";
            return null;
        }
        var size = header.EnigmaBerrySize;
        var offset = header.EnigmaBerryOffset;
        if (size == 0)
        {
            why = "the ROM's GF header declares no e-Reader berry record (size 0)";
            return null;
        }
        if (size != RecordSize)
        {
            why = $"the ROM's GF header declares a record of 0x{size:X} bytes; only the 0x{RecordSize:X}-byte record is decoded";
            return null;
        }
        if ((ulong)offset + RecordSize > header.SaveBlock1Size)
        {
            why = $"the ROM's GF header puts the record at SaveBlock1 0x{offset:X}, which runs past the block's 0x{header.SaveBlock1Size:X} bytes";
            return null;
        }
        if (offset % 4 != 0)
        {
            why = $"the ROM's GF header puts the record at SaveBlock1 0x{offset:X}, which is not on a 4-byte boundary as a record holding pointers and a checksum word must be";
            return null;
        }
        why = null;
        return new EnigmaBerryPlace((int)offset, RecordSize, header.Shift);
    }

    public static uint? DescriptionPointer(GbaImage rom, GfRomHeader header, out string? why)
    {
        var count = Entries(rom, header, out var table, out why);
        if (count == 0)
            return null;
        if (count <= EnigmaBerryItem)
        {
            why = $"the item table reads as {count} retail entries, so it has no item {EnigmaBerryItem}";
            return null;
        }
        why = null;
        return rom.U32(table + (EnigmaBerryItem * ItemEntrySize) + ItemDescriptionOffset);
    }

    public static IReadOnlyList<EnigmaBerryHoldItem>? HoldItems(GbaImage rom, GfRomHeader header, out string? why)
    {
        var count = Entries(rom, header, out var table, out why);
        if (count == 0)
            return null;
        var items = new List<EnigmaBerryHoldItem>(count);
        for (var index = 1; index < count; index++)
        {
            var at = table + (index * ItemEntrySize);
            items.Add(new EnigmaBerryHoldItem(index, Gen3Tables.Name(rom, at, Gen3Tables.ItemNameLength) ?? "", rom.U8(at + ItemHoldEffectOffset), rom.U8(at + ItemParameterOffset)));
        }
        why = null;
        return items;
    }

    private static int Entries(GbaImage rom, GfRomHeader header, out int table, out string? why)
    {
        table = -1;
        if (header.Items is not { } start)
        {
            why = "the GF header's item pointer is not a ROM pointer";
            return 0;
        }
        if (Gen3Tables.ItemStride(rom, start) != ItemEntrySize)
        {
            why = $"the item table at ROM 0x{start:X} is not the retail {ItemEntrySize}-byte layout (its entry 1 is not MASTER BALL), so hold effects are not read from it";
            return 0;
        }
        table = start;
        var count = 1;
        while (count < Gen3Tables.MaxEntries && IsEntry(rom, start + (count * ItemEntrySize), count))
            count++;
        why = null;
        return count;
    }

    private static bool IsEntry(GbaImage rom, int at, int index)
    {
        if (!rom.Has(at, ItemEntrySize) || Gen3Tables.Name(rom, at, Gen3Tables.ItemNameLength) is null)
            return false;
        var id = rom.U16(at + ItemIdOffset);
        return (id == index || id == 0) && rom.PointerAt(at + ItemDescriptionOffset) is not null;
    }

    public static EnigmaBerryRead? Read(ReadOnlyMemory<byte> save, GfRomHeader header, EnigmaBerryPlace place, out string? why)
    {
        if (save.Length < GbaSectorContainer.FileSize || save.Length > GbaSectorContainer.FileSize + FooterAllowance)
        {
            why = $"a Gen 3 cartridge save is 0x{GbaSectorContainer.FileSize:X} bytes (at most 0x{FooterAllowance:X} more for a footer), this file is 0x{save.Length:X}";
            return null;
        }
        var file = save.Span[..GbaSectorContainer.FileSize];
        var slots = Slots(file);
        if (slots.Count == 0)
        {
            why = $"neither save slot holds each of the {SectionLayout.SectionCount} section ids once";
            return null;
        }

        var blockSize = (long)header.SaveBlock1Size;
        var reasons = new List<string>();
        EnigmaBerryRead? first = null;
        foreach (var stride in Strides)
        {
            var last = blockSize - ((LargeSections - 1) * (long)stride);
            if (last is <= 0 or > SectionLayout.PayloadCapacity)
            {
                reasons.Add($"sections of 0x{stride:X} bytes: a SaveBlock1 of 0x{blockSize:X} bytes does not fill {LargeSections} of them");
                continue;
            }
            var index = place.Offset / stride;
            var within = place.Offset - (index * stride);
            var room = index < LargeSections - 1 ? stride : (int)last;
            if (index >= LargeSections || within + place.Size > room)
            {
                reasons.Add($"sections of 0x{stride:X} bytes: the record at SaveBlock1 0x{place.Offset:X} would not lie inside one section");
                continue;
            }
            var found = false;
            foreach (var slot in slots)
            {
                if (!LargeChecksums(file, slot.SectorOf, stride, (int)last))
                    continue;
                var id = FirstLargeSection + index;
                var at = (slot.SectorOf[id] * SectionLayout.SectorSize) + within;
                found = true;
                if (first is null)
                    first = new EnigmaBerryRead(file.Slice(at, place.Size).ToArray(), at, slot.Slot, id, stride, false);
                else if (first.FileOffset != at)
                    first = first with { StrideAmbiguous = true };
                break;
            }
            if (!found)
                reasons.Add($"sections of 0x{stride:X} bytes: no complete slot has a matching checksum on all of sections {FirstLargeSection} to {FirstLargeSection + LargeSections - 1}"
                    + (stride > SectionLayout.VanillaStride ? $" together with data past 0x{SectionLayout.VanillaStride:X} in them" : ""));
        }

        if (first is null)
        {
            why = string.Join("; ", reasons);
            return null;
        }
        why = null;
        return first;
    }

    private sealed record SlotMap(int Slot, uint Counter, int[] SectorOf);

    private static List<SlotMap> Slots(ReadOnlySpan<byte> file)
    {
        var found = new List<SlotMap>();
        for (var slot = 0; slot < 2; slot++)
        {
            var sectorOf = new int[SectionLayout.SectionCount];
            Array.Fill(sectorOf, -1);
            var complete = true;
            var seen = 0;
            uint counter = 0;
            for (var i = 0; i < GbaSectorContainer.SlotSectors; i++)
            {
                var sector = (slot * GbaSectorContainer.SlotSectors) + i;
                var data = file.Slice(sector * SectionLayout.SectorSize, SectionLayout.SectorSize);
                var signature = BinaryPrimitives.ReadUInt32LittleEndian(data[GbaSectorContainer.SignatureOffset..]);
                if (signature is 0 or uint.MaxValue)
                    continue;
                var id = BinaryPrimitives.ReadUInt16LittleEndian(data[GbaSectorContainer.IdOffset..]);
                if (id >= SectionLayout.SectionCount || sectorOf[id] >= 0)
                {
                    complete = false;
                    break;
                }
                sectorOf[id] = sector;
                seen++;
                counter = BinaryPrimitives.ReadUInt32LittleEndian(data[GbaSectorContainer.CounterOffset..]);
            }
            if (complete && seen == SectionLayout.SectionCount)
                found.Add(new SlotMap(slot, counter, sectorOf));
        }
        if (found.Count == 2 && found[1].Counter > found[0].Counter)
            found.Reverse();
        return found;
    }

    private static bool LargeChecksums(ReadOnlySpan<byte> file, int[] sectorOf, int stride, int last)
    {
        var longStride = stride > SectionLayout.VanillaStride;
        var pastRetail = false;
        for (var index = 0; index < LargeSections; index++)
        {
            var data = file.Slice(sectorOf[FirstLargeSection + index] * SectionLayout.SectorSize, SectionLayout.SectorSize);
            var stored = BinaryPrimitives.ReadUInt16LittleEndian(data[GbaSectorContainer.ChecksumOffset..]);
            var size = index < LargeSections - 1 ? stride : last;
            var ok = ChecksumPolicy.Fold(data[..size]) == stored || ChecksumPolicy.Fold(data[..stride]) == stored;
            if (!ok && longStride)
                ok = ChecksumPolicy.Fold(data[..SectionLayout.PayloadCapacity]) == stored;
            if (!ok)
                return false;
            if (longStride && index < LargeSections - 1 && data[SectionLayout.VanillaStride..stride].ContainsAnyExcept((byte)0))
                pastRetail = true;
        }
        return !longStride || pastRetail;
    }
}
