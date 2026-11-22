using System;

namespace PKHaX.Hacks.Overlay;

public enum ImageKind
{
    Vanilla,
    UnNerf,
    Hack,
    HackUnNerf,
    OwnerBuild,
    Imported,
}

public static class ImageKinds
{
    public static string Name(ImageKind kind) => kind switch
    {
        ImageKind.Vanilla => "vanilla",
        ImageKind.UnNerf => "unnerf",
        ImageKind.Hack => "hack",
        ImageKind.HackUnNerf => "hack+unnerf",
        ImageKind.OwnerBuild => "owner-build",
        ImageKind.Imported => "imported",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static bool TryParse(string? name, out ImageKind kind)
    {
        foreach (var value in Enum.GetValues<ImageKind>())
        {
            if (!string.Equals(Name(value), name, StringComparison.Ordinal))
                continue;
            kind = value;
            return true;
        }
        kind = default;
        return false;
    }

    public static ImageKind Parse(string name) => TryParse(name, out var kind)
        ? kind
        : throw new ArgumentException($"Unknown image kind '{name}'.", nameof(name));

    public static bool IsHack(ImageKind kind) => kind is ImageKind.Hack or ImageKind.HackUnNerf or ImageKind.Imported;
}
