using System;
using PKHaX.Hacks.Overlay;

namespace PKHaX.Hacks.Readers;

public sealed record HackSaveContext
{
    public const string VanillaContainer = "vanilla";

    public required string ProfileId { get; init; }
    public required ImageKind Kind { get; init; }
    public required string SaveVariant { get; init; }
    public int FrontierShift { get; init; }

    public bool IsVanillaContainer => IsVanilla(SaveVariant);

    public static bool IsVanilla(string? variant) =>
        string.Equals(variant, VanillaContainer, StringComparison.Ordinal) || (variant?.StartsWith(VanillaContainer + ":", StringComparison.Ordinal) ?? false);
}
