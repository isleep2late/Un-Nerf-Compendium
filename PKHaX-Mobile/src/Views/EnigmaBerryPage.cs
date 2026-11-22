using System.Globalization;
using PKHaX.Mobile.Services;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

public sealed class EnigmaBerryPage : ContentPage
{
	public const string NoSaveReason = "No save is open. Open a Ruby, Sapphire, Emerald, FireRed or LeafGreen save first.";

	public const string TagNoSourceWarning =
		"The berry's CHECK TAG text has no source without the game's ROM. The berry works in battle; do not open its tag screen.";

	public const string ItemSourceNote =
		"This record only defines what item 175, the Enigma Berry, does. The item itself is added in the bag editor "
		+ "(Berries pocket) or set as a held item.";

	public const string LegalityNote =
		"A Pokemon holding the berry passes the legality check only when the name is one of the distributed e-Reader "
		+ "berry names (PUMKIN, DRASH, EGGANT, STRIB, CHILAN, NUTPEA; a Japanese save has its own distributed names). "
		+ "Any other name is flagged as an invalid e-Reader berry.";

	public const string ActiveSlotNote = "Only the active save slot is edited. The backup slot keeps the old record.";

	public const string SaveStateNote =
		"This is the battery save inside a save state; a running game reads the record again only when it loads its save.";

	public const string RomLanguageRefusal =
		"That Emerald ROM is not the same language family as this save, so its text addresses do not fit.";

	public const string NothingChanged = "Nothing is changed, so nothing was written.";

	public const string NoEffectWhenUsed = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

	const long RomLimit = 64L * 1024 * 1024;
	const int GameCodeOffset = 0xAC;

	readonly SaveManager saves;
	readonly VerticalStackLayout root = new() { Padding = 16, Spacing = 2 };
	readonly Label statusLabel = Wrapped(12, Ui.Muted);
	readonly Label tagLabel = Wrapped(12, Ui.Accent);
	readonly Label sentenceLabel = Wrapped(13, Ui.Text);
	readonly Label helpLabel = Wrapped(11, Ui.Muted);
	readonly Label warningLabel = Wrapped(12, Ui.Accent);
	readonly Label parameterLabel = new() { FontSize = 14, TextColor = Ui.Text, VerticalOptions = LayoutOptions.Center };
	readonly Label pointerLabel = Wrapped(11, Ui.Muted);
	readonly Label descriptionNote = Wrapped(11, Ui.Muted);
	readonly Label resultLabel = Wrapped(12, Ui.Muted);
	readonly VerticalStackLayout descriptionHost = new() { Spacing = 2 };

	readonly Picker effectPicker = new()
	{
		Title = "Hold effect", TextColor = Ui.Text, BackgroundColor = Ui.SurfaceAlt, FontSize = 14,
		ItemsSource = EnigmaBerryEffects.All.Select(e => e.Label).ToList(),
	};

	readonly Entry parameterEntry = Field(Keyboard.Numeric);
	readonly Entry rawEffectEntry = Field(Keyboard.Numeric);
	readonly Entry rawParameterEntry = Field(Keyboard.Numeric);
	readonly Entry nameEntry = Field();
	readonly Entry firmnessEntry = Field(Keyboard.Numeric);
	readonly Entry sizeEntry = Field(Keyboard.Numeric);
	readonly Entry maxYieldEntry = Field(Keyboard.Numeric);
	readonly Entry minYieldEntry = Field(Keyboard.Numeric);
	readonly Entry stageEntry = Field(Keyboard.Numeric);
	readonly Entry spicyEntry = Field(Keyboard.Numeric);
	readonly Entry dryEntry = Field(Keyboard.Numeric);
	readonly Entry sweetEntry = Field(Keyboard.Numeric);
	readonly Entry bitterEntry = Field(Keyboard.Numeric);
	readonly Entry sourEntry = Field(Keyboard.Numeric);
	readonly Entry smoothnessEntry = Field(Keyboard.Numeric);
	readonly Entry itemEffectEntry = Field();
	readonly Entry description1Entry = Field();
	readonly Entry description2Entry = Field();

	readonly Button applyButton = Ui.Action("Apply to the open save", Ui.Positive);
	readonly Button romButton = Ui.Action("Fill the CHECK TAG text from the game's ROM");

