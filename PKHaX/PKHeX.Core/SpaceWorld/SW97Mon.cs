using System;

namespace PKHeX.Core;

/// PKHaX: one 48 byte Space World '97 party entry, laid out exactly like retail Gen 2.
public sealed class SW97Mon(byte[] data, int offset, int otOffset, int nicknameOffset)
{
    public const int OffsetSpecies = 0x00;
    public const int OffsetItem = 0x01;
    public const int OffsetMoves = 0x02;
    public const int OffsetOTID = 0x06;
    public const int OffsetExp = 0x08;
    public const int OffsetStatExp = 0x0B;
    public const int OffsetDVs = 0x15;
    public const int OffsetPP = 0x17;
    public const int OffsetHappiness = 0x1B;
    public const int OffsetLevel = 0x1F;
    public const int OffsetStatus = 0x20;
    public const int OffsetHP = 0x22;
    public const int OffsetMaxHP = 0x24;
    public const int OffsetStats = 0x26;

    public bool English { get; init; }

    private Span<byte> Raw => data.AsSpan(offset, SW97Save.MonSize);
    public Span<byte> OTNameRaw => data.AsSpan(otOffset, SW97Save.NameLength);
    public Span<byte> NicknameRaw => data.AsSpan(nicknameOffset, SW97Save.NameLength);

    private int GetBigEndian16(int index) => (Raw[index] << 8) | Raw[index + 1];

    private void SetBigEndian16(int index, int value)
    {
        int v = Math.Clamp(value, 0, 0xFFFF);
        Raw[index] = (byte)(v >> 8);
        Raw[index + 1] = (byte)(v & 0xFF);
    }

    public int Species { get => Raw[OffsetSpecies]; set => Raw[OffsetSpecies] = (byte)value; }
    public int HeldItem { get => Raw[OffsetItem]; set => Raw[OffsetItem] = (byte)value; }
    public int Level { get => Raw[OffsetLevel]; set => Raw[OffsetLevel] = (byte)value; }
    public int Happiness { get => Raw[OffsetHappiness]; set => Raw[OffsetHappiness] = (byte)value; }
    public int Status { get => Raw[OffsetStatus]; set => Raw[OffsetStatus] = (byte)value; }
    public int OTID { get => GetBigEndian16(OffsetOTID); set => SetBigEndian16(OffsetOTID, value); }

    public int DVs
    {
        get => GetBigEndian16(OffsetDVs);
        set => SetBigEndian16(OffsetDVs, value);
    }

    public int DVAttack => Raw[OffsetDVs] >> 4;
    public int DVDefense => Raw[OffsetDVs] & 0xF;
    public int DVSpeed => Raw[OffsetDVs + 1] >> 4;
    public int DVSpecial => Raw[OffsetDVs + 1] & 0xF;

    public int DVHP => ((DVAttack & 1) << 3) | ((DVDefense & 1) << 2) | ((DVSpeed & 1) << 1) | (DVSpecial & 1);

    public int GetMove(int index) => Raw[OffsetMoves + index];

    public void SetMove(int index, int move, int ppUps = 0)
    {
        Raw[OffsetMoves + index] = (byte)move;
        int ups = Math.Clamp(ppUps, 0, 3);
        Raw[OffsetPP + index] = move == 0 ? (byte)0 : (byte)(SW97Data.GetMaxPP(move, ups) | (ups << 6));
    }

    public int GetPP(int index) => Raw[OffsetPP + index] & 0x3F;
    public int GetPPUps(int index) => Raw[OffsetPP + index] >> 6;

    public int GetStatExp(int index) => GetBigEndian16(OffsetStatExp + (index * 2));
    public void SetStatExp(int index, int value) => SetBigEndian16(OffsetStatExp + (index * 2), value);

    public int MaxHP { get => GetBigEndian16(OffsetMaxHP); set => SetBigEndian16(OffsetMaxHP, value); }
    public int CurrentHP { get => GetBigEndian16(OffsetHP); set => SetBigEndian16(OffsetHP, value); }
    public int GetStat(int index) => GetBigEndian16(OffsetStats + (index * 2));
    public void SetStat(int index, int value) => SetBigEndian16(OffsetStats + (index * 2), value);

    public string Nickname
    {
        get => SW97Data.DecodeName(NicknameRaw, English);
        set => SW97Data.TryEncodeName(value, NicknameRaw, English);
    }

    public string OTName
    {
        get => SW97Data.DecodeName(OTNameRaw, English);
        set => SW97Data.TryEncodeName(value, OTNameRaw, English);
    }

    public int Type1 => SW97Data.SpeciesTypes[Species * 2];
    public int Type2 => SW97Data.SpeciesTypes[(Species * 2) + 1];

    public void Clear() => Raw.Clear();

    public void ApplyDisguise(int species, bool renameToSpecies)
    {
        Species = species;
        if (!renameToSpecies)
            return;
        var name = English ? SW97Data.SpeciesNames[species] : SW97Data.SpeciesNamesJapanese[species];
        if (name.Length != 0)
            SW97Data.TryEncodeName(name, NicknameRaw, English);
    }

    public int[] CalculateStats()
    {
        int level = Level;
        var result = new int[6];
        int[] dv = [DVHP, DVAttack, DVDefense, DVSpeed, DVSpecial, DVSpecial];
        int[] baseStat =
        [
            SW97Data.BaseStats[(Species * 6) + 0],
            SW97Data.BaseStats[(Species * 6) + 1],
            SW97Data.BaseStats[(Species * 6) + 2],
            SW97Data.BaseStats[(Species * 6) + 3],
            SW97Data.BaseStats[(Species * 6) + 4],
            SW97Data.BaseStats[(Species * 6) + 5],
        ];
        int[] exp = [GetStatExp(0), GetStatExp(1), GetStatExp(2), GetStatExp(3), GetStatExp(4), GetStatExp(4)];
        for (int i = 0; i < 6; i++)
        {
            int common = (((baseStat[i] + dv[i]) * 2) + (IntegerSquareRoot(exp[i]) / 4)) * level / 100;
            result[i] = i == 0 ? common + level + 10 : common + 5;
            if (result[i] > 0xFFFF)
                result[i] = 0xFFFF;
        }
        return result;
    }

    public void ApplyCalculatedStats()
    {
        var stats = CalculateStats();
        MaxHP = stats[0];
        CurrentHP = stats[0];
        for (int i = 0; i < 5; i++)
            SetStat(i, stats[i + 1]);
    }

    private static int IntegerSquareRoot(int value)
    {
        if (value <= 0)
            return 0;
        int root = (int)Math.Sqrt(value);
        while ((root + 1) * (root + 1) <= value)
            root++;
        while (root * root > value)
            root--;
        return root;
    }
}
