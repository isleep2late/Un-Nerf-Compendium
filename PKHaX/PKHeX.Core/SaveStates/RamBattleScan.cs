using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace PKHeX.Core;

/// PKHaX: locates the battle-engine structures inside a save state taken mid-battle, so the
/// volatile battle typing can be edited. Gen 3 stores four 0x58-byte BattlePokemon structs
/// contiguously (types at +0x21/+0x22); Gen 4/5 use 0xA8-byte BattleMon structs (types at +0x24/+0x25).
public sealed class RamBattleEntry
{
    public required int Species { get; init; }
    public required int CurrentHP { get; init; }
    /// Absolute offsets of the type1 byte in each copy of this battler (type2 = +1). Gen 6/7 double-buffers.
    public required List<int> Type1Offsets { get; init; }
}

public sealed class RamBattle
{
    public required SaveStateFile State { get; init; }
    public required byte Generation { get; init; }

    // Contiguous-array layout (Gen 3/4/5): battler N at Offset + N*StructSize, type pair at +TypeOffset.
    public int Offset { get; init; }
    public int StructSize { get; init; }
    public int TypeOffset { get; init; }

    // Explicit layout (Gen 6/7): each battler's type bytes live at scattered, double-buffered offsets.
    public IReadOnlyList<RamBattleEntry>? Entries { get; init; }

    public int Battlers => Entries?.Count ?? BattlerCount;
    public int BattlerCount { get; init; }

    public int GetSpecies(int battler)
    {
        if (Entries is not null)
            return Entries[battler].Species;
        int at = Offset + (battler * StructSize);
        return BinaryPrimitives.ReadUInt16LittleEndian(State.State.AsSpan(at, 2));
    }

    public bool IsPresent(int battler) => GetSpecies(battler) != 0;

    public (byte Type1, byte Type2) GetTypes(int battler)
    {
        int at = Entries is not null ? Entries[battler].Type1Offsets[0] : Offset + (battler * StructSize) + TypeOffset;
        return (State.State[at], State.State[at + 1]);
    }

    public void SetTypes(int battler, byte type1, byte type2)
    {
        var data = State.State;
        if (Entries is not null)
        {
            foreach (var at in Entries[battler].Type1Offsets)
            {
                data[at] = type1;
                data[at + 1] = type2;
            }
            return;
        }
        int off = Offset + (battler * StructSize) + TypeOffset;
        data[off] = type1;
        data[off + 1] = type2;
    }

    public (byte Value, string Name)[] TypeTable => Generation >= 6 ? TypeNames6 : TypeNames;

    /// Gen 3 internal type ids (shared by Gen 4/5): Normal..Steel are 0-8, ??? is 9, Fire.. from 10.
    public static readonly (byte Value, string Name)[] TypeNames =
    [
        (0, "Normal"), (1, "Fighting"), (2, "Flying"), (3, "Poison"), (4, "Ground"), (5, "Rock"),
        (6, "Bug"), (7, "Ghost"), (8, "Steel"), (9, "???"), (10, "Fire"), (11, "Water"),
        (12, "Grass"), (13, "Electric"), (14, "Psychic"), (15, "Ice"), (16, "Dragon"), (17, "Dark"),
    ];

    /// Gen 6/7 type ids: Fairy added at 17, ??? removed, Fire..Dark renumbered 9..16.
    public static readonly (byte Value, string Name)[] TypeNames6 =
    [
        (0, "Normal"), (1, "Fighting"), (2, "Flying"), (3, "Poison"), (4, "Ground"), (5, "Rock"),
        (6, "Bug"), (7, "Ghost"), (8, "Steel"), (9, "Fire"), (10, "Water"), (11, "Grass"),
        (12, "Electric"), (13, "Psychic"), (14, "Ice"), (15, "Dragon"), (16, "Dark"), (17, "Fairy"),
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
        if (state.Console == StateConsole.N3DS)
            return FindGen67(state);
        return null;
    }

