using System;

namespace PKHeX.Core;

/// <summary>
/// PKHaX: shared text-format helpers for the Gen 1/2 "desync" hackmons Showdown export/import
/// (custom disguise sprite, custom typing, pre-applied status condition). See DESYNC-FORMAT.md.
/// </summary>
/// <remarks>
/// The canonical export adds up to three lines after the first/ability line:
/// <c>Sprite: &lt;Species&gt;</c>, <c>Types: &lt;Type1&gt;[ / &lt;Type2&gt;]</c>, <c>Status: &lt;Word&gt;</c>.
/// Sprite/Types are Gen-1 only; Status applies to Gen 1 and Gen 2.
/// </remarks>
public static class GBHaxFormat
{
    private const StringComparison IC = StringComparison.OrdinalIgnoreCase;

    // Gen-1 stores typing as its own internal byte values (NOT PKHeX's modern type indices).
    private static readonly (string Name, byte Value)[] G1Types =
    [
        ("Normal", 0), ("Fighting", 1), ("Flying", 2), ("Poison", 3), ("Ground", 4), ("Rock", 5),
        ("Bug", 7), ("Ghost", 8), ("Fire", 20), ("Water", 21), ("Grass", 22), ("Electric", 23),
        ("Psychic", 24), ("Ice", 25), ("Dragon", 26),
    ];

    /// <summary>Gets the display name for a Gen-1 internal type byte, or empty if unknown.</summary>
    public static string GetG1TypeName(byte type)
    {
        foreach (var (name, value) in G1Types)
        {
            if (value == type)
                return name;
        }
        return string.Empty;
    }

    /// <summary>Parses a type display name into its Gen-1 internal type byte.</summary>
    public static bool TryGetG1TypeByte(ReadOnlySpan<char> name, out byte type)
    {
        name = name.Trim();
        foreach (var (n, value) in G1Types)
        {
            if (name.Equals(n, IC))
            {
                type = value;
                return true;
            }
        }
        type = 0;
        return false;
    }

    /// <summary>Canonical status word for a Gen 1-4 status byte (empty if none).</summary>
    public static string GetStatusWord(int statusByte)
    {
        var v = (StatusCondition)(statusByte & 0xFF);
        if (v == StatusCondition.None)
            return string.Empty;
        if (v <= StatusCondition.Sleep7)
            return "Sleep";
        if ((v & StatusCondition.Paralysis) != 0)
            return "Paralysis";
        if ((v & StatusCondition.Burn) != 0)
            return "Burn";
        if ((v & (StatusCondition.Poison | StatusCondition.PoisonBad)) != 0)
            return "Poison";
        if ((v & StatusCondition.Freeze) != 0)
            return "Freeze";
        return string.Empty;
    }

    /// <summary>Parses a status word (or 3-letter code) into a Gen 1-4 status byte.</summary>
    public static bool TryGetStatusByte(ReadOnlySpan<char> word, out byte status)
    {
        word = word.Trim();
        status = 0;
        if (word.Equals("Sleep", IC) || word.Equals("slp", IC))
            status = (byte)StatusCondition.Sleep2; // a non-zero sleep counter; game just needs "asleep"
        else if (word.Equals("Poison", IC) || word.Equals("psn", IC))
            status = (byte)StatusCondition.Poison;
        else if (word.Equals("Burn", IC) || word.Equals("brn", IC))
            status = (byte)StatusCondition.Burn;
        else if (word.Equals("Freeze", IC) || word.Equals("frz", IC))
            status = (byte)StatusCondition.Freeze;
        else if (word.Equals("Paralysis", IC) || word.Equals("par", IC))
            status = (byte)StatusCondition.Paralysis;
        else
            return false;
        return true;
    }
}
