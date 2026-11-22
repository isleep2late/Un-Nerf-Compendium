using System.Collections.Generic;
using System.Globalization;
using static PKHeX.Core.EnigmaBerryParameterMeaning;

namespace PKHeX.Core;

public enum EnigmaBerryParameterMeaning : byte
{
    Unused,
    PercentChance,
    PercentBoost,
    PercentEvasion,
    FlatHp,
    FlatPp,
    HpDivisorHeal,
    HpDivisorTrigger,
    DamageDivisorHeal,
}

public sealed record EnigmaBerryEffect(byte Id, string Name, string StockItem, EnigmaBerryParameterMeaning Meaning, byte StockParameter)
{
    public bool Known => Id < EnigmaBerryEffects.Count;

    public bool ParameterUsed => Meaning != Unused;

    public string Label => Known ? $"{Id} · {Name}" : $"{Id} · {EnigmaBerryEffects.UnknownName}";

    public string ParameterLabel => EnigmaBerryEffects.ParameterLabel(this);

    public string Help => EnigmaBerryEffects.Help(this);

    public string Describe(byte parameter) => EnigmaBerryEffects.Describe(this, parameter);
}

public sealed record EnigmaBerryPreset(string Name, byte HoldEffect, byte Parameter, string Note);

public static class EnigmaBerryEffects
{
    public const int Count = 67;
    public const byte None = 0;
    public const byte Evasion = 22;
    public const byte QuickClaw = 26;
    public const byte Flinch = 30;
    public const byte Repel = 33;
    public const byte FocusBand = 39;
    public const byte ShellBell = 62;

    public const string UnknownName = "Unknown effect";

    public const string UnknownWarning =
        "The retail games define hold effects 1 to 66. This number is not one of them, and what the game does with it has not been checked.";

    public const string SamePriority = "Only among moves of the same priority.";

