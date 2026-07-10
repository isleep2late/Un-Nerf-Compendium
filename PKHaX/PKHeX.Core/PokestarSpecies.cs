using System;
using System.Collections.Generic;

namespace PKHeX.Core;

/// <summary>
/// PKHaX: Pokéstar Studios opponent "prop" Pokémon support for Gen-5 Black2/White2.
/// These occupy internal BW2 species indices 652–684 (above the National Dex max of 649),
/// so they collide with Gen-6 National-Dex numbers (652 = Chesnaught, ...). Every lookup here is
/// therefore gated to Gen-5 context by the caller so Gen-6+ editing is unaffected.
/// The 17 names match exactly what Pokémon Showdown export/import produces for these mons.
/// </summary>
public static class PokestarSpecies
{
    /// <summary>Internal BW2 species-id → Showdown/display name (the 17 user-facing opponents).</summary>
    public static readonly (ushort Species, string Name)[] Entries =
    [
        (652, "Pokestar UFO"),
        (653, "Pokestar Brycen-Man"),
        (654, "Pokestar MT"),
        (655, "Pokestar MT2"),
        (656, "Pokestar Transport"),
        (657, "Pokestar Giant"),
        (658, "Pokestar Humanoid"),
        (659, "Pokestar Monster"),
        (660, "Pokestar F-00"),
        (661, "Pokestar Spirit"),
        (662, "Pokestar White Door"),
        (663, "Pokestar Black Door"),
        (665, "Pokestar UFO-PropU2"),
        (680, "Pokestar UFO-2"),
        (682, "Pokestar F-002"),
        (683, "Pokestar Black Belt"),
        (684, "Pokestar Smeargle"),
    ];

    public static bool IsPokestar(ushort species)
    {
        foreach (var (s, _) in Entries)
        {
            if (s == species)
                return true;
        }
        return false;
    }

    public static bool TryGetName(ushort species, out string name)
    {
        foreach (var (s, n) in Entries)
        {
            if (s == species)
            {
                name = n;
                return true;
            }
        }
        name = string.Empty;
        return false;
    }

    public static bool TryGetSpecies(ReadOnlySpan<char> name, out ushort species)
    {
        foreach (var (s, n) in Entries)
        {
            if (name.Equals(n, StringComparison.OrdinalIgnoreCase))
            {
                species = s;
                return true;
            }
        }
        species = 0;
        return false;
    }

    /// <summary>Combo-box items (Pokéstar name → internal species id) for the Gen-5 B2W2 species dropdown/search.</summary>
    public static IEnumerable<ComboItem> GetComboItems()
    {
        foreach (var (s, n) in Entries)
            yield return new ComboItem(n, s);
    }
}
