using System;

namespace PKHeX.Core;

/// <summary>
/// <see cref="PersonalInfo"/> table (array).
/// </summary>
/// <remarks>
/// Serves as the main object that is accessed for stat data in a particular generation/game format.
/// </remarks>
public static class PersonalTable
{
    /// <summary>
    /// Personal Table used in <see cref="EntityContext.Gen9a"/>.
    /// </summary>
    public static PersonalTable9ZA ZA => PersonalOverride.Current?.ZA ?? StockZA;
    public static readonly PersonalTable9ZA StockZA = new(GetTable("za"));

    /// <summary>
    /// Personal Table used in <see cref="EntityContext.Gen9"/>.
    /// </summary>
    public static PersonalTable9SV SV => PersonalOverride.Current?.SV ?? StockSV;
    public static readonly PersonalTable9SV StockSV = new(GetTable("sv"));

    /// <summary>
    /// Personal Table used in <see cref="EntityContext.Gen8a"/>.
    /// </summary>
    public static PersonalTable8LA LA => PersonalOverride.Current?.LA ?? StockLA;
    public static readonly PersonalTable8LA StockLA = new(GetTable("la"));

    /// <summary>
    /// Personal Table used in <see cref="EntityContext.Gen8b"/>.
    /// </summary>
    public static PersonalTable8BDSP BDSP => PersonalOverride.Current?.BDSP ?? StockBDSP;
    public static readonly PersonalTable8BDSP StockBDSP = new(GetTable("bdsp"));

    /// <summary>
    /// Personal Table used in <see cref="EntityContext.Gen8"/>.
    /// </summary>
    public static PersonalTable8SWSH SWSH => PersonalOverride.Current?.SWSH ?? StockSWSH;
    public static readonly PersonalTable8SWSH StockSWSH = new(GetTable("swsh"));

    /// <summary>
    /// Personal Table used in <see cref="EntityContext.Gen7b"/>.
    /// </summary>
    public static PersonalTable7GG GG => PersonalOverride.Current?.GG ?? StockGG;
    public static readonly PersonalTable7GG StockGG = new(GetTable("gg"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.USUM"/>.
    /// </summary>
    public static PersonalTable7 USUM => PersonalOverride.Current?.USUM ?? StockUSUM;
    public static readonly PersonalTable7 StockUSUM = new(GetTable("uu"), Legal.MaxSpeciesID_7_USUM);

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.SM"/>.
    /// </summary>
    public static PersonalTable7 SM => PersonalOverride.Current?.SM ?? StockSM;
    public static readonly PersonalTable7 StockSM = new(GetTable("sm"), Legal.MaxSpeciesID_7);

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.ORAS"/>.
    /// </summary>
    public static PersonalTable6AO AO => PersonalOverride.Current?.AO ?? StockAO;
    public static readonly PersonalTable6AO StockAO = new(GetTable("ao"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.XY"/>.
    /// </summary>
    public static PersonalTable6XY XY => PersonalOverride.Current?.XY ?? StockXY;
    public static readonly PersonalTable6XY StockXY = new(GetTable("xy"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.B2W2"/>.
    /// </summary>
    public static PersonalTable5B2W2 B2W2 => PersonalOverride.Current?.B2W2 ?? StockB2W2;
    public static readonly PersonalTable5B2W2 StockB2W2 = new(GetTable("b2w2"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.BW"/>.
    /// </summary>
    public static PersonalTable5BW BW => PersonalOverride.Current?.BW ?? StockBW;
    public static readonly PersonalTable5BW StockBW = new(GetTable("bw"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.HGSS"/>.
    /// </summary>
    public static PersonalTable4 HGSS => PersonalOverride.Current?.HGSS ?? StockHGSS;
    public static readonly PersonalTable4 StockHGSS = new(GetTable("hgss"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.Pt"/>.
    /// </summary>
    public static PersonalTable4 Pt => PersonalOverride.Current?.Pt ?? StockPt;
    public static readonly PersonalTable4 StockPt = new(GetTable("pt"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.DP"/>.
    /// </summary>
    public static PersonalTable4 DP => PersonalOverride.Current?.DP ?? StockDP;
    public static readonly PersonalTable4 StockDP = new(GetTable("dp"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.LG"/>.
    /// </summary>
    public static PersonalTable3 LG => PersonalOverride.Current?.LG ?? StockLG;
    public static readonly PersonalTable3 StockLG = new(GetTable("lg"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.FR"/>.
    /// </summary>
    public static PersonalTable3 FR => PersonalOverride.Current?.FR ?? StockFR;
    public static readonly PersonalTable3 StockFR = new(GetTable("fr"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.E"/>.
    /// </summary>
    public static PersonalTable3 E => PersonalOverride.Current?.E ?? StockE;
    public static readonly PersonalTable3 StockE = new(GetTable("e"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.RS"/>.
    /// </summary>
    public static PersonalTable3 RS => PersonalOverride.Current?.RS ?? StockRS;
    public static readonly PersonalTable3 StockRS = new(GetTable("rs"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.C"/>.
    /// </summary>
    public static PersonalTable2 C => PersonalOverride.Current?.C ?? StockC;
    public static readonly PersonalTable2 StockC = new(GetTable("c"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.GS"/>.
    /// </summary>
    public static PersonalTable2 GS => PersonalOverride.Current?.GS ?? StockGS;
    public static readonly PersonalTable2 StockGS = new(GetTable("gs"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.RB"/>.
    /// </summary>
    public static PersonalTable1 RB => PersonalOverride.Current?.RB ?? StockRB;
    public static readonly PersonalTable1 StockRB = new(GetTable("rb"));

    /// <summary>
    /// Personal Table used in <see cref="GameVersion.YW"/>.
    /// </summary>
    public static PersonalTable1 Y => PersonalOverride.Current?.Y ?? StockY;
    public static readonly PersonalTable1 StockY = new(GetTable("y"));

    private static Memory<byte> GetTable(string game) => Util.GetBinaryResource($"personal_{game}");

    static PersonalTable() // Finish Setup
    {
        PopulateGen3Tutors();
        PopulateGen4Tutors();
    }

    private static void PopulateGen3Tutors()
    {
        // Enable Gen3 data with Emerald's data, FR/LG is a subset of Emerald's compatibility.
        var machine = BinLinkerAccessor.Get(Util.GetBinaryResource("hmtm_g3.pkl"), "g3"u8);
        var tutors = BinLinkerAccessor.Get(Util.GetBinaryResource("tutors_g3.pkl"), "g3"u8);
        StockE.LoadTables(machine, tutors);
        StockFR.CopyTables(StockE);
        StockLG.CopyTables(StockE);
        // PKHaX: allow Gen-3 Deoxys to use all 4 forms (Normal/Attack/Defense/Speed)
        StockRS[(int)Species.Deoxys].FormCount = 4;
        StockE[(int)Species.Deoxys].FormCount = 4;
        StockFR[(int)Species.Deoxys].FormCount = 4;
        StockLG[(int)Species.Deoxys].FormCount = 4;
        StockRS.CopyTables(StockE);
    }

    private static void PopulateGen4Tutors()
    {
        var tutors = BinLinkerAccessor.Get(Util.GetBinaryResource("tutors_g4.pkl"), "g4"u8);
        StockHGSS.LoadTables(tutors);
    }
}
