using System;
using System.Collections.Generic;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core;

public enum EnigmaBerry3Kind : byte
{
    EmeraldFireRedLeafGreen,
    RubySapphire,
}

public readonly record struct EnigmaBerryRange(int Offset, int Length);

public sealed class EnigmaBerry3
{
    public const int SizeEmeraldFireRedLeafGreen = 0x34;
    public const int SizeRubySapphire = 0x530;
    public const int NameLength = 6;
    public const int ItemEffectLength = 18;
    public const int DescriptionLength = 44;
    public const int PicLength = 0x480;
    public const int PaletteLength = 0x20;
    public const int StockEntryLength = 0x1C;

    private const int OffsetName = 0x00;
    private const int OffsetFirmness = 0x07;
    private const int OffsetSize = 0x08;
    private const int OffsetMaxYield = 0x0A;
    private const int OffsetMinYield = 0x0B;
    private const int OffsetDescription1 = 0x0C;
    private const int OffsetDescription2 = 0x10;
    private const int OffsetStageDuration = 0x14;
    private const int OffsetSpicy = 0x15;
    private const int OffsetDry = 0x16;
    private const int OffsetSweet = 0x17;
    private const int OffsetBitter = 0x18;
    private const int OffsetSour = 0x19;
    private const int OffsetSmoothness = 0x1A;
    private const int OffsetPic = 0x1C;
    private const int OffsetPalette = 0x49C;
    private const int OffsetText1 = 0x4BC;
    private const int OffsetText2 = 0x4E9;
    private const uint RomBase = 0x08000000;
    private const int LongestTagText = 0x80;

    private static ReadOnlySpan<byte> StockNameBytes => [0xBF, 0xC8, 0xC3, 0xC1, 0xC7, 0xBB, 0xFF];
    private static ReadOnlySpan<byte> StockHead => [0, 0, 0, 2, 1];
    private static ReadOnlySpan<byte> StockTail => [24, 40, 40, 40, 40, 40, 40, 0];

    private readonly byte[] data;
    private byte[] opened;
    private bool defaultsUsed;

    private EnigmaBerry3(ReadOnlySpan<byte> record, EnigmaBerry3Kind kind, bool japanese)
    {
        data = record.ToArray();
        opened = record.ToArray();
        Kind = kind;
        Japanese = japanese;
    }

    public EnigmaBerry3Kind Kind { get; }
    public bool Japanese { get; }

    public static bool TryKind(int length, out EnigmaBerry3Kind kind)
    {
        kind = length == SizeRubySapphire ? EnigmaBerry3Kind.RubySapphire : EnigmaBerry3Kind.EmeraldFireRedLeafGreen;
        return length is SizeRubySapphire or SizeEmeraldFireRedLeafGreen;
    }

    public static int SizeOf(EnigmaBerry3Kind kind) => kind == EnigmaBerry3Kind.RubySapphire ? SizeRubySapphire : SizeEmeraldFireRedLeafGreen;

    public static EnigmaBerry3 From(SAV3 sav)
    {
        var record = sav.LargeBlock.EReaderBerry;
        if (!TryKind(record.Length, out var kind))
            throw new ArgumentException($"An e-Reader berry record is 0x{SizeEmeraldFireRedLeafGreen:X} or 0x{SizeRubySapphire:X} bytes long; this save holds 0x{record.Length:X}.", nameof(sav));
        return new EnigmaBerry3(record, kind, sav.Japanese);
    }

    public static EnigmaBerry3 From(ReadOnlySpan<byte> record, EnigmaBerry3Kind kind, bool japanese)
    {
        if (!TryKind(record.Length, out var found) || found != kind)
            throw new ArgumentException($"A {kind} record is 0x{SizeOf(kind):X} bytes long; this one is 0x{record.Length:X}.", nameof(record));
        return new EnigmaBerry3(record, kind, japanese);
    }

    public int Length => data.Length;
    public bool HasOwnArt => Kind == EnigmaBerry3Kind.RubySapphire;
    public bool HasOwnDescriptionText => Kind == EnigmaBerry3Kind.RubySapphire;
    public bool DescriptionTextEditable => HasOwnDescriptionText && DescriptionPointer1 != 0 && DescriptionPointer2 != 0;