    private static readonly EnigmaBerryEffect[] Table =
    [
        new(0, "None", "", Unused, 0),
        new(1, "Restore HP at half HP (eaten)", "Oran Berry", FlatHp, 10),
        new(2, "Cure paralysis (eaten)", "Cheri Berry", Unused, 0),
        new(3, "Cure sleep (eaten)", "Chesto Berry", Unused, 0),
        new(4, "Cure poison (eaten)", "Pecha Berry", Unused, 0),
        new(5, "Cure burn (eaten)", "Rawst Berry", Unused, 0),
        new(6, "Cure freeze (eaten)", "Aspear Berry", Unused, 0),
        new(7, "Restore PP when a move runs out (eaten)", "Leppa Berry", FlatPp, 10),
        new(8, "Cure confusion (eaten)", "Persim Berry", Unused, 0),
        new(9, "Cure any status (eaten)", "Lum Berry", Unused, 0),
        new(10, "Heal, confuses if spicy is disliked (eaten)", "Figy Berry", HpDivisorHeal, 8),
        new(11, "Heal, confuses if dry is disliked (eaten)", "Wiki Berry", HpDivisorHeal, 8),
        new(12, "Heal, confuses if sweet is disliked (eaten)", "Mago Berry", HpDivisorHeal, 8),
        new(13, "Heal, confuses if bitter is disliked (eaten)", "Aguav Berry", HpDivisorHeal, 8),
        new(14, "Heal, confuses if sour is disliked (eaten)", "Iapapa Berry", HpDivisorHeal, 8),
        new(15, "Raise Attack in a pinch (eaten)", "Liechi Berry", HpDivisorTrigger, 4),
        new(16, "Raise Defense in a pinch (eaten)", "Ganlon Berry", HpDivisorTrigger, 4),
        new(17, "Raise Speed in a pinch (eaten)", "Salac Berry", HpDivisorTrigger, 4),
        new(18, "Raise Sp. Atk in a pinch (eaten)", "Petaya Berry", HpDivisorTrigger, 4),
        new(19, "Raise Sp. Def in a pinch (eaten)", "Apicot Berry", HpDivisorTrigger, 4),
        new(20, "Raise the critical-hit rate in a pinch (eaten)", "Lansat Berry", HpDivisorTrigger, 4),
        new(21, "Raise a random stat in a pinch (eaten)", "Starf Berry", HpDivisorTrigger, 4),
        new(22, "Evasion up", "BrightPowder", PercentEvasion, 10),
        new(23, "Restore lowered stats (consumed)", "White Herb", Unused, 0),
        new(24, "Macho Brace: halves Speed, doubles EVs", "Macho Brace", Unused, 0),
        new(25, "Exp. Share", "Exp. Share", Unused, 0),
        new(26, "Quick Claw: chance to move first", "Quick Claw", PercentChance, 20),
        new(27, "Friendship rises faster", "Soothe Bell", Unused, 0),
        new(28, "Cure infatuation (consumed)", "Mental Herb", Unused, 0),
        new(29, "Choice Band: 1.5x Attack, one move", "Choice Band", Unused, 0),
        new(30, "King's Rock: chance to flinch the target", "King's Rock", PercentChance, 10),
        new(31, "Bug power", "SilverPowder", PercentBoost, 10),
        new(32, "Double prize money", "Amulet Coin", Unused, 10),
        new(33, "Repel (does nothing on the berry)", "Cleanse Tag", Unused, 0),
        new(34, "Soul Dew (Latias and Latios only)", "Soul Dew", Unused, 0),
        new(35, "DeepSeaTooth (Clamperl only)", "DeepSeaTooth", Unused, 0),
        new(36, "DeepSeaScale (Clamperl only)", "DeepSeaScale", Unused, 0),
        new(37, "Can always run from wild battles", "Smoke Ball", Unused, 0),
        new(38, "Prevent evolution", "Everstone", Unused, 0),
        new(39, "Focus Band: chance to survive at 1 HP", "Focus Band", PercentChance, 10),
        new(40, "Lucky Egg: more experience", "Lucky Egg", Unused, 0),
        new(41, "Scope Lens: one critical-hit stage", "Scope Lens", Unused, 0),
        new(42, "Steel power", "Metal Coat", PercentBoost, 10),
        new(43, "Leftovers: heals 1/16 of max HP each turn", "Leftovers", Unused, 10),
        new(44, "Dragon Scale (no battle effect)", "Dragon Scale", Unused, 10),
        new(45, "Light Ball (Pikachu only)", "Light Ball", Unused, 0),
        new(46, "Ground power", "Soft Sand", PercentBoost, 10),
        new(47, "Rock power", "Hard Stone", PercentBoost, 10),
        new(48, "Grass power", "Miracle Seed", PercentBoost, 10),
        new(49, "Dark power", "BlackGlasses", PercentBoost, 10),
        new(50, "Fighting power", "Black Belt", PercentBoost, 10),
        new(51, "Electric power", "Magnet", PercentBoost, 10),
        new(52, "Water power", "Mystic Water", PercentBoost, 10),
        new(53, "Flying power", "Sharp Beak", PercentBoost, 10),
        new(54, "Poison power", "Poison Barb", PercentBoost, 10),
        new(55, "Ice power", "NeverMeltIce", PercentBoost, 10),
        new(56, "Ghost power", "Spell Tag", PercentBoost, 10),
        new(57, "Psychic power", "TwistedSpoon", PercentBoost, 10),
        new(58, "Fire power", "Charcoal", PercentBoost, 10),
        new(59, "Dragon power", "Dragon Fang", PercentBoost, 10),
        new(60, "Normal power", "Silk Scarf", PercentBoost, 10),
        new(61, "Up-Grade (no battle effect)", "Up-Grade", Unused, 0),
        new(62, "Shell Bell: heals from damage dealt", "Shell Bell", DamageDivisorHeal, 8),
        new(63, "Lucky Punch (Chansey only)", "Lucky Punch", Unused, 0),
        new(64, "Metal Powder (Ditto only)", "Metal Powder", Unused, 0),
        new(65, "Thick Club (Cubone and Marowak only)", "Thick Club", Unused, 0),
        new(66, "Stick (Farfetch'd only)", "Stick", Unused, 0),
    ];

