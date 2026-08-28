using System.Text;
using PKHeX.Core;

namespace PKHaX.Mobile.Views;

/// <summary>
/// Showdown import/export for the open Pokemon. Parsing, re-rendering and application all come from
/// PKHeX.Core (<see cref="ShowdownSet"/> and ApplySetDetails), the same code the desktop app calls.
/// An import is deliberately faithful: the IVs and EVs the set names are what gets written, and nothing
/// here touches the separate "Max IVs" / "MAX HAX" buttons — those stay one tap away if they are wanted.
/// Anything the target format genuinely cannot hold is listed in the confirmation dialog before it is applied.
/// </summary>
public sealed partial class EntityEditorPage
{
	private string showdownText = string.Empty;
	private Editor showdownBox = null!;
	private bool showdownBusy;

	private const string SampleSet =
		"Chomper (Garchomp) @ Life Orb\n"
		+ "Ability: Rough Skin\n"
		+ "Level: 50\n"
		+ "Jolly Nature\n"
		+ "EVs: 252 Atk / 4 Def / 252 Spe\n"
		+ "- Earthquake";

	private void BuildShowdown()
	{
		root.Add(Ui.SectionHeader("Showdown set"));
		var v = new VerticalStackLayout { Spacing = 0 };
		v.Add(Ui.Caption(
			"Paste a Showdown set to overwrite this Pokemon, or copy this one out as a set. An import writes "
			+ "exactly the IVs and EVs the set asks for — press Max IVs or MAX HAX afterwards if you want them maxed."));

		showdownBox = Ui.TextArea(showdownText, SampleSet);
		showdownBox.TextChanged += (_, e) => showdownText = e.NewTextValue ?? string.Empty;
		v.Add(showdownBox);

		var clip = new Grid
		{
			ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)],
			ColumnSpacing = 8,
			Margin = new Thickness(0, 6, 0, 0),
		};
		var paste = Ui.Action("Paste from clipboard");
		paste.Clicked += async (_, _) => await PasteShowdownAsync();
		clip.Add(paste, 0);
		var copy = Ui.Action("Copy this Pokemon");
		copy.Clicked += async (_, _) => await CopyShowdownAsync();
		clip.Add(copy, 1);
		v.Add(clip);

		var import = Ui.Action("Import the set above", Ui.Positive);
		import.Margin = new Thickness(0, 8, 0, 0);
		import.Clicked += async (_, _) => await ImportShowdownAsync(showdownText);
		v.Add(import);

		root.Add(Ui.Card(v));
	}

	// ------------------------------------------------------------- clipboard
	private async Task PasteShowdownAsync()
	{
		if (showdownBusy) return;

		string? text;
		try
		{
			text = Clipboard.Default.HasText ? await Clipboard.Default.GetTextAsync() : null;
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Clipboard unavailable", $"The clipboard could not be read: {ex.Message}", "OK");
			return;
		}

		if (string.IsNullOrWhiteSpace(text))
		{
			await DisplayAlertAsync("Clipboard is empty",
				"There is no text on the clipboard. Copy a Showdown set from a browser or another app first, "
				+ "or type one straight into the box.", "OK");
			return;
		}

		showdownText = text;
		showdownBox.Text = text;
		await ImportShowdownAsync(text);
	}

	private async Task CopyShowdownAsync()
	{
		if (pk.Species == 0)
		{
			await DisplayAlertAsync("Nothing to copy", "This slot is empty, so there is no set to export.", "OK");
			return;
		}

		string text;
		try
		{
			text = ShowdownParsing.GetShowdownText(pk);
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Could not build a set",
				$"PKHeX could not describe this Pokemon as a Showdown set: {ex.Message}", "OK");
			return;
		}

		if (string.IsNullOrWhiteSpace(text))
		{
			await DisplayAlertAsync("Nothing to copy", "PKHeX produced an empty set for this Pokemon.", "OK");
			return;
		}

		showdownText = text;
		showdownBox.Text = text;

		try
		{
			await Clipboard.Default.SetTextAsync(text);
			var back = Clipboard.Default.HasText ? await Clipboard.Default.GetTextAsync() : null;
			if (!SameText(back, text))
			{
				await DisplayAlertAsync("Copied to the box only",
					"The set is in the box above, but the clipboard did not take it. Select the text and copy it by hand.", "OK");
				return;
			}
		}
		catch (Exception ex)
		{
			await DisplayAlertAsync("Copied to the box only",
				$"The set is in the box above, but the clipboard could not be written: {ex.Message}", "OK");
			return;
		}

		await DisplayAlertAsync("Copied to the clipboard", text, "OK");
	}

	private static bool SameText(string? a, string b) =>
		a is not null && Normalise(a) == Normalise(b);

	private static string Normalise(string s) => s.Replace("\r\n", "\n").Trim();

	// ------------------------------------------------------------- import
	private async Task ImportShowdownAsync(string? raw)
	{
		if (showdownBusy) return;

		var text = (raw ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			await DisplayAlertAsync("Nothing to import",
				"The box above is empty. Paste or type a Showdown set into it first.", "OK");
			return;
		}

		showdownBusy = true;
		try
		{
			// A set's EntityContext otherwise defaults to the newest generation, which drives item conversion,
			// form-name lookup and the Game Boy era Hidden Power scaling. Point it at the save that is open.
			if (saves.Save is { } sav)
				RecentTrainerCache.SetRecentTrainer(sav);

			ShowdownSet set;
			int extraSets;
			try
			{
				(set, extraSets) = ParseFirstSet(text);
			}
			catch (Exception ex)
			{
				await DisplayAlertAsync("Could not read that set",
					$"The text could not be parsed: {ex.Message}\n\nNothing was changed.", "OK");
				return;
			}

			if (set.Species == 0)
			{
				await ExplainNoSpeciesAsync(text, set);
				return;
			}

			// The applicator clamps an out-of-range species to the format's highest one, which would quietly
			// hand back a completely different Pokemon. Refuse instead.
			if (set.Species > pk.MaxSpeciesID)
			{
				await DisplayAlertAsync("Wrong generation",
					$"{lists.SpeciesName(set.Species)} (#{set.Species}) does not exist in PK{pk.Format}, which stops at "
					+ $"#{pk.MaxSpeciesID} ({lists.SpeciesName(pk.MaxSpeciesID)}).\n\n"
					+ "Nothing was changed — importing would have stored a different Pokemon than the set asked for.", "OK");
				return;
			}

			var before = pk;
			var candidate = pk.Clone();
			HeldItemRequest item = default;
			try
			{
				candidate.ApplySetDetails(set);
				ApplyGen3AnyAbility(candidate, set);
				FixPidDerivedFields(candidate, set);
				item = ResolveHeldItem(text, set, candidate);
				candidate.ApplyHeldItem(item.Id, item.Context);
			}
			catch (Exception ex)
			{
				await DisplayAlertAsync("Could not apply that set",
					$"PKHeX rejected the set while writing it to a PK{pk.Format}: {ex.Message}\n\nNothing was changed.", "OK");
				return;
			}

			var problems = new List<string>();
			foreach (var error in set.InvalidLines)
				problems.Add(Humanize(error));
			var dropped = DescribeDropped(set, candidate, item);
			var notes = DescribeNotes(set, before, candidate, extraSets);

			var body = new StringBuilder();
			body.AppendLine("Understood as:").AppendLine();
			body.AppendLine(EchoText(set, item));
			AppendSection(body, "Lines that could not be read", problems);
			AppendSection(body, $"Cannot be stored in PK{pk.Format}", dropped);
			AppendSection(body, "Heads-up", notes);
			body.AppendLine();
			body.Append("The IVs and EVs above are written exactly as they read. Max IVs and MAX HAX stay separate buttons.");

			if (!await DisplayAlertAsync("Import Showdown set", body.ToString(), "Import", "Cancel"))
				return;

			pk = candidate;
			Rebuild();
		}
		finally
		{
			showdownBusy = false;
		}
	}

	private async Task ExplainNoSpeciesAsync(string text, ShowdownSet set)
	{
		var first = FirstContentLine(text);
		var sb = new StringBuilder();
		if (first.Length == 0)
			sb.AppendLine("The text has no readable first line.");
		else
			sb.Append('"').Append(first).AppendLine("\" is not a species name, so there is no Pokemon to import.");

		sb.AppendLine().AppendLine("A set starts with the species, optionally a nickname and a held item:").AppendLine();
		sb.AppendLine(SampleSet.Replace("\n", Environment.NewLine));

		var problems = new List<string>();
		foreach (var error in set.InvalidLines)
			problems.Add(Humanize(error));
		AppendSection(sb, "Other lines that could not be read", problems);

		sb.AppendLine().Append("Nothing was changed.");
		await DisplayAlertAsync("No Pokemon in that set", sb.ToString(), "OK");
	}

	/// <summary>Parses the first set in the text and counts how many further sets follow it.</summary>
	private static (ShowdownSet Set, int Extra) ParseFirstSet(string text)
	{
		var span = text.AsSpan();
		var set = ShowdownParsing.GetShowdownSet(span, out int used);

		int extra = 0;
		var rest = used >= span.Length ? default : span[used..];
		while (!rest.IsWhiteSpace() && extra < 30)
		{
			ShowdownParsing.GetShowdownSet(rest, out int step);
			if (step <= 0)
				break;
			extra++;
			if (step >= rest.Length)
				break;
			rest = rest[step..];
		}
		return (set, extra);
	}

	private static string Humanize(BattleTemplateParseError error)
	{
		string text;
		try { text = error.Humanize(BattleTemplateParseErrorLocalization.Get("en")); }
		catch { return $"{error.Type}: {error.Value}"; }

		// PKHeX only substitutes the "{0}" placeholder when the error carries a value, so an error raised
		// against a line with nothing quotable (a stray "@", a bare "Ability:") reaches the user as the raw
		// template. Drop the placeholder rather than showing it.
		if (text.Contains("{0}", StringComparison.Ordinal))
		{
			text = text.Replace(" \"{0}\"", "", StringComparison.Ordinal)
				.Replace(": {0}", "", StringComparison.Ordinal)
				.Replace("{0}", "", StringComparison.Ordinal)
				.TrimEnd();
		}
		return text;
	}

	private static void AppendSection(StringBuilder sb, string title, List<string> items)
	{
		if (items.Count == 0)
			return;
		sb.AppendLine();
		sb.Append(title).AppendLine(":");
		foreach (var item in items)
			sb.Append(" • ").AppendLine(item);
	}

	private static string FirstContentLine(string text)
	{
		foreach (var line in text.Split('\n'))
		{
			var trimmed = line.Trim();
			if (trimmed.Length == 0)
				continue;
			return trimmed.Length <= 60 ? trimmed : trimmed[..60] + "…";
		}
		return string.Empty;
	}

	/// <summary>
	/// PKHaX Gen 3 any-ability: the shared applicator refuses an ability outside the species' pool, but PK3
	/// carries an override byte for exactly that (the same field the Ability picker on this page writes).
	/// </summary>
	private static void ApplyGen3AnyAbility(PKM candidate, ShowdownSet set)
	{
		if (candidate is not PK3 p3 || set.Ability <= 0 || p3.Ability == set.Ability)
			return;

		var index = p3.PersonalInfo.GetIndexOfAbility(set.Ability);
		if (index >= 0)
		{
			p3.AbilityOverride = 0;
			p3.SetAbilityIndex(index);
		}
		else if (set.Ability >= 2)
		{
			p3.AbilityOverride = set.Ability;
		}
	}

	/// <summary>
	/// Generation 3 and 4 read the nature (and an Unown's letter) straight out of the PID, but the shared PID
	/// generator only applies those rules when the entity names an origin game — and a blank slot names none,
	/// so a set imported into an empty Gen 3/4 slot lands on a random nature. Redo the PID under the right rules.
	/// Gender, the ability bit and the requested shininess are all preserved.
	/// </summary>
	private static void FixPidDerivedFields(PKM candidate, ShowdownSet set)
	{
		if (candidate.Format is not (3 or 4))
			return;

		var nature = (byte)set.Nature < 25 ? set.Nature : candidate.Nature;
		var isUnown3 = candidate.Format == 3 && candidate.Species == (int)Species.Unown;
		if (candidate.Nature == nature && !(isUnown3 && candidate.Form != set.Form))
			return;

		var origin = candidate.Version;
		if (origin == GameVersion.Any)
			origin = candidate.Format == 3 ? (isUnown3 ? GameVersion.FR : GameVersion.E) : GameVersion.D;

		var rnd = Util.Rand;
		for (int i = 0; i < 64; i++)
		{
			candidate.PID = EntityPID.GetRandomPID(rnd, candidate.Species, candidate.Gender, origin, nature, set.Form, candidate.PID);
			if (candidate.Nature != nature)
				continue;
			if (candidate.IsShiny && !set.Shiny)
				continue;
			break;
		}

		if (set.Shiny && !candidate.IsShiny)
			candidate.SetShinySID();
		candidate.RefreshChecksum();
	}

	// ------------------------------------------------------------- reporting
	/// <summary>
	/// Everything the set asked for that the format could not store, worked out by comparing the set against
	/// the entity it actually produced. Built before anything is committed, so a cancel really does change nothing.
	/// </summary>
	private List<string> DescribeDropped(ShowdownSet set, PKM after, HeldItemRequest item)
	{
		var d = new List<string>();
		var fmt = after.Format;

		if (set.Form != after.Form)
		{
			var want = set.FormName.Length != 0 ? set.FormName : $"#{set.Form}";
			d.Add($"Form \"{want}\" — this format stores form #{after.Form} instead.");
		}

		if (set.Nickname.Length != 0 && set.Nickname != after.Nickname)
			d.Add($"Nickname \"{set.Nickname}\" — stored as \"{after.Nickname}\"; this format allows {after.MaxStringLengthNickname} characters.");

		if (item.Id > 0 && after.HeldItem == 0)
			d.Add(fmt == 1
				? $"Held item \"{item.Name}\" — Generation 1 has no held items."
				: $"Held item \"{item.Name}\" — there is no equivalent item in this format.");

		foreach (var move in set.Moves)
		{
			if (move == 0)
				continue;
			if (after.Move1 == move || after.Move2 == move || after.Move3 == move || after.Move4 == move)
				continue;
			d.Add($"Move \"{lists.MoveName(move)}\" (#{move}) — this format only knows moves up to #{after.MaxMoveID}.");
		}

		if (set.Ability > 0)
		{
			if (fmt < 3)
				d.Add($"Ability \"{lists.AbilityName(set.Ability)}\" — Generation 1 and 2 have no abilities.");
			else if (after.Ability != set.Ability)
				d.Add($"Ability \"{lists.AbilityName(set.Ability)}\" — not one this species can have in this format; kept \"{lists.AbilityName(after.Ability)}\".");
		}

		if (set.Nature != Nature.Random)
		{
			if (fmt < 3)
			{
				d.Add($"Nature \"{lists.NatureName((int)set.Nature)}\" — Generation 1 and 2 have no natures.");
			}
			else
			{
				// Gen 8 onwards keeps the stat-changing nature separate from the displayed one, and a set's
				// nature is the stat-changing one.
				var stored = fmt >= 8 ? after.StatAlignment : after.Nature;
				if (stored != set.Nature)
					d.Add($"Nature \"{lists.NatureName((int)set.Nature)}\" — stored as \"{lists.NatureName((int)stored)}\".");
			}
		}

		if (set.Gender is { } want2 && want2 < 2 && after.Gender != want2)
			d.Add(fmt <= 2
				? $"Gender {GenderText(want2)} — Generation 1 and 2 read gender from the Attack DV, which here gives {GenderText(after.Gender)}."
				: $"Gender {GenderText(want2)} — this species cannot be that gender; stored {GenderText(after.Gender)}.");

		if (set.Shiny && !after.IsShiny)
			d.Add(fmt == 1
				? "Shiny — Generation 1 has no shiny Pokemon."
				: "Shiny — this format could not produce a shiny with the rest of the set applied.");
		else if (!set.Shiny && after.IsShiny && fmt == 2)
			d.Add("Not shiny — Generation 2 reads shininess from the DVs, and the imported DVs happen to be a shiny spread.");

		var ivs = new int[6];
		after.GetIVs(ivs);
		if (!SameStats(ivs, set.IVs))
			d.Add($"IVs (HP/Atk/Def/SpA/SpD/Spe) {StatText(set.IVs)} — stored as {StatText(ivs)}"
				+ (fmt <= 2
					? "; Generation 1 and 2 use 0-15 DVs, HP is calculated from the other four, and Sp. Def always matches Sp. Atk."
					: $"; this format allows 0-{after.MaxIV}."));

		var evs = new int[6];
		after.GetEVs(evs);
		if (!SameStats(evs, set.EVs))
			d.Add($"EVs (HP/Atk/Def/SpA/SpD/Spe) {StatText(set.EVs)} — stored as {StatText(evs)}"
				+ (fmt <= 2
					? (AllZero(set.EVs)
						? "; a Generation 1 or 2 set with no EV line means maximum Stat Exp."
						: "; Generation 1 and 2 use 0-65535 Stat Exp.")
					: "; this format clamps each EV."));

		int wantLevel = set.Level;
		int gotLevel = LevelOf(after);
		if (gotLevel != wantLevel)
			d.Add($"Level {wantLevel} — stored as {gotLevel}; this format tops out at {(after is GBPKM ? 255 : 100)}.");

		if (set.TeraType != MoveType.Any && after is not ITeraType)
		{
			var tera = (int)set.TeraType;
			if (tera == TeraTypeUtil.Stellar) tera = TeraTypeUtil.StellarTypeDisplayStringIndex;
			d.Add($"Tera Type {TypeName(tera)} — only Generation 9 formats store a Tera Type.");
		}
		if (set.CanGigantamax && after is not IGigantamax)
			d.Add("Gigantamax — only Sword/Shield entities carry the Gigantamax flag.");
		if (set.DynamaxLevel != 10 && after is not IDynamaxLevel)
			d.Add($"Dynamax Level {set.DynamaxLevel} — this format has no Dynamax level.");

		return d;
	}

	/// <summary>Things that were stored, but are surprising enough to say out loud.</summary>
	private List<string> DescribeNotes(ShowdownSet set, PKM before, PKM after, int extraSets)
	{
		var notes = new List<string>();

		if (extraSets == 1)
			notes.Add("The text holds another set after this one; only the first is imported.");
		else if (extraSets > 1)
			notes.Add($"The text holds {extraSets} more sets after this one; only the first is imported.");

		var wasLevel = LevelOf(before);
		var nowLevel = LevelOf(after);
		if (wasLevel != nowLevel)
			notes.Add($"Level {wasLevel} → {nowLevel}" + (set.Level == 100 ? " (a set with no Level line means level 100)." : "."));

		if (set.HiddenPowerType >= 0)
		{
			var names = saves.Strings.HiddenPowerTypes;
			var name = set.HiddenPowerType < names.Length ? names[set.HiddenPowerType] : set.HiddenPowerType.ToString();
			notes.Add($"Hidden Power [{name}] comes from this exact IV spread — Max IVs or MAX HAX afterwards will change its type.");
		}

		if (after.Format >= 8 && set.Nature != Nature.Random && after.Nature != after.StatAlignment)
			notes.Add($"Nature {lists.NatureName((int)set.Nature)} is stored as the stat-changing (Mint) nature, the way the desktop app does it; "
				+ $"the Nature row further down still reads {lists.NatureName((int)after.Nature)}.");

		if (before.CurrentFriendship != after.CurrentFriendship)
			notes.Add($"Friendship {before.CurrentFriendship} → {after.CurrentFriendship} (a set with no Friendship line means 255).");

		return notes;
	}

	private static int LevelOf(PKM entity) =>
		entity is GBPKM gb ? Math.Max(gb.Stat_Level, entity.CurrentLevel) : entity.CurrentLevel;

	private static bool SameStats(ReadOnlySpan<int> a, ReadOnlySpan<int> b)
	{
		for (int i = 0; i < 6; i++)
		{
			if (a[i] != b[i])
				return false;
		}
		return true;
	}

	private static bool AllZero(ReadOnlySpan<int> values)
	{
		foreach (var v in values)
		{
			if (v != 0)
				return false;
		}
		return true;
	}

	/// <summary>Stat arrays are stored HP/Atk/Def/Spe/SpA/SpD; print them in the Showdown order.</summary>
	private static string StatText(ReadOnlySpan<int> stored) =>
		$"{stored[0]}/{stored[1]}/{stored[2]}/{stored[4]}/{stored[5]}/{stored[3]}";

	/// <summary>
	/// The set as PKHeX re-renders it, with the held item put back to what the user typed. PKHeX prints the
	/// item id against <see cref="ShowdownSet.Context"/>, which a "Tera Type:" line may have replaced after the
	/// item was resolved, so the echo would otherwise name a different item than the one being stored.
	/// </summary>
	private static string EchoText(ShowdownSet set, HeldItemRequest item)
	{
		var text = set.Text.Trim();
		if (item.Id <= 0 || item.Name.Length == 0)
			return text;

		var end = text.IndexOf('\n');
		var firstLine = end < 0 ? text : text[..end];
		var at = firstLine.IndexOf('@');
		if (at < 0 || firstLine.AsSpan((at + 1)..).Trim().SequenceEqual(item.Name))
			return text;

		var rebuilt = string.Concat(firstLine.AsSpan(0, at + 1), " ", item.Name);
		return end < 0 ? rebuilt : rebuilt + text[end..];
	}

	/// <summary>A held item as the set asked for it: the id, the item table it was found in, and its name.</summary>
	private readonly record struct HeldItemRequest(int Id, EntityContext Context, string Name);

	/// <summary>
	/// Works out the held item from the text the user actually typed.
	/// <see cref="ShowdownSet"/> resolves the item against one generation's item table and records which table
	/// that was in <see cref="ShowdownSet.Context"/> — but a later "Tera Type:" or "Dynamax Level:" line
	/// overwrites Context without re-resolving the item, leaving the id pointing into a different table.
	/// Every modern Showdown export carries a Tera line, so a Gen 1-3 save would otherwise lose the item and
	/// be told the wrong item name. Re-resolving from the first line restores what the set asked for.
	/// </summary>
	private HeldItemRequest ResolveHeldItem(string raw, ShowdownSet set, PKM target)
	{
		var typed = FirstLineItemName(raw);
		if (typed.Length == 0)
			return new HeldItemRequest(set.HeldItem, set.Context, ItemNameIn(set.Context, set.HeldItem));

		// Same table order PKHeX itself searches, but anchored to the open save rather than a Context that a
		// later line may have replaced.
		foreach (var context in (ReadOnlySpan<EntityContext>)[target.Context, EntityContext.Gen3, EntityContext.Gen2, Latest.Context])
		{
			var names = saves.Strings.GetItemStrings(context);
			var id = StringUtil.FindIndexIgnoreCase(names, typed);
			if (id > 0)
				return new HeldItemRequest(id, context, names[id]);
		}
		return new HeldItemRequest(0, set.Context, typed);
	}

	/// <summary>The item text of a set's first line — everything after the first "@", as PKHeX splits it.</summary>
	private static string FirstLineItemName(string raw)
	{
		var line = FirstContentLineRaw(raw);
		var at = line.IndexOf('@');
		return at < 0 ? string.Empty : line[(at + 1)..].Trim();
	}

	private static string FirstContentLineRaw(string text)
	{
		foreach (var line in text.Split('\n'))
		{
			var trimmed = line.Trim();
			if (trimmed.Length != 0)
				return trimmed;
		}
		return string.Empty;
	}

	private string ItemNameIn(EntityContext context, int id)
	{
		var items = saves.Strings.GetItemStrings(context);
		return (uint)id < items.Length ? items[id] : $"#{id}";
	}
}
