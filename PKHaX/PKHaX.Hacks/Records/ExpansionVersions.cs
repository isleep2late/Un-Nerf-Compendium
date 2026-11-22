using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PKHaX.Hacks.Records;

public static class ExpansionVersions
{
    public const int FirstMinor = 9;
    public const int LastMinor = 15;

    public static IReadOnlyList<string> Supported { get; } = Build();

    private static string[] Build()
    {
        var list = new string[LastMinor - FirstMinor + 1];
        for (var minor = FirstMinor; minor <= LastMinor; minor++)
            list[minor - FirstMinor] = $"1.{minor}";
        return list;
    }

    public static bool TryNormalize(string? version, [NotNullWhen(true)] out string? normalized, [NotNullWhen(false)] out string? why)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(version))
        {
            why = "record codec 'pk3x' needs a version (pokeemerald-expansion 1.9 to 1.15)";
            return false;
        }
        var parts = version.Trim().Split('.');
        if (parts.Length is < 2 or > 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || (parts.Length == 3 && !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out _)))
        {
            why = $"'{version}' is not a pokeemerald-expansion version (expected 1.N or 1.N.P)";
            return false;
        }
        if (major != 1 || minor < FirstMinor || minor > LastMinor)
        {
            why = $"pokeemerald-expansion {version} is outside the measured BoxPokemon layout (1.{FirstMinor} to 1.{LastMinor})";
            return false;
        }
        normalized = $"1.{minor}";
        why = null;
        return true;
    }
}