    public static IReadOnlyList<EnigmaBerryEffect> All => Table;

    public static bool IsKnown(byte id) => id < Table.Length;

    public static EnigmaBerryEffect Get(byte id) => id < Table.Length ? Table[id] : new EnigmaBerryEffect(id, UnknownName, "", Unused, 0);

    public static EnigmaBerryPreset QuickClawAlways { get; } = new("Quick Claw, always first", QuickClaw, 101,
        "Uses 101, because 100 still fails one turn in 65536. First only among moves of the same priority: "
        + "a move of higher priority, a switch, an item or running still goes before the holder.");

    public static EnigmaBerryPreset QuickClawDial { get; } = new("Quick Claw, 100 on the dial", QuickClaw, 100,
        "Fails one turn in 65536. First only among moves of the same priority.");

    public static EnigmaBerryPreset FocusBandAlways { get; } = new("Focus Band, always holds", FocusBand, 100,
        "Survives every hit of a move's direct damage at 1 HP while no Substitute is up. Poison, burn, weather, recoil, "
        + "Leech Seed, Perish Song and Destiny Bond still knock the holder out.");

    public static EnigmaBerryPreset KingsRockAlways { get; } = new("King's Rock, always flinches", Flinch, 100,
        "Needs the holder to move first and a damaging move that King's Rock affects.");

    public static EnigmaBerryPreset StockQuickClaw { get; } = new("Stock Quick Claw", QuickClaw, 20,
        "The number the Quick Claw item uses. First only among moves of the same priority.");

    public static EnigmaBerryPreset Clear { get; } = new("Clear (no hold effect)", None, 0,
        "The berry does nothing when held.");

    public static IReadOnlyList<EnigmaBerryPreset> Presets { get; } =
        [QuickClawAlways, QuickClawDial, FocusBandAlways, KingsRockAlways, StockQuickClaw, Clear];

    public static EnigmaBerryPreset Stock(byte id)
    {
        var effect = Get(id);
        var item = effect.StockItem.Length == 0 ? effect.Name : effect.StockItem;
        return new EnigmaBerryPreset($"Stock effect of {item}", effect.Id, effect.StockParameter,
            $"The hold effect and the number the {item} item uses.");
    }

    public static string ParameterLabel(EnigmaBerryEffect effect) => effect.Meaning switch
    {
        PercentChance => "Chance, percent",
        PercentBoost => "Power added, percent",
        PercentEvasion => "Accuracy taken off, percent",
        FlatHp => "HP restored",
        FlatPp => "PP restored",
        HpDivisorHeal => "Heal divisor",
        HpDivisorTrigger => "Trigger divisor",
        DamageDivisorHeal => "Heal divisor",
        _ => "Parameter (not read)",
    };

    public static string Help(EnigmaBerryEffect effect) => effect.Meaning switch
    {
        PercentChance when effect.Id == QuickClaw =>
            "Chance to move first, percent. 100 fails one turn in 65536; 101 or more never fails. " + SamePriority,
        PercentChance when effect.Id == FocusBand =>
            "Chance to survive a knockout from a move's direct damage at 1 HP, percent. 100 or more never fails.",
        PercentChance => "Chance to flinch the target, percent.",
        PercentBoost => "Power added to moves of this type, percent.",
        PercentEvasion =>
            "Percent taken off the attacker's accuracy. 100 makes every accuracy-checked move miss. "
            + "Above 100 the number wraps: accurate moves always hit the holder and inaccurate ones always miss.",
        FlatHp => "HP restored when the holder is at half HP or less.",
        FlatPp => "PP restored to a move that has run out.",
        HpDivisorHeal => "Heals max HP divided by this number. With 0 it heals 1 HP.",
        HpDivisorTrigger => "Triggers at max HP divided by this number. With 0 it never triggers.",
        DamageDivisorHeal => "Heals damage dealt divided by this number. 1 heals all of it. With 0 it heals 1 HP.",
        _ when !effect.Known => UnknownWarning,
        _ when effect.Id == Repel =>
            "No effect on the berry: the game tests for the Cleanse Tag item itself, not for this hold effect. The stored number is kept.",
        _ => "This effect does not read the parameter. The stored number is kept.",
    };

