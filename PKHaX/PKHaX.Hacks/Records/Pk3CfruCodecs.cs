using System;
using System.Buffers.Binary;

namespace PKHaX.Hacks.Records;

internal sealed class Pk3CfruPlainCodec : IHackRecordCodec
{
    public RecordCodecKey Key { get; } = new(RecordCodecId.Pk3CfruPlain, null);
    public int? PartySize => Gen3Record.PartySize;
    public int? StoredSize => null;
    public bool Encrypted => false;

    public HackMon? Decode(ReadOnlySpan<byte> record, out string? why)
    {
        if (!RecordShapes.TryShape(this, record.Length, out var shape, out why))
            return null;
        var g = record.Slice(0x20, 12);
        var a = record.Slice(0x2C, 12);
        var e = record.Slice(0x38, 12);
        var m = record.Slice(0x44, 12);
        var species = BinaryPrimitives.ReadUInt16LittleEndian(g);
        if (species == 0)
        {
            why = "empty slot";
            return null;
        }
        var pid = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var otId = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        var language = record[0x12];
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(m[4..]);
        var party = Gen3Record.PartyTail(record, 0x50);
        var checksum = BinaryPrimitives.ReadUInt16LittleEndian(record[0x1C..]);
        why = checksum != 0
            ? $"{RecordPlausibility.Prefix}checksum field 0x{checksum:X4} is not 0 (CFRU party records are plaintext with a zero checksum)"
            : species > RecordPlausibility.CfruSpeciesCap
                ? $"{RecordPlausibility.Prefix}species {species} is above {RecordPlausibility.CfruSpeciesCap}"
                : RecordPlausibility.Gen3Header(language, record[0x13], ivWord) ?? RecordPlausibility.Gen3Party(party);
        if (why is not null)
            return null;
        return new HackMon
        {
            Codec = Key,
            Shape = shape,
            Species = species,
            National = Gen3Record.VanillaNational(species),
            Pid = pid,
            Tid16 = (ushort)otId,
            Sid16 = (ushort)(otId >> 16),
            Nickname = Gen3Record.Text(record.Slice(0x08, 10), language),
            OtName = Gen3Record.Text(record.Slice(0x14, 7), language),
            Language = language,
            Exp = BinaryPrimitives.ReadUInt32LittleEndian(g[4..]),
            Level = party.Level,
            Moves = Gen3Record.Moves16(a, 0xFFFF),
            Pp = Gen3Record.Bytes4(a[8..], 0xFF),
            PpUps = Gen3Record.PpUps(g[8]),
            HeldItem = BinaryPrimitives.ReadUInt16LittleEndian(g[2..]),
            Ball = g[10],
            Ivs = Gen3Record.IvsFromWord(ivWord),
            Evs = Gen3Record.Evs(e),
            Nature = (byte)(pid % 25),
            PidNature = (byte)(pid % 25),
            HiddenAbility = (ivWord >> 31) != 0,
            IsEgg = ((ivWord >> 30) & 1) != 0,
            ShinyXor = Gen3Record.ShinyXor(pid, otId),
            Friendship = g[9],
            HeaderFlags = record[0x13],
            Party = party,
            Raw = record.ToArray(),
        };
    }
}

internal sealed class Pk3CfruCompact58Codec : IHackRecordCodec
{
    public const int Size = 58;

    public RecordCodecKey Key { get; } = new(RecordCodecId.Pk3CfruCompact58, null);
    public int? PartySize => null;
    public int? StoredSize => Size;
    public bool Encrypted => false;

    public HackMon? Decode(ReadOnlySpan<byte> record, out string? why)
    {
        if (!RecordShapes.TryShape(this, record.Length, out var shape, out why))
            return null;
        var species = BinaryPrimitives.ReadUInt16LittleEndian(record[0x1C..]);
        if (species == 0)
        {
            why = "empty slot";
            return null;
        }
        var pid = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var otId = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        var language = record[0x12];
        ulong packed = 0;
        for (var i = 0; i < 5; i++)
            packed |= (ulong)record[0x27 + i] << (8 * i);
        var moves = new ushort[4];
        for (var i = 0; i < 4; i++)
            moves[i] = (ushort)((packed >> (10 * i)) & 0x3FF);
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(record[0x36..]);
        why = species > RecordPlausibility.CfruSpeciesCap
            ? $"{RecordPlausibility.Prefix}species {species} is above {RecordPlausibility.CfruSpeciesCap}"
            : RecordPlausibility.Gen3Header(language, record[0x13], ivWord);
        if (why is not null)
            return null;
        return new HackMon
        {
            Codec = Key,
            Shape = shape,
            Species = species,
            National = Gen3Record.VanillaNational(species),
            Pid = pid,
            Tid16 = (ushort)otId,
            Sid16 = (ushort)(otId >> 16),
            Nickname = Gen3Record.Text(record.Slice(0x08, 10), language),
            OtName = Gen3Record.Text(record.Slice(0x14, 7), language),
            Language = language,
            Exp = BinaryPrimitives.ReadUInt32LittleEndian(record[0x20..]),
            Moves = moves,
            PpUps = Gen3Record.PpUps(record[0x24]),
            HeldItem = BinaryPrimitives.ReadUInt16LittleEndian(record[0x1E..]),
            Ball = record[0x26],
            Ivs = Gen3Record.IvsFromWord(ivWord),
            Evs = Gen3Record.Evs(record.Slice(0x2C, 6)),
            Nature = (byte)(pid % 25),
            PidNature = (byte)(pid % 25),
            HiddenAbility = (ivWord >> 31) != 0,
            IsEgg = ((ivWord >> 30) & 1) != 0,
            ShinyXor = Gen3Record.ShinyXor(pid, otId),
            Friendship = record[0x25],
            HeaderFlags = record[0x13],
            Raw = record.ToArray(),
        };
    }
}
