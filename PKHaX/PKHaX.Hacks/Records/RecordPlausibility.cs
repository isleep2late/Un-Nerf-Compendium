using System;

namespace PKHaX.Hacks.Records;

public static class RecordPlausibility
{
    public const string Prefix = "implausible: ";
    public const int CfruSpeciesCap = 0x7FF;
    public const int MaxLevel = 100;
    public const int MaxStat = 999;
    public const int MaxGen3Pp = 64;
    public const uint GbMaxExp = 1_250_000;

    public static string? Gen3Header(int language, byte flags, uint ivWord)
    {
        if (language is < 1 or > 7)
            return $"{Prefix}language {language} is outside 1..7";
        if ((flags & 1) != 0)
            return $"{Prefix}the bad-egg flag is set (flags 0x{flags:X2})";
        if ((flags & 2) == 0)
            return $"{Prefix}the has-species flag is clear (flags 0x{flags:X2})";
        if (((flags >> 2) & 1) != ((ivWord >> 30) & 1))
            return $"{Prefix}the header egg flag (flags 0x{flags:X2}) and the IV-word egg bit disagree";
        return null;
    }

    public static string? Gen3Party(HackPartyStats party)
    {
        if (party.Level is < 1 or > MaxLevel)
            return $"{Prefix}party level {party.Level} is outside 1..{MaxLevel}";
        return Stats(party);
    }

    public static string? GbParty(HackPartyStats party)
    {
        if (party.Level < 1)
            return $"{Prefix}party level 0";
        return Stats(party);
    }

    public static string? GbLevelAndExp(byte level, uint exp)
    {
        if (level < 1)
            return $"{Prefix}level 0";
        if (exp > GbMaxExp)
            return $"{Prefix}EXP {exp} is above the level-100 EXP of the slowest growth rate ({GbMaxExp})";
        var floor = GbMinExp(Math.Min((int)level, MaxLevel));
        if (exp < floor)
            return $"{Prefix}EXP {exp} is below the least EXP any growth rate needs for level {level} ({floor})";
        return null;
    }

    public static string? GbStatus(byte status)
    {
        if (status is 0 or 8 or 16 or 32 or 64 || status is >= 1 and <= 7)
            return null;
        return $"{Prefix}status 0x{status:X2} is not one condition";
    }

    public static string? Gen2Pokerus(byte pokerus)
    {
        var strain = pokerus >> 4;
        var days = pokerus & 0xF;
        var max = (strain & 3) + 1;
        return days <= max ? null : $"{Prefix}Pokerus 0x{pokerus:X2} has {days} days left, its strain allows {max}";
    }

    public static string? Gen3Pp(ReadOnlySpan<byte> pp, byte mask)
    {
        for (var i = 0; i < pp.Length; i++)
        {
            if ((pp[i] & mask) > MaxGen3Pp)
                return $"{Prefix}move {i + 1} has {pp[i] & mask} PP, above {MaxGen3Pp}";
        }
        return null;
    }

    public static uint GbMinExp(int level)
    {
        if (level <= 1)
            return 0;
        long n = level;
        long cube = n * n * n;
        long[] curves =
        [
            cube,
            (3 * cube / 4) + (10 * n * n) - 30,
            (3 * cube / 4) + (20 * n * n) - 70,
            (6 * cube / 5) - (15 * n * n) + (100 * n) - 140,
            4 * cube / 5,
            5 * cube / 4,
        ];
        var min = long.MaxValue;
        foreach (var c in curves)
            min = Math.Min(min, c);
        return (uint)Math.Max(0, min);
    }

    private static string? Stats(HackPartyStats party)
    {
        var s = party.Stats;
        if (s.Hp < 1)
            return $"{Prefix}max HP 0";
        if (party.CurrentHp > s.Hp)
            return $"{Prefix}current HP {party.CurrentHp} is above max HP {s.Hp}";
        for (var i = 0; i < 6; i++)
        {
            if (s[i] > MaxStat)
                return $"{Prefix}stat {i} is {s[i]}, above {MaxStat}";
        }
        return null;
    }
}
