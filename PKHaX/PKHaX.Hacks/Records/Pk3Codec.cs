using System;
using System.Buffers.Binary;

namespace PKHaX.Hacks.Records;

internal sealed class Pk3Codec : IHackRecordCodec
{
    public RecordCodecKey Key { get; } = new(RecordCodecId.Pk3, null);
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
        var species = BinaryPrimitives.ReadUInt16LittleEndian(s);
        var flags = record[0x13];
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
        var g = s[..12];
        var a = s.Slice(12, 12);
        var e = s.Slice(24, 12);
        var m = s.Slice(36, 12);
        var pid = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var otId = BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        var language = record[0x12];
        var origins = BinaryPrimitives.ReadUInt16LittleEndian(m[2..]);
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(m[4..]);
        var xor = Gen3Record.ShinyXor(pid, otId);
        var party = shape == RecordShape.Party ? Gen3Record.PartyTail(record, 0x50) : null;
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
            Level = party?.Level,
            Moves = Gen3Record.Moves16(a, 0xFFFF),
            Pp = Gen3Record.Bytes4(a[8..], 0xFF),
            PpUps = Gen3Record.PpUps(g[8]),
            HeldItem = BinaryPrimitives.ReadUInt16LittleEndian(g[2..]),
            Ball = (byte)((origins >> 11) & 0xF),
            Ivs = Gen3Record.IvsFromWord(ivWord),
            Evs = Gen3Record.Evs(e),
            Nature = (byte)(pid % 25),
            PidNature = (byte)(pid % 25),
            AbilitySlot = (byte)(ivWord >> 31),
            IsEgg = ((ivWord >> 30) & 1) != 0,
            ShinyXor = xor,
            IsShiny = xor < 8,
            Friendship = g[9],
            HeaderFlags = flags,
            ChecksumValid = true,
            Party = party,
            Raw = record.ToArray(),
        };
    }
}