    public int ItemEffectOffset => Kind == EnigmaBerry3Kind.RubySapphire ? 0x516 : 0x1C;
    public int HoldEffectOffset => Kind == EnigmaBerry3Kind.RubySapphire ? 0x528 : 0x2E;
    public int HoldEffectParamOffset => Kind == EnigmaBerry3Kind.RubySapphire ? 0x529 : 0x2F;
    public int ChecksumOffset => ChecksumOffsetOf(Kind);

    private static int ChecksumOffsetOf(EnigmaBerry3Kind kind) => kind == EnigmaBerry3Kind.RubySapphire ? 0x52C : 0x30;

    public string Name => StringConverter3.GetString(data.AsSpan(OffsetName, NameLength + 1), Japanese);

    public bool TrySetName(ReadOnlySpan<char> value, out string? refusal)
    {
        if (value.Length == 0)
        {
            refusal = "The name needs at least one character.";
            return false;
        }
        if (value.Length > NameLength)
        {
            refusal = $"The name holds {NameLength} characters at most; this one has {value.Length}.";
            return false;
        }
        if (value.SequenceEqual(Name.AsSpan()))
        {
            refusal = null;
            return true;
        }
        Span<byte> encoded = stackalloc byte[NameLength + 1];
        int count = StringConverter3.SetString(encoded, value, NameLength, Japanese, StringConverterOption.ClearFF);
        if (count != value.Length + 1)
        {
            refusal = $"The character '{value[count - 1]}' is not in this game's character set.";
            return false;
        }
        if (encoded[0] == 0)
        {
            refusal = "The name cannot start with a space: a first byte of 0 reads as no berry.";
            return false;
        }
        encoded.CopyTo(data.AsSpan(OffsetName));
        refusal = null;
        return true;
    }

    public byte Firmness { get => data[OffsetFirmness]; set => data[OffsetFirmness] = value; }
    public ushort Size { get => ReadUInt16LittleEndian(data.AsSpan(OffsetSize)); set => WriteUInt16LittleEndian(data.AsSpan(OffsetSize), value); }
    public byte MaxYield { get => data[OffsetMaxYield]; set => data[OffsetMaxYield] = value; }
    public byte MinYield { get => data[OffsetMinYield]; set => data[OffsetMinYield] = value; }
    public uint DescriptionPointer1 { get => ReadUInt32LittleEndian(data.AsSpan(OffsetDescription1)); set => WriteUInt32LittleEndian(data.AsSpan(OffsetDescription1), value); }
    public uint DescriptionPointer2 { get => ReadUInt32LittleEndian(data.AsSpan(OffsetDescription2)); set => WriteUInt32LittleEndian(data.AsSpan(OffsetDescription2), value); }
    public byte StageDuration { get => data[OffsetStageDuration]; set => data[OffsetStageDuration] = value; }
    public byte Spicy { get => data[OffsetSpicy]; set => data[OffsetSpicy] = value; }
    public byte Dry { get => data[OffsetDry]; set => data[OffsetDry] = value; }
    public byte Sweet { get => data[OffsetSweet]; set => data[OffsetSweet] = value; }
    public byte Bitter { get => data[OffsetBitter]; set => data[OffsetBitter] = value; }
    public byte Sour { get => data[OffsetSour]; set => data[OffsetSour] = value; }
    public byte Smoothness { get => data[OffsetSmoothness]; set => data[OffsetSmoothness] = value; }
    public byte HoldEffect { get => data[HoldEffectOffset]; set => data[HoldEffectOffset] = value; }
    public byte HoldEffectParam { get => data[HoldEffectParamOffset]; set => data[HoldEffectParamOffset] = value; }

    public ReadOnlySpan<byte> ItemEffect => data.AsSpan(ItemEffectOffset, ItemEffectLength);

