using System;
using System.Diagnostics.CodeAnalysis;

namespace PKHeX.Core;

// PKHaX: one-press "max everything" used by both PKHaX.exe and PKHaX Mobile, so the two stay identical.
/// <summary>
/// Applies every maxed-out value a Pokemon can hold in one pass: 252 EVs and 31 IVs across the board,
/// every ribbon and mark, both memory ribbon counters, the OT/HT memory blocks and affection, contest
/// stats, Pokerus, and shininess.
/// </summary>
/// <remarks>
/// This is deliberately ILLEGAL: 252 EVs in all six stats is 1512 total against a legal cap of 510, so a
/// treated Pokemon will fail legality. That is the point of the button — it is only offered in HaX mode.
/// Every block is guarded by the interface that owns it, so formats lacking a feature are skipped rather
/// than throwing.
/// </remarks>
public static class MaxHax
{
    /// <summary>Full EV value written to each stat (above the 510 legal total on purpose).</summary>
    public const int EV = 252;

    /// <summary>Contest stat and affection ceiling.</summary>
    public const byte Max = byte.MaxValue;

    [RequiresUnreferencedCode("Ribbon enumeration reflects over Ribbon* properties on the PKM type.")]
    public static void Apply(PKM pk)
    {
        ArgumentNullException.ThrowIfNull(pk);

        ApplyStats(pk);
        ApplyRibbons(pk);
        ApplyMemories(pk);
        ApplyContest(pk);
        ApplyPokerus(pk);

        pk.SetIsShiny(true);
        pk.RefreshChecksum();
    }

    private static void ApplyStats(PKM pk)
    {
        pk.EV_HP = pk.EV_ATK = pk.EV_DEF = pk.EV_SPA = pk.EV_SPD = pk.EV_SPE = EV;

        var iv = pk.MaxIV;
        pk.IV_HP = pk.IV_ATK = pk.IV_DEF = pk.IV_SPA = pk.IV_SPD = pk.IV_SPE = iv;

        if (pk is IAwakened av)
            av.AV_HP = av.AV_ATK = av.AV_DEF = av.AV_SPA = av.AV_SPD = av.AV_SPE = 200;

        if (pk is IHyperTrain ht)
            ht.HT_HP = ht.HT_ATK = ht.HT_DEF = ht.HT_SPA = ht.HT_SPD = ht.HT_SPE = true;

        pk.ResetPartyStats();
    }

    [RequiresUnreferencedCode("Reflects over Ribbon* properties on the PKM type.")]
    private static void ApplyRibbons(PKM pk)
    {
        foreach (var rib in RibbonInfo.GetRibbonInfo(pk))
        {
            if (rib.Type is RibbonValueType.Boolean)
                ReflectUtil.SetValue(pk, rib.Name, true);
            else
                ReflectUtil.SetValue(pk, rib.Name, (byte)rib.MaxCount);
        }

        // The two memory ribbon counters carry their own caps (contest 40, battle 8) and a paired flag.
        if (pk is IRibbonSetMemory6 m6)
        {
            m6.RibbonCountMemoryContest = 40;
            m6.RibbonCountMemoryBattle = 8;
            m6.HasContestMemoryRibbon = true;
            m6.HasBattleMemoryRibbon = true;
        }
    }

    private static void ApplyMemories(PKM pk)
    {
        if (pk is IMemoryOT ot)
        {
            ot.OriginalTrainerMemory = Max;
            ot.OriginalTrainerMemoryIntensity = Max;
            ot.OriginalTrainerMemoryFeeling = Max;
            ot.OriginalTrainerMemoryVariable = ushort.MaxValue;
        }

        if (pk is IMemoryHT htm)
        {
            htm.HandlingTrainerMemory = Max;
            htm.HandlingTrainerMemoryIntensity = Max;
            htm.HandlingTrainerMemoryFeeling = Max;
            htm.HandlingTrainerMemoryVariable = ushort.MaxValue;
        }

        if (pk is IAffection af)
        {
            af.OriginalTrainerAffection = Max;
            af.HandlingTrainerAffection = Max;
        }

        pk.CurrentFriendship = Max;
    }

    private static void ApplyContest(PKM pk)
    {
        if (pk is not IContestStats cs)
            return;

        cs.ContestCool = cs.ContestBeauty = cs.ContestCute = Max;
        cs.ContestSmart = cs.ContestTough = cs.ContestSheen = Max;
    }

    private static void ApplyPokerus(PKM pk)
    {
        // Strain 0xF with 4 days left is the strongest still-infectious state; days 0 would read as cured.
        pk.PokerusStrain = 0xF;
        pk.PokerusDays = 4;
    }
}