    public static string Describe(byte id, byte parameter) => Describe(Get(id), parameter);

    public static string Describe(EnigmaBerryEffect effect, byte parameter)
    {
        int p = parameter;
        var n = p.ToString(CultureInfo.InvariantCulture);
        switch (effect.Meaning)
        {
            case PercentChance when effect.Id == QuickClaw:
                if (p == 0)
                    return "Never moves first through this effect. " + SamePriority;
                if (p < 100)
                    return $"{n} percent chance to move first. " + SamePriority;
                if (p == 100)
                    return "Fails one turn in 65536; on the rest it moves first. " + SamePriority;
                return "Always moves first. Never fails. " + SamePriority;
            case PercentChance when effect.Id == FocusBand:
                if (p == 0)
                    return "Never survives a knockout through this effect. It only ever applies to a move's direct damage.";
                if (p < 100)
                    return $"{n} percent chance, rolled for each hit, to survive a move's direct damage at 1 HP.";
                return "Always survives a move's direct damage at 1 HP. Never fails. "
                    + "Poison, burn, weather, recoil and other indirect damage still knock the holder out, and a Substitute takes the hit instead.";
            case PercentChance:
                if (p == 0)
                    return "Never flinches the target through this effect.";
                if (p < 100)
                    return $"{n} percent chance to flinch the target, when the holder moves first with a move King's Rock affects.";
                return "Always flinches the target, when the holder moves first with a move King's Rock affects.";
            case PercentBoost:
                if (p == 0)
                    return "No power added.";
                var factor = ((100 + p) / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
                return $"Adds {n} percent to the attacking stat for moves of this type ({factor}x). There is no cap.";
            case PercentEvasion:
                if (p == 0)
                    return "No change to the attacker's accuracy.";
                if (p < 100)
                    return $"Takes {n} percent off the attacker's accuracy.";
                if (p == 100)
                    return "Every accuracy-checked move aimed at the holder misses.";
                var least = (100 + (p - 100) - 1) / (p - 100);
                if (least <= 1)
                    return "Above 100 the game's number wraps: every accuracy-checked move always hits the holder.";
                var bar = least.ToString(CultureInfo.InvariantCulture);
                return $"Above 100 the game's number wraps: a move whose accuracy after stat stages is {bar} or more always hits the holder, "
                    + $"and one under {bar} always misses.";
            case FlatHp:
                return p == 0
                    ? "At half HP or less the berry is eaten and restores 0 HP."
                    : $"At half HP or less the berry is eaten and restores {n} HP.";
            case FlatPp:
                return p == 0
                    ? "When a move runs out of PP the berry is eaten and restores 0 PP."
                    : $"When a move runs out of PP the berry is eaten and restores {n} PP to it.";
            case HpDivisorHeal:
                if (p == 0)
                    return "With 0 it heals 1 HP: the game's divide returns 0 for a zero divisor and the heal is then raised to 1. Read from the game's source, not run.";
                return $"At half HP or less the berry is eaten and heals max HP divided by {n}, at least 1 HP. A Pokemon that dislikes the flavour becomes confused.";
            case HpDivisorTrigger:
                if (p == 0)
                    return "With 0 it never triggers: the game's divide returns 0 for a zero divisor, and a living Pokemon is never at 0 HP. Read from the game's source, not run.";
                if (p == 1)
                    return "Triggers at any HP, because max HP divided by 1 is max HP. The berry is eaten.";
                return $"Triggers when HP is at or under max HP divided by {n}. The berry is eaten.";
            case DamageDivisorHeal:
                if (p == 0)
                    return "With 0 it heals 1 HP for each hit: the game's divide returns 0 for a zero divisor and the heal is then raised to 1. Read from the game's source, not run.";
                if (p == 1)
                    return "Heals all of the damage dealt.";
                return $"Heals the damage dealt divided by {n}, at least 1 HP.";
            default:
                return Help(effect);
        }
    }
}