	SAV3? sav;
	EnigmaBerry3? berry;
	bool openedValid;
	string? openedProblem;
	byte[] itemEffectAsFound = [];
	bool insideState;
	string? readOnlyReason;
	string? notice;
	bool loading;
	string? shown;

	public EnigmaBerryPage(SaveManager saves)
	{
		this.saves = saves;
		Title = "Enigma Berry (Gen 3)";
		BackgroundColor = Ui.Bg;
		Content = new ScrollView { Content = root };
	}

	public static string HackKitReason(string kitName) =>
		$"This save is open with the ROM hack kit {kitName}. A hack can keep the retail layout while it uses the berry's "
		+ "bytes for something else, so the Enigma Berry record is shown read-only for hack saves in this version.";

	public static string HackSaveReason(string? kitName) =>
		$"This save is in the ROM hack {kitName ?? "kit"}'s own save format, not a retail Gen 3 layout, so PKHaX Mobile "
		+ "does not read or write an Enigma Berry record in it.";

	public static string BridgedPartyReason(SaveStateFacet facet) =>
		$"This is a live-RAM party bridged from a save state ({facet}); it has no e-Reader berry record. "
		+ "The bridge copies only the six party slots in and out of the state, so a record shown here would not be the "
		+ "game's and an edit would go nowhere. Open the battery save (.sav/.srm) to edit the berry.";

	public static string? Unavailable(SaveFile? save, SaveStateFacet? facet)
	{
		if (facet is { } f && f != SaveStateFacet.EmbeddedSave)
			return BridgedPartyReason(f);
		if (save is null)
			return NoSaveReason;
		if (save is not SAV3 sav3)
			return $"This save ({save.Version}, Gen {save.Generation}) has no e-Reader berry record. "
				+ "Only Ruby, Sapphire, Emerald, FireRed and LeafGreen keep one.";
		return EnigmaBerry3.TryKind(sav3.LargeBlock.EReaderBerry.Length, out _)
			? null
			: "This save's e-Reader berry record has a length no Gen 3 game uses, so it is not edited.";
	}

	static Label Wrapped(double size, Color color, string text = "") => new()
	{
		Text = text, FontSize = size, TextColor = color, LineBreakMode = LineBreakMode.WordWrap, Margin = new Thickness(4, 0, 4, 6),
	};

	static Entry Field(Keyboard? keyboard = null) => new()
	{
		FontSize = 14, TextColor = Ui.Text, BackgroundColor = Ui.SurfaceAlt,
		Keyboard = keyboard ?? Keyboard.Default, HeightRequest = 42,
	};

