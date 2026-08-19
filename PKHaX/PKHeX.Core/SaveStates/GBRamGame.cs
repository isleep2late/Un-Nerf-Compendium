using System;

namespace PKHeX.Core;

/// PKHaX: live-RAM party access for Gen 1/2 games inside an emulator save state.
public enum GBRamGameKind
{
    Gen1RedBlue,
    Gen1Yellow,
    Gen2GoldSilver,
    Gen2Crystal,
}

public sealed record GBRamLayout(
    GBRamGameKind Kind,
    string Name,
    int PartyCount,
    int PartyList,
    int PartyMons,
    int MonSize,
    int OTNames,
    int Nicknames,
    int BattleMonSpecies,
    int BattleMonType1,
    int EnemyMonSpecies,
    int EnemyMonType1,
    int BattleMonSpecies2,
    int EnemyMonSpecies2)
{
    public bool IsGen1 => Kind is GBRamGameKind.Gen1RedBlue or GBRamGameKind.Gen1Yellow;
    public int NameLength => 11;
}

public sealed class GBRamGame(SaveStateFile state, GBRamLayout layout)
{
    public static readonly GBRamLayout Gen1RedBlue = new(GBRamGameKind.Gen1RedBlue, "Red/Blue",
        0xD163, 0xD164, 0xD16B, 44, 0xD273, 0xD2B5,
        0xD014, 0xD019, 0xCFE5, 0xCFEA, 0xCFD9, 0xCFD8);

    public static readonly GBRamLayout Gen1Yellow = new(GBRamGameKind.Gen1Yellow, "Yellow",
        0xD162, 0xD163, 0xD16A, 44, 0xD272, 0xD2B4,
        0xD013, 0xD018, 0xCFE4, 0xCFE9, 0xCFD8, 0xCFD7);

    public static readonly GBRamLayout Gen2GoldSilver = new(GBRamGameKind.Gen2GoldSilver, "Gold/Silver",
        0xDA22, 0xDA23, 0xDA2A, 48, 0xDB4A, 0xDB8C,
        0xCB0C, 0xCB2A, 0xD0EF, 0xD10D, -1, -1);

    public static readonly GBRamLayout Gen2Crystal = new(GBRamGameKind.Gen2Crystal, "Crystal",
        0xDCD7, 0xDCD8, 0xDCDF, 48, 0xDDFF, 0xDE41,
        0xC62C, 0xC64A, 0xD206, 0xD224, -1, -1);

    private static readonly GBRamLayout[] Layouts = [Gen1RedBlue, Gen1Yellow, Gen2GoldSilver, Gen2Crystal];

    public SaveStateFile State { get; } = state;
    public GBRamLayout Layout { get; } = layout;
    private byte[] Data => State.State;

    public static GBRamGame? TryDetect(SaveStateFile state)
    {
        if (state.WramOffset < 0)
            return null;
        foreach (var layout in Layouts)
        {
            if (IsPartyShaped(state, layout))
                return new GBRamGame(state, layout);
        }
        return null;
    }

    private static bool IsPartyShaped(SaveStateFile state, GBRamLayout layout)
    {
        int countOffset = state.GetGBOffset(layout.PartyCount);
        int listOffset = state.GetGBOffset(layout.PartyList);
        int monsOffset = state.GetGBOffset(layout.PartyMons);
        if (countOffset < 0 || listOffset < 0 || monsOffset < 0)
            return false;
        var data = state.State;
        if (monsOffset + (6 * layout.MonSize) > data.Length)
            return false;
        int count = data[countOffset];
        if (count is < 1 or > 6)
            return false;
        if (data[listOffset + count] != 0xFF)
            return false;
        for (int i = 0; i < count; i++)
        {
            int species = data[listOffset + i];
            if (species is 0 or 0xFF)
                return false;
            int mon = monsOffset + (i * layout.MonSize);
            if (data[mon] != species)
                return false;
            int level = data[mon + (layout.IsGen1 ? 0x21 : 0x1F)];
            if (level == 0)
                return false;
        }
        return true;
    }

