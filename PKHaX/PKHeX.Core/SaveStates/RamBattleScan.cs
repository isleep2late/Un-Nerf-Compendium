using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace PKHeX.Core;

/// PKHaX: locates the battle-engine structures inside a save state taken mid-battle, so the
/// volatile battle typing can be edited. Gen 3 stores four 0x58-byte BattlePokemon structs
/// contiguously (types at +0x21/+0x22); Gen 4/5 use 0xA8-byte BattleMon structs (types at +0x24/+0x25).
public sealed class RamBattle
{
    public required SaveStateFile State { get; init; }
    public required int Offset { get; init; }
    public required int StructSize { get; init; }
    public required int TypeOffset { get; init; }
    public required int Battlers { get; init; }
    public required byte Generation { get; init; }

    public int GetSpecies(int battler)
    {
        int at = Offset + (battler * StructSize);
        return BinaryPrimitives.ReadUInt16LittleEndian(State.State.AsSpan(at, 2));
    }

    public bool IsPresent(int battler) => GetSpecies(battler) != 0;

    public (byte Type1, byte Type2) GetTypes(int battler)
    {
        int at = Offset + (battler * StructSize) + TypeOffset;
        return (State.State[at], State.State[at + 1]);
    }

    public void SetTypes(int battler, byte type1, byte type2)
    {
        int at = Offset + (battler * StructSize) + TypeOffset;
        State.State[at] = type1;
        State.State[at + 1] = type2;
    }

    /// Gen 3 internal type ids (shared by Gen 4/5): Normal..Steel are 0-8, ??? is 9, Fire.. from 10.
    public static readonly (byte Value, string Name)[] TypeNames =
    [
        (0, "Normal"), (1, "Fighting"), (2, "Flying"), (3, "Poison"), (4, "Ground"), (5, "Rock"),
        (6, "Bug"), (7, "Ghost"), (8, "Steel"), (9, "???"), (10, "Fire"), (11, "Water"),
        (12, "Grass"), (13, "Electric"), (14, "Psychic"), (15, "Ice"), (16, "Dragon"), (17, "Dark"),
    ];
}

public static class RamBattleScan
{
    /// Anchors on the already-located RAM party: the player's battle struct mirrors slot 0's
    /// species/level/max HP, with valid type bytes. Species here are the raw in-game ids.
    public static RamBattle? FindBattle(SaveStateFile state, IReadOnlyList<RamParty> parties)
    {
        foreach (var party in parties)
        {
            var result = party.Generation switch
            {
                3 => FindGen3(state, party),
                4 or 5 => FindGen45(state, party),
                _ => null,
            };
            if (result is not null)
                return result;
        }
        return null;
    }

    private static RamBattle? FindGen3(SaveStateFile state, RamParty party)
    {
        var pk = party.GetSlot(0);
        if (pk is not PK3 pk3)
            return null;
        ushort species = SpeciesConverter.GetInternal3(pk3.Species);
        byte level = pk3.Stat_Level;
        ushort maxHP = (ushort)pk3.Stat_HPMax;
        if (species == 0 || level == 0)
            return null;
        var data = state.State;
        int start = state.EwramOffset >= 0 ? state.EwramOffset : 0;
        int length = state.EwramOffset >= 0 ? 0x40000 : data.Length;
        int end = Math.Min(start + length, data.Length) - (4 * 0x58);
        for (int pos = start; pos <= end; pos += 2)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos, 2)) != species)
                continue;
            if (data[pos + 0x2A] != level)
                continue;
            if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos + 0x2C, 2)) != maxHP)
                continue;
            if (!IsValidTypePair(data[pos + 0x21], data[pos + 0x22]))
                continue;
            int foe = pos + 0x58;
            ushort foeSpecies = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(foe, 2));
            if (foeSpecies is 0 or > 440)
                continue;
            if (!IsValidTypePair(data[foe + 0x21], data[foe + 0x22]))
                continue;
            return new RamBattle
            {
                State = state,
                Offset = pos,
                StructSize = 0x58,
                TypeOffset = 0x21,
                Battlers = 4,
                Generation = 3,
            };
        }
        return null;
    }

    private static RamBattle? FindGen45(SaveStateFile state, RamParty party)
    {
        var pk = party.GetSlot(0);
        ushort species = pk.Species;
        byte level = pk.Stat_Level;
        ushort maxHP = (ushort)pk.Stat_HPMax;
        if (species == 0 || level == 0)
            return null;
        var data = state.State;
        int end = data.Length - (4 * 0xA8);
        for (int pos = state.MainRamOffset >= 0 ? state.MainRamOffset : 0; pos <= end; pos += 4)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos, 2)) != species)
                continue;
            if (data[pos + 0x34] != level)
                continue;
            if (BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(pos + 0x50, 4)) != maxHP)
                continue;
            if (!IsValidTypePair(data[pos + 0x24], data[pos + 0x25]))
                continue;
            return new RamBattle
            {
                State = state,
                Offset = pos,
                StructSize = 0xA8,
                TypeOffset = 0x24,
                Battlers = 4,
                Generation = party.Generation,
            };
        }
        return null;
    }

    private static bool IsValidTypePair(byte type1, byte type2) => type1 <= 17 && type2 <= 17;
}