	static Grid Row(string label, View field)
	{
		var grid = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(new GridLength(1.35, GridUnitType.Star))],
			ColumnSpacing = 8,
			Margin = new Thickness(0, 3),
		};
		grid.Add(new Label { Text = label, FontSize = 14, TextColor = Ui.Text, VerticalOptions = LayoutOptions.Center }, 0);
		grid.Add(field, 1);
		return grid;
	}

	static void Enable(Button button, bool enabled)
	{
		button.IsEnabled = enabled;
		button.Opacity = enabled ? 1 : 0.45;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (root.Count > 0)
			return;

		var facet = saves.StateSession?.Facet;
		var reason = saves.Hack is not null ? HackSaveReason(saves.Kit?.Kit.Name) : Unavailable(saves.Save, facet);
		if (reason is not null || saves.Save is not SAV3 opened)
		{
			root.Add(Ui.SectionHeader("Enigma Berry"));
			root.Add(Wrapped(13, Ui.Text, reason ?? NoSaveReason));
			return;
		}

		EnigmaBerry3 record;
		try
		{
			record = EnigmaBerry3.From(opened);
		}
		catch (ArgumentException ex)
		{
			root.Add(Ui.SectionHeader("Enigma Berry"));
			root.Add(Wrapped(13, Ui.Text, $"This save's e-Reader berry record could not be read: {ex.Message}"));
			return;
		}

		sav = opened;
		berry = record;
		insideState = facet is not null;
		openedValid = record.IsValid;
		openedProblem = record.Problem;
		itemEffectAsFound = record.ItemEffect.ToArray();
		if (saves.Kit is { } kit)
			readOnlyReason = HackKitReason(kit.Kit.Name);
		if (!openedValid && readOnlyReason is null)
			UseStockValues();

		BuildChrome();
		Push(null);
	}

	void UseStockValues()
	{
		if (berry is not { } b)
			return;
		b.UseDefaults();
		if (sav is SAV3FRLG)
			return;
		b.DescriptionPointer1 = 0;
		b.DescriptionPointer2 = 0;
	}

	void BuildChrome()
	{
		if (sav is null || berry is null)
			return;

		root.Add(Ui.SectionHeader("Enigma Berry"));
		root.Add(new Label
		{
			Text = $"{sav.Version} · Gen 3 · e-Reader berry record, {berry.Length} bytes",
			FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Ui.Text, Margin = new Thickness(4, 0),
		});
		root.Add(statusLabel);
		root.Add(tagLabel);

		var editable = readOnlyReason is null;
		root.Add(Ui.SectionHeader("Presets"));
		foreach (var preset in EnigmaBerryEffects.Presets)
		{
			var picked = preset;
			var button = Ui.Action($"{picked.Name} ({picked.HoldEffect} / {picked.Parameter})");
			button.Margin = new Thickness(0, 2);
			button.Clicked += (_, _) => UsePreset(picked);
			Enable(button, editable);
			root.Add(button);
		}
		var stock = Ui.Action("Use the stock number of the picked effect");
		stock.Margin = new Thickness(0, 2);
		stock.Clicked += (_, _) =>
		{
			if (berry is { } b)
				UsePreset(EnigmaBerryEffects.Stock(b.HoldEffect));
		};
		Enable(stock, editable);
		root.Add(stock);

		root.Add(Ui.SectionHeader("Hold effect"));
		effectPicker.SelectedIndexChanged += (_, _) =>
		{
			if (loading || effectPicker.SelectedIndex < 0)
				return;
			rawEffectEntry.Text = effectPicker.SelectedIndex.ToString(CultureInfo.InvariantCulture);
		};
		root.Add(effectPicker);
		var parameterRow = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(new GridLength(1.35, GridUnitType.Star))],
			ColumnSpacing = 8,
			Margin = new Thickness(0, 3),
		};
		parameterRow.Add(parameterLabel, 0);
		parameterRow.Add(parameterEntry, 1);
		root.Add(parameterRow);
		parameterEntry.TextChanged += (_, _) =>
		{
			if (loading || parameterEntry.Text == rawParameterEntry.Text)
				return;
			rawParameterEntry.Text = parameterEntry.Text;
		};
		root.Add(sentenceLabel);
		root.Add(helpLabel);
		root.Add(warningLabel);
		root.Add(Row("Raw effect number (0-255)", rawEffectEntry));
		root.Add(Row("Raw parameter (0-255)", rawParameterEntry));

		root.Add(Ui.SectionHeader("Name"));
		root.Add(Row("Up to 6 characters", nameEntry));
		nameEntry.MaxLength = EnigmaBerry3.NameLength;
		root.Add(Ui.Caption(LegalityNote));

		root.Add(Ui.SectionHeader("Berry stats"));
		root.Add(Row("Firmness", firmnessEntry));
		root.Add(Row("Size", sizeEntry));
		root.Add(Row("Largest harvest", maxYieldEntry));
		root.Add(Row("Smallest harvest", minYieldEntry));
		root.Add(Row("Growth time per stage", stageEntry));
		root.Add(Row("Spicy", spicyEntry));
		root.Add(Row("Dry", dryEntry));
		root.Add(Row("Sweet", sweetEntry));
		root.Add(Row("Bitter", bitterEntry));
		root.Add(Row("Sour", sourEntry));
		root.Add(Row("Smoothness", smoothnessEntry));

		root.Add(Ui.SectionHeader("Effect when used from the bag"));
		root.Add(Ui.Caption("18 bytes, two hex digits each."));
		root.Add(itemEffectEntry);
		var noEffect = Ui.Action("No effect when used");
		noEffect.Margin = new Thickness(0, 2);
		noEffect.Clicked += (_, _) => itemEffectEntry.Text = NoEffectWhenUsed;
		Enable(noEffect, editable);
		root.Add(noEffect);
		var asFound = Ui.Action("As found");
		asFound.Margin = new Thickness(0, 2);
		asFound.Clicked += (_, _) => itemEffectEntry.Text = FormatItemEffect(itemEffectAsFound);
		Enable(asFound, editable);
		root.Add(asFound);

		root.Add(Ui.SectionHeader("CHECK TAG text"));
		description1Entry.MaxLength = EnigmaBerry3.DescriptionLength;
		description2Entry.MaxLength = EnigmaBerry3.DescriptionLength;
		descriptionHost.Add(Row("Line 1", description1Entry));
		descriptionHost.Add(Row("Line 2", description2Entry));
		descriptionHost.Add(descriptionNote);
		root.Add(descriptionHost);
		root.Add(pointerLabel);
		romButton.Margin = new Thickness(0, 2);
		romButton.Clicked += async (_, _) => await PickRomAsync();
		root.Add(romButton);

		root.Add(Ui.SectionHeader("Write"));
		root.Add(Ui.Caption(ItemSourceNote));
		root.Add(Ui.Caption(ActiveSlotNote));
		root.Add(resultLabel);
		applyButton.Margin = new Thickness(0, 10, 0, 4);
		applyButton.Clicked += async (_, _) => await ApplyAsync();
		root.Add(applyButton);
		var revert = Ui.Action("Revert to what the save holds");
		revert.Margin = new Thickness(0, 2, 0, 24);
		revert.Clicked += (_, _) =>
		{
			if (berry is not { } b)
				return;
			b.Revert();
			if (!b.IsValid && readOnlyReason is null)
				UseStockValues();
			Push("Reverted to the record the open save holds now.");
		};
		Enable(revert, editable);
		root.Add(revert);

		foreach (var entry in EditEntries())
		{
			entry.IsEnabled = editable;
			entry.TextChanged += (_, _) => OnEdited();
		}
		parameterEntry.IsEnabled = editable;
		effectPicker.IsEnabled = editable;
	}

	IEnumerable<Entry> EditEntries() =>
	[
		rawEffectEntry, rawParameterEntry, nameEntry, firmnessEntry, sizeEntry, maxYieldEntry, minYieldEntry, stageEntry,
		spicyEntry, dryEntry, sweetEntry, bitterEntry, sourEntry, smoothnessEntry, itemEffectEntry, description1Entry, description2Entry,
	];

	string Snapshot() => string.Join("\u0001", EditEntries().Select(e => e.Text ?? string.Empty));

	void Push(string? note)
	{
		if (berry is not { } b)
			return;
		notice = note;
		loading = true;
		try
		{
			nameEntry.Text = b.Name;
			firmnessEntry.Text = Text(b.Firmness);
			sizeEntry.Text = Text(b.Size);
			maxYieldEntry.Text = Text(b.MaxYield);
			minYieldEntry.Text = Text(b.MinYield);
			stageEntry.Text = Text(b.StageDuration);
			spicyEntry.Text = Text(b.Spicy);
			dryEntry.Text = Text(b.Dry);
			sweetEntry.Text = Text(b.Sweet);
			bitterEntry.Text = Text(b.Bitter);
			sourEntry.Text = Text(b.Sour);
			smoothnessEntry.Text = Text(b.Smoothness);
			rawEffectEntry.Text = Text(b.HoldEffect);
			rawParameterEntry.Text = Text(b.HoldEffectParam);
			itemEffectEntry.Text = FormatItemEffect(b.ItemEffect);
			description1Entry.Text = b.Description1;
			description2Entry.Text = b.Description2;
		}
		finally
		{
			loading = false;
		}
		shown = Snapshot();
		RefreshDerived(null);
	}

	void OnEdited()
	{
		if (loading || berry is null)
			return;
		var now = Snapshot();
		if (now == shown)
			return;
		shown = now;
		notice = null;
		RefreshDerived(ReadEntries());
	}

	string? ReadEntries()
	{
		if (berry is not { } b)
			return NoSaveReason;
		if (!Number(firmnessEntry.Text, "Firmness", byte.MaxValue, out var firmness, out var refusal)
			|| !Number(sizeEntry.Text, "Size", ushort.MaxValue, out var size, out refusal)
			|| !Number(maxYieldEntry.Text, "Largest harvest", byte.MaxValue, out var maxYield, out refusal)
			|| !Number(minYieldEntry.Text, "Smallest harvest", byte.MaxValue, out var minYield, out refusal)
			|| !Number(stageEntry.Text, "Growth time", byte.MaxValue, out var stage, out refusal)
			|| !Number(spicyEntry.Text, "Spicy", byte.MaxValue, out var spicy, out refusal)
			|| !Number(dryEntry.Text, "Dry", byte.MaxValue, out var dry, out refusal)
			|| !Number(sweetEntry.Text, "Sweet", byte.MaxValue, out var sweet, out refusal)
			|| !Number(bitterEntry.Text, "Bitter", byte.MaxValue, out var bitter, out refusal)
			|| !Number(sourEntry.Text, "Sour", byte.MaxValue, out var sour, out refusal)
			|| !Number(smoothnessEntry.Text, "Smoothness", byte.MaxValue, out var smoothness, out refusal)
			|| !Number(rawEffectEntry.Text, "Hold effect", byte.MaxValue, out var holdEffect, out refusal)
			|| !Number(rawParameterEntry.Text, "Parameter", byte.MaxValue, out var parameter, out refusal))
			return refusal;
		if (!TryParseItemEffect(itemEffectEntry.Text, out var itemEffect, out refusal))
			return refusal;

		var name = nameEntry.Text ?? string.Empty;
		if (name != b.Name && !b.TrySetName(name.AsSpan(), out refusal))
			return refusal;
		if (b.HasOwnDescriptionText)
		{
			var line1 = description1Entry.Text ?? string.Empty;
			var line2 = description2Entry.Text ?? string.Empty;
			if (line1 != b.Description1 && !b.TrySetDescription(1, line1.AsSpan(), out refusal))
				return refusal;
			if (line2 != b.Description2 && !b.TrySetDescription(2, line2.AsSpan(), out refusal))
				return refusal;
		}

		b.Firmness = (byte)firmness;
		b.Size = (ushort)size;
		b.MaxYield = (byte)maxYield;
		b.MinYield = (byte)minYield;
		b.StageDuration = (byte)stage;
		b.Spicy = (byte)spicy;
		b.Dry = (byte)dry;
		b.Sweet = (byte)sweet;
		b.Bitter = (byte)bitter;
		b.Sour = (byte)sour;
		b.Smoothness = (byte)smoothness;
		b.HoldEffect = (byte)holdEffect;
		b.HoldEffectParam = (byte)parameter;
		if (!itemEffect.AsSpan().SequenceEqual(b.ItemEffect))
			b.SetItemEffect(itemEffect);
		return null;
	}

	bool KeepsPointersAsFound => sav is SAV3FRLG;

	bool TagTextMissing => berry is { } b && !KeepsPointersAsFound && (b.DescriptionPointer1 == 0 || b.DescriptionPointer2 == 0);

	string? TagWarning => TagTextMissing ? TagNoSourceWarning : null;

	bool RomCanFill => sav is SAV3E && berry is { } b && (!b.IsValid || b.DescriptionPointer1 == 0 || b.DescriptionPointer2 == 0);

	string StatusLine(EnigmaBerry3 b)
	{
		var record = b.IsValid
			? "The record is valid: the game uses it for item 175."
			: openedValid
				? $"The record is not valid. {b.Problem}"
				: readOnlyReason is not null
				? $"The record in this save is not valid. {openedProblem}"
				: $"The record in this save is not valid. {openedProblem} Stock values are filled in below; nothing is written until Apply.";
		if (insideState)
			record += " " + SaveStateNote;
		if (readOnlyReason is not null)
			record += " " + readOnlyReason;
		return record;
	}

	void RefreshDerived(string? refusal)
	{
		if (berry is not { } b)
			return;
		var effect = EnigmaBerryEffects.Get(b.HoldEffect);
		var editable = readOnlyReason is null;
		loading = true;
		try
		{
			effectPicker.SelectedIndex = effect.Known ? effect.Id : -1;
			if (parameterEntry.Text != rawParameterEntry.Text)
				parameterEntry.Text = rawParameterEntry.Text;
		}
		finally
		{
			loading = false;
		}

		parameterEntry.IsEnabled = editable && effect.ParameterUsed;
		parameterLabel.Text = effect.ParameterLabel;
		helpLabel.Text = effect.Help;
		sentenceLabel.Text = EnigmaBerryEffects.Describe(b.HoldEffect, b.HoldEffectParam);
		var warning = EnigmaBerryEffects.IsKnown(b.HoldEffect) ? null : EnigmaBerryEffects.UnknownWarning;
		warningLabel.Text = warning ?? string.Empty;
		warningLabel.IsVisible = warning is not null;

		descriptionHost.IsVisible = b.HasOwnDescriptionText;
		description1Entry.IsEnabled = editable && b.DescriptionTextEditable;
		description2Entry.IsEnabled = editable && b.DescriptionTextEditable;
		descriptionNote.Text = b.DescriptionTextEditable
			? "Two lines of up to 44 characters, kept in the save."
			: "The description pointers are zero, so the game does not read this text. It is left as found.";
		pointerLabel.Text = KeepsPointersAsFound
			? $"Description pointers 0x{b.DescriptionPointer1:X8} and 0x{b.DescriptionPointer2:X8}: FireRed and LeafGreen never read them, so they are kept as found."
			: $"Description pointers 0x{b.DescriptionPointer1:X8} and 0x{b.DescriptionPointer2:X8}.";
		romButton.IsVisible = editable && RomCanFill;

		statusLabel.Text = StatusLine(b);
		tagLabel.Text = TagWarning ?? string.Empty;
		tagLabel.IsVisible = TagWarning is not null;

		var blocked = readOnlyReason ?? refusal ?? saves.WriteRefusal ?? b.WriteRefusal;
		resultLabel.Text = blocked ?? notice ?? (b.HasChanges
			? "Changes are pending. Nothing is written until Apply."
			: "No pending changes.");
		Enable(applyButton, blocked is null && b.HasChanges);
	}

	void UsePreset(EnigmaBerryPreset preset)
	{
		if (berry is not { } b || readOnlyReason is not null)
			return;
		b.Apply(preset);
		Push($"{preset.Name}: hold effect {preset.HoldEffect}, parameter {preset.Parameter}. {preset.Note} Nothing is written until Apply.");
	}

	async Task PickRomAsync()
	{
		if (berry is null || readOnlyReason is not null)
			return;
		byte[] rom;
		try
		{
			var picked = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "The Emerald ROM this save belongs to (.gba)" });
			if (picked is null)
				return;
			await using var stream = await picked.OpenReadAsync();
			if (stream.CanSeek && stream.Length > RomLimit)
			{
				Push($"{picked.FileName} is too big to be a GBA ROM.");
				return;
			}
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);
			if (ms.Length > RomLimit)
			{
				Push($"{picked.FileName} is too big to be a GBA ROM.");
				return;
			}
			rom = ms.ToArray();
		}
		catch (Exception ex)
		{
			Push($"That file could not be read: {ex.Message}");
			return;
		}

		UseRom(rom, out var message);
		Push(message);
	}

	bool UseRom(ReadOnlySpan<byte> rom, out string message)
	{
		if (berry is not { } b || sav is null)
		{
			message = NoSaveReason;
			return false;
		}
		if (sav is SAV3FRLG)
		{
			message = "FireRed and LeafGreen never read the two description pointers, so they are kept as found.";
			return false;
		}
		if (sav is not SAV3E)
		{
			message = "Ruby and Sapphire point the tag text at the record's own text in memory; a ROM cannot fill that in.";
			return false;
		}
		if (!RomCanFill)
		{
			message = "This record already has its own tag text pointers. They are kept as found.";
			return false;
		}
		if (rom.Length < GameCodeOffset + 4 || rom[GameCodeOffset] != (byte)'B' || rom[GameCodeOffset + 1] != (byte)'P' || rom[GameCodeOffset + 2] != (byte)'E')
		{
			message = "That file is not an Emerald ROM (its game code does not start with BPE), so its text addresses do not fit this save.";
			return false;
		}
		if ((rom[GameCodeOffset + 3] == (byte)'J') != sav.Japanese)
		{
			message = RomLanguageRefusal;
			return false;
		}
		if (!EnigmaBerry3.TryFindStockDescriptions(rom, out var first, out var second, out var refusal))
		{
			message = refusal ?? "That ROM gives no tag text.";
			return false;
		}

		b.DescriptionPointer1 = first;
		b.DescriptionPointer2 = second;
		message = "The CHECK TAG text now points at the stock Enigma Berry text in that ROM. Nothing is written until Apply.";
		return true;
	}

	async Task ApplyAsync()
	{
		if (berry is not { } b || sav is null)
			return;
		if (readOnlyReason is null && (saves.Kit is not null || saves.Hack is not null))
			readOnlyReason = saves.Hack is not null ? HackSaveReason(saves.Kit?.Kit.Name) : HackKitReason(saves.Kit!.Kit.Name);
		if (readOnlyReason is not null)
		{
			RefreshDerived(null);
			return;
		}
		if (!ReferenceEquals(saves.Save, sav))
		{
			await DisplayAlertAsync("Enigma Berry",
				"The open save changed since this record was read. Nothing was written. Reopen this page and redo the edit.",
				"OK");
			return;
		}

		var refusal = ReadEntries();
		if (refusal is not null)
		{
			RefreshDerived(refusal);
			return;
		}
		if (saves.WriteRefusal is not null || b.WriteRefusal is not null || !b.HasChanges)
		{
			notice = b.HasChanges ? null : NothingChanged;
			RefreshDerived(null);
			return;
		}

		var effect = EnigmaBerryEffects.Get(b.HoldEffect);
		if (!await DisplayAlertAsync("Apply the Enigma Berry record",
			$"Write the record into the open save?\n\nHold effect {effect.Label}, parameter {b.HoldEffectParam}.\n{EnigmaBerryEffects.Describe(b.HoldEffect, b.HoldEffectParam)}",
			"Apply", "Cancel"))
			return;

		IReadOnlyList<EnigmaBerryRange> changed;
		try
		{
			changed = b.Write(sav.LargeBlock.EReaderBerry);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
		{
			Push($"The record was not written: {ex.Message}");
			return;
		}

		EReaderBerrySettings.LoadFrom(sav);
		saves.MarkEdited();
		var bytes = changed.Sum(r => r.Length);
		var tag = TagWarning is { } warning ? " " + warning : string.Empty;
		Push($"The Enigma Berry record is written into the open save ({bytes} byte{(bytes == 1 ? "" : "s")} changed). "
			+ "Use Save on the main page to put it on disk." + tag);
	}

	static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

	static bool Number(string? text, string label, int max, out int value, out string? refusal)
	{
		var s = text?.Trim() ?? string.Empty;
		var ok = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
			? int.TryParse(s.AsSpan(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value)
			: int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
		if (ok && value >= 0 && value <= max)
		{
			refusal = null;
			return true;
		}
		value = 0;
		refusal = $"{label} takes a whole number from 0 to {max}.";
		return false;
	}

	public static string FormatItemEffect(ReadOnlySpan<byte> bytes)
	{
		var parts = new string[bytes.Length];
		for (var i = 0; i < bytes.Length; i++)
			parts[i] = bytes[i].ToString("X2", CultureInfo.InvariantCulture);
		return string.Join(" ", parts);
	}

	public static bool TryParseItemEffect(string? text, out byte[] bytes, out string? refusal)
	{
		bytes = new byte[EnigmaBerry3.ItemEffectLength];
		var tokens = (text ?? string.Empty).Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		if (tokens.Length == 1 && tokens[0].Length == EnigmaBerry3.ItemEffectLength * 2)
		{
			var joined = tokens[0];
			tokens = new string[EnigmaBerry3.ItemEffectLength];
			for (var i = 0; i < tokens.Length; i++)
				tokens[i] = joined.Substring(i * 2, 2);
		}
		if (tokens.Length != EnigmaBerry3.ItemEffectLength)
		{
			refusal = $"The item-use effect is {EnigmaBerry3.ItemEffectLength} bytes, written as two hex digits each; {tokens.Length} were given.";
			return false;
		}
		for (var i = 0; i < tokens.Length; i++)
		{
			if (tokens[i].Length > 2 || !byte.TryParse(tokens[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i]))
			{
				refusal = $"\"{tokens[i]}\" is not a byte in hex (00 to FF).";
				return false;
			}
		}
		refusal = null;
		return true;
	}
}