    public int PartyCount
    {
        get => Data[State.GetGBOffset(Layout.PartyCount)];
        set
        {
            int count = Math.Clamp(value, 0, 6);
            Data[State.GetGBOffset(Layout.PartyCount)] = (byte)count;
            int list = State.GetGBOffset(Layout.PartyList);
            for (int i = count; i < 6; i++)
                Data[list + i] = 0;
            Data[list + count] = 0xFF;
        }
    }

    public Span<byte> GetMonRaw(int slot) => Data.AsSpan(State.GetGBOffset(Layout.PartyMons) + (slot * Layout.MonSize), Layout.MonSize);
    public Span<byte> GetOTRaw(int slot) => Data.AsSpan(State.GetGBOffset(Layout.OTNames) + (slot * Layout.NameLength), Layout.NameLength);
    public Span<byte> GetNicknameRaw(int slot) => Data.AsSpan(State.GetGBOffset(Layout.Nicknames) + (slot * Layout.NameLength), Layout.NameLength);

    public byte GetListSpecies(int slot) => Data[State.GetGBOffset(Layout.PartyList) + slot];
    public void SetListSpecies(int slot, byte value) => Data[State.GetGBOffset(Layout.PartyList) + slot] = value;

    public PKM GetSlot(int slot)
    {
        if (Layout.IsGen1)
        {
            var pk = new PK1(GetMonRaw(slot), GetOTRaw(slot), GetNicknameRaw(slot))
            {
                HeaderSpeciesInternal = GetListSpecies(slot),
            };
            return pk;
        }
        return new PK2(GetMonRaw(slot), GetOTRaw(slot), GetNicknameRaw(slot));
    }

    public void SetSlot(int slot, PKM pk)
    {
        var raw = GetMonRaw(slot);
        pk.Data[..raw.Length].CopyTo(raw);
        if (pk is GBPKML gb)
        {
            if (gb.OriginalTrainerTrash.Length >= Layout.NameLength)
                gb.OriginalTrainerTrash[..Layout.NameLength].CopyTo(GetOTRaw(slot));
            if (gb.NicknameTrash.Length >= Layout.NameLength)
                gb.NicknameTrash[..Layout.NameLength].CopyTo(GetNicknameRaw(slot));
        }
        byte list = pk is PK1 { HeaderSpeciesInternal: not 0 } p1 ? p1.HeaderSpeciesInternal : raw[0];
        SetListSpecies(slot, list);
    }

    public bool HasBattleTypes => Layout.BattleMonType1 >= 0 && State.GetGBOffset(Layout.BattleMonType1) >= 0;

    public bool IsBattleActive
    {
        get
        {
            int species = GetBattleByte(Layout.BattleMonSpecies);
            if (species is <= 0 or 0xFF)
                return false;
            int count = PartyCount;
            for (int i = 0; i < count; i++)
            {
                if (GetMonRaw(i)[0] == species || GetListSpecies(i) == species)
                    return true;
            }
            return false;
        }
    }

    private int GetBattleByte(int address)
    {
        if (address < 0)
            return -1;
        int offset = State.GetGBOffset(address);
        return offset < 0 ? -1 : Data[offset];
    }

    private void SetBattleByte(int address, byte value)
    {
        int offset = State.GetGBOffset(address);
        if (offset >= 0)
            Data[offset] = value;
    }

    public (int Type1, int Type2) GetBattleTypes(bool enemy)
    {
        int address = enemy ? Layout.EnemyMonType1 : Layout.BattleMonType1;
        return (GetBattleByte(address), GetBattleByte(address + 1));
    }

    public void SetBattleTypes(bool enemy, byte type1, byte type2)
    {
        int address = enemy ? Layout.EnemyMonType1 : Layout.BattleMonType1;
        SetBattleByte(address, type1);
        SetBattleByte(address + 1, type2);
    }
}
