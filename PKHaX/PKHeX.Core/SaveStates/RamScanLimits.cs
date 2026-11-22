using System;

namespace PKHeX.Core;

public sealed record RamScanLimits
{
    public static RamScanLimits Default { get; } = new();

    public int MaxSpecies3 { get; init; } = 440;
    public int MaxMove3 { get; init; } = 354;
    public int MaxSpecies4 { get; init; } = 507;
    public int MaxSpecies5 { get; init; } = 649;
    public int MaxSpecies67 { get; init; } = 807;
    public int MinLevel { get; init; } = 2;
    public int MinLevel67 { get; init; } = 1;
    public int MaxLevel { get; init; } = 100;
    public Func<PK3, bool>? Gen3Validator { get; init; }

    public static RamScanLimits ForHack(int maxSpecies3, int maxMove3) => new()
    {
        MaxSpecies3 = maxSpecies3,
        MaxMove3 = maxMove3,
        MinLevel = 1,
        MinLevel67 = 1,
        MaxLevel = 255,
    };
}