    public void SetItemEffect(ReadOnlySpan<byte> value)
    {
        if (value.Length != ItemEffectLength)
            throw new ArgumentException($"The item-use effect is {ItemEffectLength} bytes long; {value.Length} were given.", nameof(value));
        value.CopyTo(data.AsSpan(ItemEffectOffset));
    }

    public ReadOnlySpan<byte> Pic => HasOwnArt ? data.AsSpan(OffsetPic, PicLength) : ReadOnlySpan<byte>.Empty;
    public ReadOnlySpan<byte> Palette => HasOwnArt ? data.AsSpan(OffsetPalette, PaletteLength) : ReadOnlySpan<byte>.Empty;

    public string Description1 => DescriptionText(OffsetText1);
    public string Description2 => DescriptionText(OffsetText2);

    private string DescriptionText(int offset) => HasOwnDescriptionText
        ? StringConverter3.GetString(data.AsSpan(offset, DescriptionLength + 1), Japanese)
        : string.Empty;

    public bool TrySetDescription(int line, ReadOnlySpan<char> value, out string? refusal)
    {
        if (line is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(line));
        if (!HasOwnDescriptionText)
        {
            refusal = "Only Ruby and Sapphire keep the berry's description text in the save.";
            return false;
        }
        int offset = line == 1 ? OffsetText1 : OffsetText2;
        if (value.SequenceEqual(DescriptionText(offset).AsSpan()))
        {
            refusal = null;
            return true;
        }
        if (!DescriptionTextEditable)
        {
            refusal = "The description pointers are zero, so the game does not read this text. It is left as found.";
            return false;
        }
        if (value.Length > DescriptionLength)
        {
            refusal = $"A description line holds {DescriptionLength} characters at most; this one has {value.Length}.";
            return false;
        }
        Span<byte> encoded = stackalloc byte[DescriptionLength + 1];
        int count = StringConverter3.SetString(encoded, value, DescriptionLength, Japanese, StringConverterOption.ClearFF);
        if (count != value.Length + 1)
        {
            refusal = $"The character '{value[count - 1]}' is not in this game's character set.";
            return false;
        }
        encoded.CopyTo(data.AsSpan(offset));
        refusal = null;
        return true;
    }

    public uint StoredChecksum => ReadUInt32LittleEndian(opened.AsSpan(ChecksumOffset));

    public uint ComputeChecksum() => Checksum(data, Kind);

    public static uint Checksum(ReadOnlySpan<byte> record, EnigmaBerry3Kind kind)
    {
        int end = ChecksumOffsetOf(kind);
        if (record.Length < end)
            throw new ArgumentException($"A {kind} record is 0x{SizeOf(kind):X} bytes long; this one is 0x{record.Length:X}.", nameof(record));
        bool skipPointers = kind == EnigmaBerry3Kind.RubySapphire;
        uint sum = 0;
        for (int i = 0; i < end; i++)
        {
            if (skipPointers && i is >= OffsetDescription1 and < OffsetStageDuration)
                continue;
            sum += record[i];
        }
        return sum;
    }

    public bool IsValid => Problem is null;

    public string? Problem
    {
        get
        {
            if (opened[OffsetStageDuration] == 0)
                return "The growth time is 0, which the game reads as no berry data.";
            if (opened[OffsetMaxYield] == 0)
                return "The largest harvest is 0, which the game reads as no berry data.";
            if (Checksum(opened, Kind) != StoredChecksum)
                return "The stored checksum does not match the record's bytes.";
            return null;
        }
    }

    public string? WriteRefusal
    {
        get
        {
            if (StageDuration == 0)
                return "The growth time cannot be 0: the game would treat the record as empty.";
            if (MaxYield == 0)
                return "The largest harvest cannot be 0: the game would treat the record as empty.";
            if (MinYield > MaxYield)
                return "The smallest harvest cannot be above the largest: a planted berry would give a garbage harvest count.";
            return null;
        }
    }

    public bool DefaultsUsed => defaultsUsed;

    public void UseDefaults()
    {
        StockNameBytes.CopyTo(data.AsSpan(OffsetName));
        Firmness = 0;
        Size = 0;
        MaxYield = 2;
        MinYield = 1;
        StageDuration = 24;
        Spicy = 40;
        Dry = 40;
        Sweet = 40;
        Bitter = 40;
        Sour = 40;
        Smoothness = 40;
        defaultsUsed = true;
    }

