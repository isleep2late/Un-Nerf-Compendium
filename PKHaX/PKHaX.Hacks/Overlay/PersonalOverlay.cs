using System;
using PKHeX.Core;

namespace PKHaX.Hacks.Overlay;

public static class PersonalOverlay
{
    public static IDisposable Use(GameVersion version, Memory<byte> data)
    {
        var (context, table) = Create(version, data);
        return PersonalOverride.Use(context, table);
    }

    public static (EntityContext Context, IPersonalTable Table) Create(GameVersion version, Memory<byte> data)
    {
        var (context, size, stockCount) = Layout(version);
        if (data.Length == 0 || data.Length % size != 0)
            throw new ArgumentException($"Personal data for {version} must be a non-empty multiple of {size} bytes, got {data.Length}.", nameof(data));
        if (data.Length / size < stockCount)
            throw new ArgumentException($"Personal data for {version} has {data.Length / size} entries, fewer than the {stockCount} of the stock table.", nameof(data));
        return (context, Build(version, data));
    }

    public static EntityContext ContextOf(GameVersion version) => Layout(version).Context;

    private static (EntityContext Context, int Size, int StockCount) Layout(GameVersion version) => version switch
    {
        GameVersion.RD or GameVersion.GN or GameVersion.BU or GameVersion.RB => (EntityContext.Gen1, PersonalInfo1.SIZE, PersonalTable.StockRB.Count),
        GameVersion.YW or GameVersion.RBY => (EntityContext.Gen1, PersonalInfo1.SIZE, PersonalTable.StockY.Count),
        GameVersion.GD or GameVersion.SI or GameVersion.GS => (EntityContext.Gen2, PersonalInfo2.SIZE, PersonalTable.StockGS.Count),
        GameVersion.C or GameVersion.GSC => (EntityContext.Gen2, PersonalInfo2.SIZE, PersonalTable.StockC.Count),
        GameVersion.R or GameVersion.S or GameVersion.RS => (EntityContext.Gen3, PersonalInfo3.SIZE, PersonalTable.StockRS.Count),
        GameVersion.E or GameVersion.RSE => (EntityContext.Gen3, PersonalInfo3.SIZE, PersonalTable.StockE.Count),
        GameVersion.FR or GameVersion.FRLG => (EntityContext.Gen3, PersonalInfo3.SIZE, PersonalTable.StockFR.Count),
        GameVersion.LG => (EntityContext.Gen3, PersonalInfo3.SIZE, PersonalTable.StockLG.Count),
        GameVersion.D or GameVersion.P or GameVersion.DP => (EntityContext.Gen4, PersonalInfo4.SIZE, PersonalTable.StockDP.Count),
        GameVersion.Pt or GameVersion.DPPt => (EntityContext.Gen4, PersonalInfo4.SIZE, PersonalTable.StockPt.Count),
        GameVersion.HG or GameVersion.SS or GameVersion.HGSS => (EntityContext.Gen4, PersonalInfo4.SIZE, PersonalTable.StockHGSS.Count),
        GameVersion.B or GameVersion.W or GameVersion.BW => (EntityContext.Gen5, PersonalInfo5BW.SIZE, PersonalTable.StockBW.Count),
        GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2 => (EntityContext.Gen5, PersonalInfo5B2W2.SIZE, PersonalTable.StockB2W2.Count),
        GameVersion.X or GameVersion.Y or GameVersion.XY => (EntityContext.Gen6, PersonalInfo6XY.SIZE, PersonalTable.StockXY.Count),
        GameVersion.OR or GameVersion.AS or GameVersion.ORAS => (EntityContext.Gen6, PersonalInfo6AO.SIZE, PersonalTable.StockAO.Count),
        GameVersion.SN or GameVersion.MN or GameVersion.SM => (EntityContext.Gen7, PersonalInfo7.SIZE, PersonalTable.StockSM.Count),
        GameVersion.US or GameVersion.UM or GameVersion.USUM => (EntityContext.Gen7, PersonalInfo7.SIZE, PersonalTable.StockUSUM.Count),
        GameVersion.GP or GameVersion.GE or GameVersion.GG => (EntityContext.Gen7b, PersonalInfo7GG.SIZE, PersonalTable.StockGG.Count),
        GameVersion.SW or GameVersion.SH or GameVersion.SWSH => (EntityContext.Gen8, PersonalInfo8SWSH.SIZE, PersonalTable.StockSWSH.Count),
        GameVersion.BD or GameVersion.SP or GameVersion.BDSP => (EntityContext.Gen8b, PersonalInfo8BDSP.SIZE, PersonalTable.StockBDSP.Count),
        GameVersion.PLA => (EntityContext.Gen8a, PersonalInfo8LA.SIZE, PersonalTable.StockLA.Count),
        GameVersion.SL or GameVersion.VL or GameVersion.SV => (EntityContext.Gen9, PersonalInfo9SV.SIZE, PersonalTable.StockSV.Count),
        GameVersion.ZA => (EntityContext.Gen9a, PersonalInfo9ZA.SIZE, PersonalTable.StockZA.Count),
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, "No personal table layout for this version."),
    };

    private static IPersonalTable Build(GameVersion version, Memory<byte> data) => Layout(version).Context switch
    {
        EntityContext.Gen1 => new PersonalTable1(data),
        EntityContext.Gen2 => new PersonalTable2(data),
        EntityContext.Gen3 => new PersonalTable3(data),
        EntityContext.Gen4 => new PersonalTable4(data),
        EntityContext.Gen5 when version is GameVersion.B2 or GameVersion.W2 or GameVersion.B2W2 => new PersonalTable5B2W2(data),
        EntityContext.Gen5 => new PersonalTable5BW(data),
        EntityContext.Gen6 when version is GameVersion.OR or GameVersion.AS or GameVersion.ORAS => new PersonalTable6AO(data),
        EntityContext.Gen6 => new PersonalTable6XY(data),
        EntityContext.Gen7 when version is GameVersion.US or GameVersion.UM or GameVersion.USUM => new PersonalTable7(data, PersonalTable.StockUSUM.MaxSpeciesID),
        EntityContext.Gen7 => new PersonalTable7(data, PersonalTable.StockSM.MaxSpeciesID),
        EntityContext.Gen7b => new PersonalTable7GG(data),
        EntityContext.Gen8 => new PersonalTable8SWSH(data),
        EntityContext.Gen8a => new PersonalTable8LA(data),
        EntityContext.Gen8b => new PersonalTable8BDSP(data),
        EntityContext.Gen9 => new PersonalTable9SV(data),
        EntityContext.Gen9a => new PersonalTable9ZA(data),
        _ => throw new ArgumentOutOfRangeException(nameof(version), version, null),
    };
}
