using System;

namespace PKHaX.Hacks.Analysis;

public static class Gen3Engine
{
    public const string Expansion = "gba-expansion";
    public const string Cfru = "gba-cfru";
    public const string FrBinary = "gba-fr-binary";
    public const string Pokeemerald = "gba-pokeemerald";
    public const string Vanilla = "gba-vanilla";

    public const int CfruRun = CfruBoxTable.EngineRun;

    public static string Of(GbaImage rom, string? reader = null) => Of(rom, reader, CfruRun);

    public static string Of(GbaImage rom, string? reader, int cfruRun)
    {
        ArgumentNullException.ThrowIfNull(rom);
        if (RhhRomHeader.Read(rom) is not null)
            return Expansion;
        if (CfruBoxTable.HasRun(rom, cfruRun))
            return Cfru;
        var code = rom.GameCode;
        var fireRed = code.StartsWith("BPR", StringComparison.Ordinal) || code.StartsWith("BPG", StringComparison.Ordinal);
        if (fireRed)
            return reader == GbaHackData.ReaderFrFollow ? FrBinary : Vanilla;
        var emerald = code.StartsWith("BPE", StringComparison.Ordinal);
        if (emerald && reader is GbaHackData.ReaderGfInline or GbaHackData.ReaderGfShifted)
            return Pokeemerald;
        return Vanilla;
    }

    public static bool IsCfru(GbaImage rom) => RhhRomHeader.Read(rom) is null && CfruBoxTable.HasRun(rom, CfruRun);
}
