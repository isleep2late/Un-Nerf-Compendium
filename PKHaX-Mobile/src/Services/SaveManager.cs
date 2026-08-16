using PKHeX.Core;

namespace PKHaX.Mobile.Services;

/// <summary>
/// Owns the currently-loaded save file and all interaction with the fork's PKHeX.Core.
/// Every hackmons feature (Gen 3 any-ability, Gen 1 sprite/typing, Deoxys forms, No Move) is
/// implemented inside PKHeX.Core, so this class gets them for free — there is no feature logic here.
/// </summary>
public sealed class SaveManager
{
	private readonly ISaveFileGateway gateway;
	private byte[]? originalBytes;

	public SaveManager(ISaveFileGateway gateway) => this.gateway = gateway;

	/// <summary>The loaded save, or null if none is open.</summary>
	public SaveFile? Save { get; private set; }

	/// <summary>The opaque handle the platform layer needs to write the file back where it came from.</summary>
	public SaveFileHandle? Handle { get; private set; }

	/// <summary>
	/// Illegal-edit mode. ON by default — the whole point of "PKHaX" (the HaX spelling) is illegal mode.
	/// PKHeX.Core surfaces the unfiltered data sources when this is true, exactly like the desktop build's
	/// filename-ends-in-"HaX" toggle.
	/// </summary>
	public bool IllegalMode { get; set; } = true;

	public bool IsLoaded => Save is not null;

	public GameStrings Strings { get; } = GameInfo.GetStrings("en");

	/// <summary>Opens whatever the user picked. Returns null on success, or a human-readable error.</summary>
	public async Task<string?> OpenAsync()
	{
		var picked = await gateway.PickSaveAsync();
		if (picked is null)
			return null; // user cancelled — not an error

		return Load(picked.Value.Bytes, picked.Value.Handle);
	}

	public string? Load(byte[] bytes, SaveFileHandle handle)
	{
		var sav = SaveUtil.GetSaveFile(bytes, handle.DisplayName);
		if (sav is null)
			return "That file was not recognised as a supported save (Gen 1-9, main-series).";

		Save = sav;
		Handle = handle;
		originalBytes = bytes;
		return null;
	}

	/// <summary>Serialises the save through PKHeX.Core and writes it back to its original location.</summary>
	public async Task<string?> SaveBackAsync()
	{
		if (Save is null || Handle is null)
			return "No save is open.";

		try
		{
			var data = Save.Write().ToArray();
			await gateway.WriteSaveAsync(Handle.Value, data);
			originalBytes = data;
			return null;
		}
		catch (Exception ex)
		{
			return $"Could not write the save: {ex.Message}";
		}
	}

	/// <summary>True if the in-memory save differs from what is on disk.</summary>
	public bool HasUnsavedChanges()
	{
		if (Save is null || originalBytes is null)
			return false;
		var current = Save.Write().ToArray();
		return !current.AsSpan().SequenceEqual(originalBytes);
	}

	public IReadOnlyList<PKM> GetBox(int box) => Save?.GetBoxData(box) ?? [];

	public void SetBoxSlot(int box, int slot, PKM pk) => Save?.SetBoxSlotAtIndex(pk, box, slot);

	public IReadOnlyList<PKM> GetParty()
	{
		if (Save is null) return [];
		var list = new List<PKM>();
		for (int i = 0; i < Save.PartyCount; i++)
			list.Add(Save.GetPartySlotAtIndex(i));
		return list;
	}

	public string SpeciesName(int species) =>
		species >= 0 && species < Strings.specieslist.Length ? Strings.specieslist[species] : $"#{species}";

	public string AbilityName(int ability) =>
		ability >= 0 && ability < Strings.abilitylist.Length ? Strings.abilitylist[ability] : $"#{ability}";

	public string MoveName(int move) =>
		move >= 0 && move < Strings.movelist.Length ? Strings.movelist[move] : $"#{move}";
}
