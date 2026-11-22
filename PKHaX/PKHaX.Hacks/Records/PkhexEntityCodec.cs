using System;
using PKHeX.Core;

namespace PKHaX.Hacks.Records;

internal sealed class PkhexEntityCodec(RecordCodecId id, int party, int stored, Func<byte[], PKM> create) : IHackRecordCodec
{
    public RecordCodecKey Key { get; } = new(id, null);
    public int? PartySize => party;
    public int? StoredSize => stored;
    public bool Encrypted => true;

    public HackMon? Decode(ReadOnlySpan<byte> record, out string? why)
    {
        if (!RecordShapes.TryShape(this, record.Length, out var shape, out why))
            return null;
        var pk = create(record.ToArray());
        if (!pk.ChecksumValid)
        {
            why = pk.Species == 0 && IsBlank(record) ? "empty slot" : "checksum does not match the data";
            return null;
        }
        if (pk.Species == 0)
        {
            why = "empty slot";
            return null;
        }
        var hasParty = shape == RecordShape.Party;
        return new HackMon
        {
            Codec = Key,
            Shape = shape,
            Species = pk.Species,
            National = pk.Species,
            Form = pk.Form,
            Pid = pk.PID,
            Tid16 = pk.TID16,
            Sid16 = pk.SID16,
            Nickname = pk.Nickname,
            OtName = pk.OriginalTrainerName,
            Language = pk.Language,
            Exp = pk.EXP,
            Level = hasParty ? pk.Stat_Level : null,
            Moves = [pk.Move1, pk.Move2, pk.Move3, pk.Move4],
            Pp = [(byte)pk.Move1_PP, (byte)pk.Move2_PP, (byte)pk.Move3_PP, (byte)pk.Move4_PP],
            PpUps = [(byte)pk.Move1_PPUps, (byte)pk.Move2_PPUps, (byte)pk.Move3_PPUps, (byte)pk.Move4_PPUps],
            HeldItem = (ushort)pk.HeldItem,
            Ball = pk.Ball,
            Ivs = new StatSet(pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE),
            Evs = new StatSet(pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPA, pk.EV_SPD, pk.EV_SPE),
            Nature = (byte)pk.StatAlignment,
            PidNature = (byte)pk.Nature,
            AbilitySlot = (byte)pk.AbilityNumber,
            Ability = (ushort)pk.Ability,
            IsEgg = pk.IsEgg,
            ShinyXor = pk.ShinyXor,
            IsShiny = pk.IsShiny,
            Friendship = pk.CurrentFriendship,
            ChecksumValid = true,
            Party = hasParty
                ? new HackPartyStats((uint)pk.Status_Condition, pk.Stat_Level, pk.Stat_HPCurrent,
                    new StatSet(pk.Stat_HPMax, pk.Stat_ATK, pk.Stat_DEF, pk.Stat_SPA, pk.Stat_SPD, pk.Stat_SPE))
                : null,
            Raw = record.ToArray(),
        };
    }

    private static bool IsBlank(ReadOnlySpan<byte> record) => record.IndexOfAnyExcept((byte)0) < 0;
}
