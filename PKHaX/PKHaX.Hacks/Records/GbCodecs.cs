using System;
using System.Buffers.Binary;
using PKHeX.Core;

namespace PKHaX.Hacks.Records;

internal static class GbRecord
{
    public static StatSet Dvs(ushort dv)
    {
        var atk = (dv >> 12) & 0xF;
        var def = (dv >> 8) & 0xF;
        var spe = (dv >> 4) & 0xF;
        var spc = dv & 0xF;
        var hp = ((atk & 1) << 3) | ((def & 1) << 2) | ((spe & 1) << 1) | (spc & 1);
        return new StatSet(hp, atk, def, spc, spc, spe);
    }

    public static StatSet StatExp(ReadOnlySpan<byte> data)
    {
        var hp = BinaryPrimitives.ReadUInt16BigEndian(data);
        var atk = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        var def = BinaryPrimitives.ReadUInt16BigEndian(data[4..]);
        var spe = BinaryPrimitives.ReadUInt16BigEndian(data[6..]);
        var spc = BinaryPrimitives.ReadUInt16BigEndian(data[8..]);
        return new StatSet(hp, atk, def, spc, spc, spe);
    }

    public static uint Exp(ReadOnlySpan<byte> data) => (uint)((data[0] << 16) | (data[1] << 8) | data[2]);

    public static ushort[] Moves(ReadOnlySpan<byte> data) => [data[0], data[1], data[2], data[3]];

    public static bool Gen2Shiny(StatSet dv) => dv.Def == 10 && dv.Spe == 10 && dv.SpA == 10 && (dv.Atk & 2) == 2;
}

internal sealed class Pk1Codec : IHackRecordCodec
{
    public RecordCodecKey Key { get; } = new(RecordCodecId.Pk1, null);
    public int? PartySize => 44;
    public int? StoredSize => 33;
    public bool Encrypted => false;

    public HackMon? Decode(ReadOnlySpan<byte> record, out string? why)
    {
        if (!RecordShapes.TryShape(this, record.Length, out var shape, out why))
            return null;
        var species = record[0];
        if (species == 0)
        {
            why = "empty slot";
            return null;
        }
        var national = SpeciesConverter.GetNational1(species);
        var dvs = GbRecord.Dvs(BinaryPrimitives.ReadUInt16BigEndian(record[0x1B..]));
        HackPartyStats? party = null;
        if (shape == RecordShape.Party)
        {
            var spc = BinaryPrimitives.ReadUInt16BigEndian(record[0x2A..]);
            party = new HackPartyStats(
                record[4],
                record[0x21],
                BinaryPrimitives.ReadUInt16BigEndian(record[1..]),
                new StatSet(
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x22..]),
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x24..]),
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x26..]),
                    spc,
                    spc,
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x28..])));
        }
        var tid = BinaryPrimitives.ReadUInt16BigEndian(record[0x0C..]);
        var level = shape == RecordShape.Party ? record[0x21] : record[3];
        var exp = GbRecord.Exp(record[0x0E..]);
        var currentHp = BinaryPrimitives.ReadUInt16BigEndian(record[1..]);
        why = RecordPlausibility.GbLevelAndExp(level, exp)
            ?? RecordPlausibility.GbStatus(record[4])
            ?? (party is not null
                ? RecordPlausibility.GbParty(party)
                : currentHp > RecordPlausibility.MaxStat ? $"{RecordPlausibility.Prefix}current HP {currentHp} is above {RecordPlausibility.MaxStat}" : null);
        if (why is not null)
            return null;
        return new HackMon
        {
            Codec = Key,
            Shape = shape,
            Species = species,
            National = national == 0 ? null : national,
            Tid16 = tid,
            Exp = exp,
            Level = level,
            Moves = GbRecord.Moves(record[0x08..]),
            Pp = Gen3Record.Bytes4(record[0x1D..], 0x3F),
            PpUps = [(byte)(record[0x1D] >> 6), (byte)(record[0x1E] >> 6), (byte)(record[0x1F] >> 6), (byte)(record[0x20] >> 6)],
            CatchRate = record[7],
            Ivs = dvs,
            Evs = GbRecord.StatExp(record[0x11..]),
            Party = party,
            Raw = record.ToArray(),
        };
    }
}

internal sealed class Pk2Codec : IHackRecordCodec
{
    public RecordCodecKey Key { get; } = new(RecordCodecId.Pk2, null);
    public int? PartySize => 48;
    public int? StoredSize => 32;
    public bool Encrypted => false;

    public HackMon? Decode(ReadOnlySpan<byte> record, out string? why)
    {
        if (!RecordShapes.TryShape(this, record.Length, out var shape, out why))
            return null;
        var species = record[0];
        if (species == 0)
        {
            why = "empty slot";
            return null;
        }
        var dvs = GbRecord.Dvs(BinaryPrimitives.ReadUInt16BigEndian(record[0x15..]));
        HackPartyStats? party = null;
        if (shape == RecordShape.Party)
        {
            party = new HackPartyStats(
                record[0x20],
                record[0x1F],
                BinaryPrimitives.ReadUInt16BigEndian(record[0x22..]),
                new StatSet(
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x24..]),
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x26..]),
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x28..]),
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x2C..]),
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x2E..]),
                    BinaryPrimitives.ReadUInt16BigEndian(record[0x2A..])));
        }
        var exp = GbRecord.Exp(record[0x08..]);
        why = RecordPlausibility.GbLevelAndExp(record[0x1F], exp)
            ?? RecordPlausibility.Gen2Pokerus(record[0x1C])
            ?? (party is null ? null : RecordPlausibility.GbStatus(record[0x20]) ?? RecordPlausibility.GbParty(party));
        if (why is not null)
            return null;
        return new HackMon
        {
            Codec = Key,
            Shape = shape,
            Species = species,
            National = species <= 251 ? species : null,
            Tid16 = BinaryPrimitives.ReadUInt16BigEndian(record[0x06..]),
            Exp = exp,
            Level = record[0x1F],
            Moves = GbRecord.Moves(record[0x02..]),
            Pp = Gen3Record.Bytes4(record[0x17..], 0x3F),
            PpUps = [(byte)(record[0x17] >> 6), (byte)(record[0x18] >> 6), (byte)(record[0x19] >> 6), (byte)(record[0x1A] >> 6)],
            HeldItem = record[1],
            Ivs = dvs,
            Evs = GbRecord.StatExp(record[0x0B..]),
            IsShiny = GbRecord.Gen2Shiny(dvs),
            Friendship = record[0x1B],
            Party = party,
            Raw = record.ToArray(),
        };
    }
}
