using PKHeX.Core;

namespace PKHaX.Mobile.ViewModels;

/// <summary>
/// Builds remote sprite URLs. PKHeX.Drawing (the desktop sprite pipeline) uses System.Drawing and is not
/// mobile-safe, so we do not reference it — sprites come over the network instead. Species that only exist
/// in the fork's battle sim (Goku, etc.) never appear inside a real cartridge save, so the standard sprite
/// set covers everything a save can hold.
/// </summary>
public static class Sprites
{
	private const string Base = "https://play.pokemonshowdown.com/sprites";

	public static string Url(PKM pk)
	{
		var species = GameInfo.Strings.Species[pk.Species].ToLowerInvariant();
		var id = ToShowdownId(species);
		var dir = pk.IsShiny ? "gen5-shiny" : "gen5";
		return $"{Base}/{dir}/{id}.png";
	}

	private static string ToShowdownId(string name)
	{
		Span<char> buffer = stackalloc char[name.Length];
		int n = 0;
		foreach (var c in name)
		{
			if (char.IsLetterOrDigit(c))
				buffer[n++] = char.ToLowerInvariant(c);
		}
		return new string(buffer[..n]);
	}
}
