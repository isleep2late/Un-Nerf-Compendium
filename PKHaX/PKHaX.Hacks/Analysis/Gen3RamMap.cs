using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;

namespace PKHaX.Hacks.Analysis;

public sealed record Gen3RamBases(string GameCode, byte Revision, uint Storage, uint SaveBlock1, uint SaveBlock2, string Source);

public static class Gen3RamMap
{
    public const string PublicSource = "pokefirered sym_ewram (gPokemonStorage, gSaveBlock1, gSaveBlock2) for FireRed 1.0";

    public static IReadOnlyList<Gen3RamBases> Known { get; } =
    [
        new(CfruRamMap.FireRedCode, 0, CfruRamMap.FireRed10.Storage, CfruRamMap.FireRed10.SaveBlock1, CfruRamMap.FireRed10.SaveBlock2, PublicSource),
    ];

    public static uint CfruExtraSectors => CfruRamMap.FireRed10.ExtraSectors;

    public static Gen3RamBases? For(GbaImage rom) => For(rom.GameCode, rom.U8(CfruRamMap.RevisionAt));

    public static Gen3RamBases? For(string gameCode, byte revision) => Known.FirstOrDefault(k => k.GameCode == gameCode && k.Revision == revision);

    public static CfruRamMap? Cfru(GbaImage rom, out string? why) => CfruRamMap.For(rom, out why);

    public static SaveBlock? BlockOf(CfruBoxArea area) => area switch
    {
        CfruBoxArea.Storage => SaveBlock.Storage,
        CfruBoxArea.SaveBlock1 => SaveBlock.Large,
        CfruBoxArea.SaveBlock2 => SaveBlock.Small,
        _ => null,
    };
}
