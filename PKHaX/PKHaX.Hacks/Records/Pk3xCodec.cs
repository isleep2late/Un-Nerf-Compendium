using System;
using System.Buffers.Binary;

namespace PKHaX.Hacks.Records;

internal sealed class Pk3xCodec(string version) : IHackRecordCodec
{
    public RecordCodecKey Key { get; } = new(RecordCodecId.Pk3x, version);
    public int? PartySize => Gen3Record.PartySize;
    public int? StoredSize => Gen3Record.StoredSize;
    public bool Encrypted => true;

    public HackMon? Decode(ReadOnlySpan<byte> record, out string? why)
    {
        if (!RecordShapes.TryShape(this, record.Length, out var shape, out why))
            return null;
        Span<byte> s = stackalloc byte[Gen3Record.SecureSize];
        var sum = Gen3Record.Decrypt(record, s);
        var stored = BinaryPrimitives.ReadUInt16LittleEndian(record[0x1C..]);
        var flags = record[0x13];
        var g = s[..12];
        var a = s.Slice(12, 12);
        var e = s.Slice(24, 12);
        var m = s.Slice(36, 12);
        var g0 = BinaryPrimitives.ReadUInt16LittleEndian(g);
        var species = (ushort)(g0 & 0x7FF);
        if (sum != stored)
        {
            why = species == 0 && stored == 0 && (flags & 2) == 0 ? "empty slot" : $"checksum 0x{stored:X4} does not match the data (0x{sum:X4})";
            return null;
        }
        if (species == 0)
        {
            why = "empty slot";
            return null;
        }
        var pid = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var otId = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        var language = record[0x12] & 7;
        var hiddenNature = (byte)(record[0x12] >> 3);
        var tail = BinaryPrimitives.ReadUInt16LittleEndian(record[0x1E..]);
        var expWord = BinaryPrimitives.ReadUInt32LittleEndian(g[4..]);
        var ballWord = BinaryPrimitives.ReadUInt16LittleEndian(g[10..]);
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(m[4..]);
        var ribbons = BinaryPrimitives.ReadUInt32LittleEndian(m[8..]);
        Span<byte> nick = stackalloc byte[12];
        record.Slice(0x08, 10).CopyTo(nick);
        var n11 = (byte)((expWord >> 21) & 0xFF);
        var n12 = (byte)((ballWord >> 6) & 0xFF);
        var vanillaTail = n11 == 0 && n12 == 0;
        nick[10] = vanillaTail ? (byte)0xFF : n11;
        nick[11] = vanillaTail ? (byte)0xFF : n12;
        var pidNature = (byte)(pid % 25);
        var party = shape == RecordShape.Party ? Gen3Record.PartyTail(record, 0x50) : null;
        return new HackMon
        {
            Codec = Key,
            Shape = shape,
            Species = species,
            Pid = pid,
            Tid16 = (ushort)otId,
            Sid16 = (ushort)(otId >> 16),
            Nickname = Gen3Record.Text(nick, language),
            OtName = Gen3Record.Text(record.Slice(0x14, 7), language),
            Language = language,
            Exp = expWord & 0x1FFFFF,
            Level = party?.Level,
            Moves = Gen3Record.Moves16(a, 0x7FF),
            Pp = Gen3Record.Bytes4(a[8..], 0x7F),
            PpUps = Gen3Record.PpUps(g[8]),
            HeldItem = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(g[2..]) & 0x3FF),
            Ball = (byte)(ballWord & 0x3F),
            Ivs = Gen3Record.IvsFromWord(ivWord),
            Evs = Gen3Record.Evs(e),
            Nature = (byte)(pidNature ^ hiddenNature),
            PidNature = pidNature,
            AbilitySlot = (byte)((ribbons >> 29) & 3),
            IsEgg = ((ivWord >> 30) & 1) != 0,
            ShinyXor = Gen3Record.ShinyXor(pid, otId),
            ShinyModifier = ((tail >> 14) & 1) != 0,
            TeraType = (byte)(g0 >> 11),
            HiddenNatureModifier = hiddenNature,
            Friendship = g[9],
            HeaderFlags = flags,
            ChecksumValid = true,
            Party = party,
            Raw = record.ToArray(),
        };
    }
}

internal sealed class Pk3SoulGold96Codec : IHackRecordCodec
{
    public const int Party = 96;
    public const int Box = 0x4C;

    public RecordCodecKey Key { get; } = new(RecordCodecId.Pk3SoulGold96, null);
    public int? PartySize => Party;
    public int? StoredSize => Box;
    public bool Encrypted => false;

    public HackMon? Decode(ReadOnlySpan<byte> record, out string? why)
    {
        if (!RecordShapes.TryShape(this, record.Length, out var shape, out why))
            return null;
        var g = record.Slice(0x20, 12);
        var a = record.Slice(0x2C, 12);
        var e = record.Slice(0x38, 12);
        var m = record.Slice(0x44, 8);
        var g0 = BinaryPrimitives.ReadUInt16LittleEndian(g);
        var species = (ushort)(g0 & 0x7FF);
        if (species == 0)
        {
            why = "empty slot";
            return null;
        }
        var pid = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var otId = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        var language = record[0x14] & 7;
        var hiddenNature = (byte)(record[0x14] >> 3);
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(m[4..]);
        var pidNature = (byte)(pid % 25);
        var party = shape == RecordShape.Party ? Gen3Record.PartyTail(record, 0x4C) : null;
        var nature = pidNature ^ hiddenNature;
        why = RecordPlausibility.Gen3Header(language, record[0x15], ivWord)
            ?? (nature >= 25 ? $"{RecordPlausibility.Prefix}nature {nature} (PID nature {pidNature} XOR modifier {hiddenNature}) is not below 25" : null)
            ?? RecordPlausibility.Gen3Pp(a.Slice(8, 4), 0x7F)
            ?? (party is null ? null : RecordPlausibility.Gen3Party(party));
        if (why is not null)
            return null;
        return new HackMon
        {
            Codec = Key,
            Shape = shape,
            Species = species,
            Pid = pid,
            Tid16 = (ushort)otId,
            Sid16 = (ushort)(otId >> 16),
            Nickname = Gen3Record.Text(record.Slice(0x08, 12), language),
            OtName = Gen3Record.Text(record.Slice(0x16, 7), language),
            Language = language,
            Exp = BinaryPrimitives.ReadUInt32LittleEndian(g[4..]) & 0x1FFFFF,
            Level = party?.Level,
            Moves = Gen3Record.Moves16(a, 0x7FF),
            Pp = Gen3Record.Bytes4(a[8..], 0x7F),
            PpUps = Gen3Record.PpUps(g[8]),
            HeldItem = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(g[2..]) & 0x3FF),
            Ivs = Gen3Record.IvsFromWord(ivWord),
            Evs = Gen3Record.Evs(e),
            Nature = (byte)nature,
            PidNature = pidNature,
            IsEgg = ((ivWord >> 30) & 1) != 0,
            ShinyXor = Gen3Record.ShinyXor(pid, otId),
            TeraType = (byte)(g0 >> 11),
            HiddenNatureModifier = hiddenNature,
            Friendship = g[11],
            HeaderFlags = record[0x15],
            Party = party,
            Raw = record.ToArray(),
        };
    }
}
