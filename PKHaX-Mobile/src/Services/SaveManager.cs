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

	/// <summary>A loaded Space World '97 save state or battery file, or null. PKHeX has no SaveFile for the
	/// 1997 prototype, so it is carried separately and edited through its own page.</summary>
	public SW97Save? SpaceWorld { get; private set; }

	/// <summary>When the open "save" actually lives inside an emulator save state, the session that
	/// syncs edits back into the state container on write.</summary>
	public SaveStateSession? StateSession { get; private set; }

	/// <summary>The opaque handle the platform layer needs to write the file back where it came from.</summary>
	public SaveFileHandle? Handle { get; private set; }

	/// <summary>
	/// Illegal-edit mode. ON by default — the whole point of "PKHaX" (the HaX spelling) is illegal mode.
	/// PKHeX.Core surfaces the unfiltered data sources when this is true, exactly like the desktop build's
	/// filename-ends-in-"HaX" toggle.
	/// </summary>
	public bool IllegalMode { get; set; } = true;

	public bool IsLoaded => Save is not null || SpaceWorld is not null;

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
		// PKHaX: SaveFile wraps the array it is given WITHOUT copying (SaveFile.Buffer = data), so every edit and every
		// Write() would leak into a backup taken from the same array. Keep a private copy of the bytes exactly as opened
		// before anything else gets hold of the array.
		var asOpened = bytes.ToArray(); // PKHaX: private copy, the only backup / corruption source
		var sav = SaveUtil.GetSaveFile(bytes, handle.DisplayName);
		if (sav is null)
		{
			if (SW97Save.TryLoad(bytes, handle.DisplayName, out var sw97) && sw97 is not null)
			{
				Save = null;
				StateSession = null;
				SpaceWorld = sw97;
				Handle = handle;
				originalBytes = asOpened; // PKHaX: never the array the SaveFile aliases
				return null;
			}
			if (SaveStateAnalysis.TryAnalyze(bytes, handle.DisplayName) is { HasAnything: true } analysis)
			{
				var session = SaveStateSession.CreateEmbedded(analysis)
					?? SaveStateSession.CreateGBParty(analysis)
					?? (analysis.RamParties.Count > 0 ? SaveStateSession.CreateRamParty(analysis, analysis.RamParties[0]) : null);
				if (session is not null)
				{
					SpaceWorld = null;
					StateSession = session;
					Save = session.Save;
					Handle = handle;
					originalBytes = asOpened; // PKHaX: never the array the SaveFile aliases
					return null;
				}
			}
			return "That file was not recognised as a supported save (Gen 1-9, main-series), an emulator save state with Pokémon data, or a Space World '97 save state.";
		}

		SpaceWorld = null;
		StateSession = null;
		Save = sav;
		Handle = handle;
		originalBytes = asOpened; // PKHaX: never the array the SaveFile aliases
		sav.State.Edited = false; // PKHaX: nothing edited yet (same reset as the desktop editor after a load)
		return null;
	}

	/// <summary>Serialises the save through PKHeX.Core and writes it back to its original location.</summary>
	public async Task<string?> SaveBackAsync()
	{
		if ((Save is null && SpaceWorld is null) || Handle is null)
			return "No save is open.";

		try
		{
			var data = SpaceWorld is not null ? SpaceWorld.PrepareForWrite()
				: StateSession is not null ? StateSession.WriteBack()
				: Save!.Write().ToArray();
			await gateway.WriteSaveAsync(Handle.Value, data);
			originalBytes = data.ToArray(); // PKHaX: private copy of what is now on disk (WriteBack/PrepareForWrite may alias live buffers)
			if (Save is not null)
				Save.State.Edited = false; // PKHaX: the file now holds every edit (same reset as the desktop editor after an export)
			return null;
		}
		catch (Exception ex)
		{
			return $"Could not write the save: {ex.Message}";
		}
	}

	/// <summary>
	/// PKHaX: true if the editor has changed the loaded save since it was opened or last written back. This is the
	/// editor's own edit flag (<see cref="SaveFileState.Edited"/>, the same one the desktop build's close/export warnings
	/// use), set by every mobile edit path through <see cref="MarkEdited"/>. It deliberately does NOT compare
	/// <see cref="SaveFile.Write"/> against the bytes on disk: SAV1.Write() re-packs the party/box lists and rewrites the
	/// checksum, so an UNEDITED real save already differs from its file (measured: Blue.srm 6 bytes, Yellow.SAV 112 bytes),
	/// and Write() mutates the live buffer, so a byte compare was both a false positive and a side effect.
	/// </summary>
	public bool HasUnsavedEdits => Save?.State.Edited == true; // PKHaX

	/// <summary>
	/// PKHaX: records that the loaded save has been edited. PKHeX.Core sets <see cref="SaveFileState.Edited"/> only in
	/// <c>SetData</c>/<c>SetFlag</c> and a few Gen 4/6/8 setters - NOT in SetBoxSlotAtIndex/SetPartySlotAtIndex, the
	/// trainer/money/play-time properties, the bag or the box tools (the desktop editor flags those itself, too), so every
	/// mobile edit path calls this after it writes into the save.
	/// </summary>
	public void MarkEdited() // PKHaX
	{
		if (Save is not null)
			Save.State.Edited = true;
	}

	/// <summary>
	/// PKHaX: a copy of the bytes of the file exactly as it was opened (or as last written back by <see cref="SaveBackAsync"/>).
	/// This is a private copy: it never aliases the buffer the loaded <see cref="SaveFile"/> edits.
	/// </summary>
	public byte[]? OriginalBytes => originalBytes?.ToArray(); // PKHaX: Gen 1 save corrupter backup source

	/// <summary>PKHaX: the Gen 1 corrupter needs a plain raw 32 KiB Gen 1 save file - not one living inside an emulator save state.</summary>
	public bool CanCorruptGen1 => Save is SAV1 && StateSession is null && SpaceWorld is null && Handle is not null
		&& originalBytes is { Length: Gen1SaveCorrupter.ExpectedSize }; // PKHaX

	/// <summary>
	/// PKHaX: writes the bytes exactly as the file was opened (or last written back) to an app-local backup file and
	/// returns its path. Backups live in &lt;AppDataDirectory&gt;/backups/&lt;name&gt;.bak-yyyyMMdd-HHmmss so they survive
	/// the corruption even if the user dismisses the share sheet.
	/// </summary>
	public async Task<string> WriteGen1BackupAsync() // PKHaX: Gen 1 save corrupter, step 1 (always first)
	{
		if (!CanCorruptGen1 || Handle is not { } handle || originalBytes is null)
			throw new InvalidOperationException("No Gen 1 save file is open.");
		var dir = Path.Combine(FileSystem.AppDataDirectory, "backups");
		Directory.CreateDirectory(dir);
		var name = string.IsNullOrWhiteSpace(handle.DisplayName) ? "gen1.sav" : Path.GetFileName(handle.DisplayName);
		var path = Path.Combine(dir, Gen1SaveCorrupter.GetBackupName(name, DateTime.Now));
		await File.WriteAllBytesAsync(path, originalBytes);
		return path;
	}

	/// <summary>
	/// PKHaX: corrupts the file exactly as it was opened / last written back (party count 0xFF, terminator 0xFF, checksum
	/// recomputed, every other byte untouched - byte-identical to the standalone corrupt-save-gen1.py) and writes it back
	/// to the file the save was opened from. The in-memory save is NOT serialized and NOT changed: unsaved edits are not
	/// included (see <see cref="HasUnsavedEdits"/>), and PKHeX cannot reload a party-count-255 file, so the
	/// pre-corruption save stays loaded. Returns null on success or an error message.
	/// Call <see cref="WriteGen1BackupAsync"/> first.
	/// </summary>
	public async Task<string?> CorruptGen1Async() // PKHaX: Gen 1 save corrupter, step 2
	{
		if (!CanCorruptGen1 || Save is not SAV1 sav || Handle is not { } handle || originalBytes is null)
			return "No Gen 1 save file is open.";
		try
		{
			var offsets = Gen1SaveCorrupter.GetOffsets(sav);
			var data = originalBytes.ToArray();
			var tidOnDisk = Gen1SaveCorrupter.GetTID16(data, offsets);
			if (tidOnDisk != sav.TID16)
				return $"The file as opened has Trainer ID {tidOnDisk} but the loaded save has {sav.TID16}; refusing to corrupt a different save. Nothing was changed.";
			Gen1SaveCorrupter.ApplyInPlace(data, offsets);
			await gateway.WriteSaveAsync(handle, data);
			return null;
		}
		catch (Exception ex)
		{
			return $"Could not write the corrupted save: {ex.Message}";
		}
	}

	public IReadOnlyList<PKM> GetBox(int box) => Save?.GetBoxData(box) ?? [];

	public void SetBoxSlot(int box, int slot, PKM pk)
	{
		Save?.SetBoxSlotAtIndex(pk, box, slot);
		MarkEdited(); // PKHaX
	}

	public IReadOnlyList<PKM> GetParty()
	{
		if (Save is null) return [];
		var list = new List<PKM>();
		for (int i = 0; i < Save.PartyCount; i++)
			list.Add(Save.GetPartySlotAtIndex(i));
		return list;
	}

	public void SetPartySlot(int index, PKM pk)
	{
		Save?.SetPartySlotAtIndex(pk, index);
		MarkEdited(); // PKHaX
	}

	public string SpeciesName(int species) =>
		species >= 0 && species < Strings.specieslist.Length ? Strings.specieslist[species] : $"#{species}";

	public string AbilityName(int ability) =>
		ability >= 0 && ability < Strings.abilitylist.Length ? Strings.abilitylist[ability] : $"#{ability}";

	public string MoveName(int move) =>
		move >= 0 && move < Strings.movelist.Length ? Strings.movelist[move] : $"#{move}";
}