    public void Apply(EnigmaBerryPreset preset)
    {
        if (!IsValid && !defaultsUsed)
            UseDefaults();
        HoldEffect = preset.HoldEffect;
        HoldEffectParam = preset.Parameter;
    }

    public byte[] Preview()
    {
        var result = data.AsSpan().ToArray();
        WriteUInt32LittleEndian(result.AsSpan(ChecksumOffset), Checksum(result, Kind));
        return result;
    }

    public bool HasChanges => !Preview().AsSpan().SequenceEqual(opened);

    public void Revert()
    {
        opened.CopyTo(data.AsSpan());
        defaultsUsed = false;
    }

    public IReadOnlyList<EnigmaBerryRange> Write(Span<byte> record)
    {
        if (record.Length != data.Length)
            throw new ArgumentException($"A {Kind} record is 0x{data.Length:X} bytes long; the destination is 0x{record.Length:X}.", nameof(record));
        if (WriteRefusal is { } refusal)
            throw new InvalidOperationException(refusal);

        WriteUInt32LittleEndian(data.AsSpan(ChecksumOffset), ComputeChecksum());
        var changed = new List<EnigmaBerryRange>();
        int start = -1;
        for (int i = 0; i < data.Length; i++)
        {
            if (record[i] != data[i])
            {
                if (start < 0)
                    start = i;
                record[i] = data[i];
            }
            else if (start >= 0)
            {
                changed.Add(new EnigmaBerryRange(start, i - start));
                start = -1;
            }
        }
        if (start >= 0)
            changed.Add(new EnigmaBerryRange(start, data.Length - start));
        opened = data.AsSpan().ToArray();
        return changed;
    }

    public IReadOnlyList<EnigmaBerryRange> Write(SAV3 sav) => Write(sav.LargeBlock.EReaderBerry);

    public static bool TryFindStockDescriptions(ReadOnlySpan<byte> rom, out uint description1, out uint description2, out string? refusal)
    {
        description1 = 0;
        description2 = 0;
        int found = 0;
        bool same = true;
        for (int at = 0; at + StockEntryLength <= rom.Length; at += 4)
        {
            var entry = rom.Slice(at, StockEntryLength);
            if (entry[OffsetStageDuration] != StockTail[0])
                continue;
            if (!entry.Slice(OffsetFirmness, StockHead.Length).SequenceEqual(StockHead))
                continue;
            if (!entry.Slice(OffsetStageDuration, StockTail.Length).SequenceEqual(StockTail))
                continue;
            if (entry[..(NameLength + 1)].IndexOf(StringConverter3.TerminatorByte) < 1)
                continue;
            uint first = ReadUInt32LittleEndian(entry[OffsetDescription1..]);
            uint second = ReadUInt32LittleEndian(entry[OffsetDescription2..]);
            if (!IsRomText(rom, first) || !IsRomText(rom, second))
                continue;
            if (found != 0 && (first != description1 || second != description2))
                same = false;
            description1 = first;
            description2 = second;
            found++;
        }

        if (found != 0 && same)
        {
            refusal = null;
            return true;
        }
        description1 = 0;
        description2 = 0;
        refusal = found == 0
            ? "This ROM holds no stock Enigma Berry entry (largest harvest 2, smallest 1, growth time 24, every flavour 40), so it gives no tag text."
            : $"This ROM holds {found} entries that look like the stock Enigma Berry with different text, so none is trusted.";
        return false;
    }

    private static bool IsRomText(ReadOnlySpan<byte> rom, uint pointer)
    {
        if (pointer < RomBase)
            return false;
        uint offset = pointer - RomBase;
        if (offset >= (uint)rom.Length)
            return false;
        var text = rom[(int)offset..];
        if (text.Length > LongestTagText)
            text = text[..LongestTagText];
        return text.IndexOf(StringConverter3.TerminatorByte) >= 0;
    }
}
