using PKHeX.Core;

namespace PKHaX.Mobile.ViewModels;

/// <summary>One box slot as the CollectionView sees it. Empty slots render as a dash.</summary>
public sealed class SlotItem
{
	public int Index { get; init; }
	public string Name { get; init; } = "—";
	public string Detail { get; init; } = "";
	public string SpriteUrl { get; init; } = "";
	public bool IsEmpty { get; init; } = true;

	public static SlotItem From(int index, PKM pk, Services.SaveManager saves)
	{
		if (pk.Species == 0)
			return new SlotItem { Index = index };

		return new SlotItem
		{
			Index = index,
			IsEmpty = false,
			Name = string.IsNullOrWhiteSpace(pk.Nickname) ? saves.SpeciesName(pk.Species) : pk.Nickname,
			Detail = $"Lv {pk.CurrentLevel}" + (pk.IsShiny ? " ★" : ""),
			SpriteUrl = Sprites.Url(pk),
		};
	}
}