    // Gen 6/7 (SM/USUM), verified live on Azahar: the battle engine caches each battler in a BTL_POKEPARAM
    // block. Within one 0x330 per-mon block the decrypted species (u16 @+0x00) is repeated as monsno
    // (u16 @+0x1CC), and the live types sit at +0x1D8/+0x1D9 (= BTL_POKEPARAM +0x0C/+0x0D). The whole
    // array is double-buffered at +0x6FE8, so every copy must be written. Located by the species==monsno
    // repeat, which is essentially coincidence-proof.
    private const int G67MonBlock = 0x330;
    private const int G67MonsnoDelta = 0x1CC;
    private const int G67TypeDelta = 0x1D8;
    private const int G67LevelDelta = 0x0C;

    private static RamBattle? FindGen67(SaveStateFile state)
    {
        var data = state.State;
        int end = data.Length - (G67TypeDelta + 2);
        var blocks = new List<(int Species, byte Level, int CurHP, int Type1)>();
        for (int pos = 0; pos <= end; pos += 4)
        {
            int species = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos, 2));
            if (species is < 1 or > 809)
                continue;
            if (BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos + G67MonsnoDelta, 2)) != species)
                continue;
            byte level = data[pos + G67LevelDelta];
            if (level is < 1 or > 100)
                continue;
            int maxHP = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos + 2, 2));
            int curHP = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(pos + 4, 2));
            if (maxHP is < 1 or > 2000 || curHP > maxHP)
                continue;
            byte t1 = data[pos + G67TypeDelta];
            byte t2 = data[pos + G67TypeDelta + 1];
            if (t1 > 17 || t2 > 17)
                continue;
            // stat stages (7 bytes at BTL_POKEPARAM +0x12) are each 0..12
            bool stagesOk = true;
            for (int i = 0; i < 7; i++)
            {
                if (data[pos + G67MonsnoDelta + 0x12 + i] > 12) { stagesOk = false; break; }
            }
            if (!stagesOk)
                continue;
            // A real battler's cached types equal its species' Personal-data types at battle start.
            // (Arceus/Silvally derive type from a held Plate/Memory, so accept any valid pair for them.)
            if (species is not (493 or 773) && !TypesMatchPersonal(species, t1, t2))
                continue;
            blocks.Add((species, level, curHP, pos + G67TypeDelta));
        }
        if (blocks.Count == 0)
            return null;

        // Group double-buffer/duplicate copies of the same battler (same species+level+curHP).
        var groups = new List<RamBattleEntry>();
        foreach (var b in blocks)
        {
            var existing = groups.Find(g => g.Species == b.Species && g.CurrentHP == b.CurHP && SameLevel(g, b, data));
            if (existing is not null)
            {
                existing.Type1Offsets.Add(b.Type1);
                continue;
            }
            groups.Add(new RamBattleEntry { Species = b.Species, CurrentHP = b.CurHP, Type1Offsets = [b.Type1] });
        }
        // On-field battlers first; keep the display manageable.
        if (groups.Count > 8)
            groups = groups.GetRange(0, 8);
        return new RamBattle { State = state, Generation = 7, Entries = groups };
    }

    private static bool TypesMatchPersonal(int species, byte t1, byte t2)
    {
        var pt = PersonalTable.USUM;
        if ((uint)species >= pt.MaxSpeciesID)
            return false;
        var pi = pt[species];
        byte p1 = (byte)pi.Type1;
        byte p2 = (byte)pi.Type2;
        return (t1 == p1 && t2 == p2) || (t1 == p2 && t2 == p1);
    }

    private static bool SameLevel(RamBattleEntry g, (int Species, byte Level, int CurHP, int Type1) b, byte[] data)
        => data[g.Type1Offsets[0] - G67TypeDelta + G67LevelDelta] == b.Level;

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
                BattlerCount = 4,
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
                BattlerCount = 4,
                Generation = party.Generation,
            };
        }
        return null;
    }

    private static bool IsValidTypePair(byte type1, byte type2) => type1 <= 17 && type2 <= 17;
}
