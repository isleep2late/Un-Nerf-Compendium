using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Intake;
using PKHeX.Core;

namespace PKHaX.Linux.Session;

public readonly record struct LinuxSlotRef(int Box, int Slot)
{
	public bool InParty => Box < 0;

	public static LinuxSlotRef Party(int slot) => new(-1, slot);

	public static LinuxSlotRef InBox(int box, int slot) => new(box, slot);

	public override string ToString() => InParty ? $"party slot {Slot + 1}" : $"box {Box + 1} slot {Slot + 1}";
}

public sealed record LinuxSlot(LinuxSlotRef Where, string Title, bool Empty)
{
	public override string ToString() => Title;
}

public sealed record LinuxSlotEdit
{
	public ushort? Species { get; init; }

	public string? Nickname { get; init; }

	public byte? Level { get; init; }

	public bool? Shiny { get; init; }

	public int? Nature { get; init; }

	public int? AbilityIndex { get; init; }

	public int? HeldItem { get; init; }

	public IReadOnlyList<ushort>? Moves { get; init; }

	public IReadOnlyList<int>? Ivs { get; init; }

	public IReadOnlyList<int>? Evs { get; init; }
}

public sealed record LinuxWriteResult(bool Written, string Sentence, string? BackupPath);

public sealed class LinuxSaveSession : IDisposable
{
	public const long MaxSaveBytes = 32L * 1024 * 1024;

	public const int BackupTries = 20;

	public const string StampFormat = "yyyyMMdd-HHmmss";

	public const string BackupSuffix = ".bak";

	public const string NothingToWrite = "Nothing to write: no change was applied since the save was opened or last written.";

	public static readonly string[] StatLabels = ["HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed"];

	internal static bool ProofNoBackup;

	internal static bool ProofWriteAnyway;

	internal static bool ProofBackupWriteFails;

	internal static bool ProofPlainRename;

	internal static bool ProofApplyIgnored;

	private readonly byte[] opened;
	private byte[] expected;
	private readonly List<string> backups = [];
	private string? stale;

	private LinuxSaveSession(string path, byte[] bytes, OpenResult result)
	{
		SavePath = path;
		opened = bytes.ToArray();
		expected = bytes.ToArray();
		Result = result;
	}

	public string SavePath { get; }

	public string FileName => Path.GetFileName(SavePath);

	public OpenResult Result { get; }

	public OpenKind Kind => Result.Kind;

	public ReadOnlyMemory<byte> Opened => opened;

	public const string NothingWritten = "Nothing was written";

	public string? WriteRefusal => stale ?? Reason(Result.WriteRefusal);

	public static string? Reason(string? refusal)
	{
		if (refusal is null)
			return null;
		var t = refusal.TrimStart();
		while (t.StartsWith(NothingWritten, StringComparison.Ordinal) && t.Length > NothingWritten.Length && t[NothingWritten.Length] is '.' or ':')
			t = t[(NothingWritten.Length + 1)..].TrimStart();
		return t.Length == 0 ? refusal : t;
	}

	public static string StaleAfterWrite(string name) => $"{name} was written; open it again before the next edit.";

	public static string StaleAfterWarning(string name) => $"{name} may not hold what PKHaX wrote; open it again before the next edit.";

	public bool CanWrite => WriteRefusal is null;

	public bool Dirty { get; private set; }

	public IReadOnlyList<string> Backups => backups;

	public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

	public static LinuxSaveSession Open(string path, HackKitStore store, OpenHint? hint = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		ArgumentNullException.ThrowIfNull(store);
		var full = Path.GetFullPath(path);
		var info = new FileInfo(full);
		if (!ProofPlainRename && info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is FileInfo target)
		{
			full = target.FullName;
			info = new FileInfo(full);
		}
		if (!info.Exists)
			throw new FileNotFoundException($"{full} does not exist", full);
		if (info.Length > MaxSaveBytes)
			throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"{info.Name} is {info.Length:N0} bytes, larger than any save PKHaX opens ({MaxSaveBytes:N0} bytes)"));
		var bytes = File.ReadAllBytes(full);
		var result = HackOpen.Open(bytes, full, store, null, hint);
		return new LinuxSaveSession(full, bytes, result);
	}

	public void Dispose() => Result.Dispose();

	public static IEnumerable<string> BackupCandidates(string savePath, DateTime at)
	{
		var stamp = at.ToString(StampFormat, CultureInfo.InvariantCulture);
		yield return $"{savePath}.{stamp}{BackupSuffix}";
		for (var n = 2; n <= BackupTries; n++)
			yield return string.Create(CultureInfo.InvariantCulture, $"{savePath}.{stamp}-{n}{BackupSuffix}");
	}

	public string? TrainerLine
	{
		get
		{
			if (Result.Stock is { } sav)
				return string.Create(CultureInfo.InvariantCulture, $"{sav.OT}, ID {sav.DisplayTID}, {sav.Version}, Gen {sav.Generation}");
			return Result.Native is { } hack ? hack.TrainerLine : null;
		}
	}

	public uint? Money => Result.Stock?.Money;

	public IReadOnlyList<LinuxSlot> PartySlots()
	{
		if (Result.Stock is not { HasParty: true } sav)
			return [];
		var slots = new List<LinuxSlot>();
		for (var i = 0; i < 6; i++)
		{
			var pk = sav.GetPartySlotAtIndex(i);
			slots.Add(new LinuxSlot(LinuxSlotRef.Party(i), $"Party {i + 1}: {TitleOf(pk)}", pk.Species == 0));
		}
		return slots;
	}

	public int BoxCount => Result.Stock is { HasBox: true } sav ? sav.BoxCount : 0;

	public string BoxName(int box)
	{
		if (Result.Stock is IBoxDetailNameRead named)
		{
			try
			{
				var name = named.GetBoxName(box);
				if (!string.IsNullOrWhiteSpace(name))
					return name;
			}
			catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException)
			{
			}
		}
		return string.Create(CultureInfo.InvariantCulture, $"Box {box + 1}");
	}

	public IReadOnlyList<LinuxSlot> BoxSlots(int box)
	{
		if (Result.Stock is not { HasBox: true } sav || box < 0 || box >= sav.BoxCount)
			return [];
		var slots = new List<LinuxSlot>();
		for (var s = 0; s < sav.BoxSlotCount; s++)
		{
			var pk = sav.GetBoxSlotAtIndex(box, s);
			slots.Add(new LinuxSlot(LinuxSlotRef.InBox(box, s), $"Slot {s + 1}: {TitleOf(pk)}", pk.Species == 0));
		}
		return slots;
	}

	public PKM? Slot(LinuxSlotRef where)
	{
		if (Result.Stock is not { } sav)
			return null;
		try
		{
			if (where.InParty)
				return sav.HasParty && where.Slot is >= 0 and < 6 ? sav.GetPartySlotAtIndex(where.Slot).Clone() : null;
			return sav.HasBox && where.Box < sav.BoxCount && where.Slot >= 0 && where.Slot < sav.BoxSlotCount ? sav.GetBoxSlotAtIndex(where.Box, where.Slot).Clone() : null;
		}
		catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
		{
			return null;
		}
	}

	public static string NameOf(IReadOnlyList<string> names, int id) =>
		id >= 0 && id < names.Count && !string.IsNullOrWhiteSpace(names[id]) ? names[id] : string.Create(CultureInfo.InvariantCulture, $"#{id}");

	public static string SpeciesName(int species) => NameOf(GameInfo.Strings.Species, species);

	public static string TitleOf(PKM pk)
	{
		if (pk.Species == 0)
			return "empty";
		var shown = pk.IsEgg ? "Egg" : pk.Nickname;
		return string.Create(CultureInfo.InvariantCulture, $"{shown} - {SpeciesName(pk.Species)} Lv {pk.CurrentLevel}{(pk.IsShiny ? " (shiny)" : "")}");
	}

	public static int? IdOf(IReadOnlyList<string> names, string? text, int max, out string? why)
	{
		why = null;
		var t = (text ?? "").Trim();
		if (t.Length == 0)
		{
			why = "give a name or a number";
			return null;
		}
		var hash = t.LastIndexOf('#');
		var digits = hash >= 0 ? t[(hash + 1)..].TrimEnd(')', ' ') : t;
		if (int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
		{
			if (id > max)
			{
				why = string.Create(CultureInfo.InvariantCulture, $"#{id} is past the last one this game has (#{max})");
				return null;
			}
			return id;
		}
		for (var i = 0; i < names.Count && i <= max; i++)
		{
			if (string.Equals(names[i], t, StringComparison.OrdinalIgnoreCase))
				return i;
		}
		why = $"no entry of this game is called \"{t}\"";
		return null;
	}

	public static string? CheckEdit(SaveFile sav, PKM pk, LinuxSlotEdit edit)
	{
		ArgumentNullException.ThrowIfNull(sav);
		ArgumentNullException.ThrowIfNull(pk);
		ArgumentNullException.ThrowIfNull(edit);
		if (edit.Species is { } species && (species == 0 || species > sav.MaxSpeciesID))
			return string.Create(CultureInfo.InvariantCulture, $"Species: #{species} is outside 1-{sav.MaxSpeciesID}");
		if (edit.Level is { } level && level is < 1 or > 100)
			return string.Create(CultureInfo.InvariantCulture, $"Level: {level} is outside 1-100");
		if (edit.Nickname is { } nick && nick.Length > pk.MaxStringLengthNickname)
			return string.Create(CultureInfo.InvariantCulture, $"Nickname: \"{nick}\" is longer than the {pk.MaxStringLengthNickname} letters this game holds");
		if (edit.HeldItem is { } item && (item < 0 || item > sav.MaxItemID))
			return string.Create(CultureInfo.InvariantCulture, $"Held item: #{item} is outside 0-{sav.MaxItemID}");
		if (edit.HeldItem is > 0 && pk.Format < 2)
			return "Held item: Generation 1 Pokemon hold no item";
		if (edit.Moves is { } moves)
		{
			if (moves.Count != 4)
				return "Moves: give four moves (0 for none)";
			foreach (var move in moves)
			{
				if (move > sav.MaxMoveID)
					return string.Create(CultureInfo.InvariantCulture, $"Moves: #{move} is outside 0-{sav.MaxMoveID}");
			}
			if (moves.All(m => m == 0))
				return "Moves: a Pokemon needs at least one move";
		}
		if (edit.Ivs is { } ivs && (ivs.Count != 6 || ivs.Any(v => v < 0 || v > pk.MaxIV)))
			return string.Create(CultureInfo.InvariantCulture, $"IVs: give six values of 0-{pk.MaxIV}");
		if (edit.Evs is { } evs && (evs.Count != 6 || evs.Any(v => v < 0 || v > pk.MaxEV)))
			return string.Create(CultureInfo.InvariantCulture, $"EVs: give six values of 0-{pk.MaxEV}");
		if (edit.Nature is { } nature && (pk.Format < 3 || nature is < 0 or > 24))
			return pk.Format < 3 ? "Nature: Generation 1 and 2 Pokemon have no nature" : string.Create(CultureInfo.InvariantCulture, $"Nature: {nature} is outside 0-24");
		if (edit.AbilityIndex is { } ability && (pk.Format < 3 || ability < 0 || ability >= pk.PersonalInfo.AbilityCount))
			return pk.Format < 3 ? "Ability: Generation 1 and 2 Pokemon have no ability" : string.Create(CultureInfo.InvariantCulture, $"Ability: slot {ability + 1} is not one this species has ({pk.PersonalInfo.AbilityCount})");
		return null;
	}

	public static void ApplyEdit(PKM pk, LinuxSlotEdit edit)
	{
		ArgumentNullException.ThrowIfNull(pk);
		ArgumentNullException.ThrowIfNull(edit);
		if (edit.Species is { } species)
			pk.Species = species;
		if (edit.Level is { } level)
			pk.CurrentLevel = level;
		if (edit.Nickname is { } nick)
			pk.SetNickname(nick);
		if (edit.HeldItem is { } item)
			pk.HeldItem = item;
		if (edit.Moves is { } moves)
		{
			pk.Move1 = moves[0];
			pk.Move2 = moves[1];
			pk.Move3 = moves[2];
			pk.Move4 = moves[3];
			pk.HealPP();
		}
		if (edit.Ivs is { } ivs)
		{
			pk.IV_HP = ivs[0];
			pk.IV_ATK = ivs[1];
			pk.IV_DEF = ivs[2];
			pk.IV_SPA = ivs[3];
			pk.IV_SPD = ivs[4];
			pk.IV_SPE = ivs[5];
		}
		if (edit.Evs is { } evs)
		{
			pk.EV_HP = evs[0];
			pk.EV_ATK = evs[1];
			pk.EV_DEF = evs[2];
			pk.EV_SPA = evs[3];
			pk.EV_SPD = evs[4];
			pk.EV_SPE = evs[5];
		}
		if (edit.Nature is { } nature)
			pk.SetNature((Nature)nature);
		if (edit.AbilityIndex is { } ability)
			pk.SetAbilityIndex(ability);
		if (edit.Shiny is { } shiny && shiny != pk.IsShiny)
			pk.SetIsShiny(shiny);
	}

	public string? Apply(LinuxSlotRef where, LinuxSlotEdit edit)
	{
		ArgumentNullException.ThrowIfNull(edit);
		if (stale is { } again)
			return again;
		if (Result.Stock is not { } sav)
			return "This save is not open in the stock editor.";
		if (Slot(where) is not { } pk || pk.Species == 0)
			return $"{where}: empty; filling an empty slot is not supported";
		if (CheckEdit(sav, pk, edit) is { } bad)
			return $"{where} is not changed: {bad}";
		try
		{
			ApplyEdit(pk, edit);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or OverflowException)
		{
			return $"{where} is not changed: {ex.Message}";
		}
		return Put(sav, where, pk);
	}

	public string? ShowdownOf(LinuxSlotRef where) => Slot(where) is { Species: > 0 } pk ? ShowdownParsing.GetShowdownText(pk) : null;

	public string? ApplyShowdown(LinuxSlotRef where, string text)
	{
		if (stale is { } again)
			return again;
		if (Result.Stock is not { } sav)
			return "This save is not open in the stock editor.";
		if (Slot(where) is not { } pk || pk.Species == 0)
			return $"{where}: empty; filling an empty slot is not supported";
		if (string.IsNullOrWhiteSpace(text))
			return "Paste a Showdown set first.";
		var set = new ShowdownSet(text);
		if (set.Species == 0 || set.Species > sav.MaxSpeciesID)
			return "The Showdown text names no species this game has.";
		if (set.InvalidLines.Count > 0)
			return string.Create(CultureInfo.InvariantCulture, $"The Showdown text has {set.InvalidLines.Count} line(s) PKHeX could not read, so nothing was changed.");
		try
		{
			pk.ApplySetDetails(set);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or OverflowException)
		{
			return $"{where} is not changed: {ex.Message}";
		}
		return Put(sav, where, pk);
	}

	private string? Put(SaveFile sav, LinuxSlotRef where, PKM pk)
	{
		try
		{
			if (where.InParty)
				pk.ResetPartyStats();
			pk.RefreshChecksum();
			if (!ProofApplyIgnored && where.InParty)
				sav.SetPartySlotAtIndex(pk, where.Slot, EntityImportSettings.None);
			else if (!ProofApplyIgnored)
				sav.SetBoxSlotAtIndex(pk, where.Box, where.Slot, EntityImportSettings.None);
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
		{
			return $"{where} is not changed: {ex.Message}";
		}
		Dirty = true;
		return null;
	}

	public string? SetMoney(uint money)
	{
		if (stale is { } again)
			return again;
		if (Result.Stock is not { } sav)
			return Result.Native is { } hack ? Mark(hack.SetMoney(money)) : "No save is open.";
		if (money > sav.MaxMoney)
			return string.Create(CultureInfo.InvariantCulture, $"Money: {money:N0} is above the {sav.MaxMoney:N0} this game holds");
		sav.Money = money;
		Dirty = true;
		return null;
	}

	public string? ApplyHack(HackMonEditModel edit)
	{
		ArgumentNullException.ThrowIfNull(edit);
		if (stale is { } again)
			return again;
		if (Result.Native is not { } hack)
			return "This save is not open in the hack editor.";
		if (edit.Changed.Count == 0)
			return null;
		return Mark(hack.Apply(edit));
	}

	private string? Mark(string? refusal)
	{
		if (refusal is null)
			Dirty = true;
		return refusal;
	}

	public LinuxWriteResult Write()
	{
		if (WriteRefusal is { } refusal && !ProofWriteAnyway)
			return new LinuxWriteResult(false, $"Nothing was written: {refusal}", null);
		if (!Dirty && !ProofWriteAnyway)
			return new LinuxWriteResult(false, NothingToWrite, null);
		byte[] now;
		try
		{
			now = File.ReadAllBytes(SavePath);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return new LinuxWriteResult(false, $"Nothing was written: {FileName} could not be read just before the write ({ex.Message}).", null);
		}
		if (!now.AsSpan().SequenceEqual(expected) && !ProofWriteAnyway)
			return new LinuxWriteResult(false, $"Nothing was written: {FileName} changed on disk since PKHaX opened it. Open it again.", null);
		byte[] data;
		try
		{
			if (Planned(now) is not { } planned)
				return new LinuxWriteResult(false, "Nothing was written: no save is open.", null);
			if (planned.Refusal is { } why)
				return new LinuxWriteResult(false, $"Nothing was written: {why}", null);
			data = planned.Bytes!;
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
		{
			return new LinuxWriteResult(false, $"Nothing was written: the save could not be prepared ({ex.Message}).", null);
		}
		string? backup = null;
		if (!ProofNoBackup)
		{
			backup = TakeBackup(out var backupWhy);
			if (backup is null && !ProofWriteAnyway)
				return new LinuxWriteResult(false, $"Nothing was written: {backupWhy} PKHaX writes a save only after its safety copy is on disk.", null);
		}
		var temp = Path.Combine(Path.GetDirectoryName(SavePath) ?? ".", $".{FileName}.pkhax-{Guid.NewGuid():N}.tmp");
		try
		{
			WriteChecked(temp, data, FileMode.CreateNew);
			if (!ProofPlainRename && !OperatingSystem.IsWindows())
				File.SetUnixFileMode(temp, File.GetUnixFileMode(SavePath));
			File.Move(temp, SavePath, overwrite: true);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			TryDelete(temp);
			return new LinuxWriteResult(false, $"Nothing was written to {FileName}: {ex.Message}" + (backup is null ? "" : $" The safety copy is at {backup}."), backup);
		}
		byte[] back;
		try
		{
			back = File.ReadAllBytes(SavePath);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			stale = StaleAfterWarning(FileName);
			return new LinuxWriteResult(true, $"WARNING: {FileName} was written but could not be read back ({ex.Message}). The safety copy is at {backup}. {stale}", backup);
		}
		if (!back.AsSpan().SequenceEqual(data))
		{
			stale = StaleAfterWarning(FileName);
			return new LinuxWriteResult(true, $"WARNING: {FileName} does not read back as written. The safety copy is at {backup}. {stale}", backup);
		}
		expected = data;
		Dirty = false;
		if (Result.Native is not null)
			stale = StaleAfterWrite(FileName);
		return new LinuxWriteResult(true, string.Create(CultureInfo.InvariantCulture, $"Wrote {FileName} ({data.Length:N0} bytes). Safety copy of the file as opened: {backup}."), backup);
	}

	private sealed record Plan(byte[]? Bytes, string? Refusal);

	private Plan? Planned(byte[] now)
	{
		if (Result.Stock is { } sav)
			return new Plan(sav.Write().ToArray(), null);
		if (Result.Native is { } hack)
		{
			var check = hack.CheckedWrite(Result.Shape, now, out var bytes);
			return check.Passed && bytes is not null ? new Plan(bytes, null) : new Plan(null, $"the self-check refused this write ({string.Join("; ", check.Failures)}).");
		}
		return null;
	}

	private string? TakeBackup(out string why)
	{
		why = "";
		var at = Clock();
		foreach (var name in BackupCandidates(SavePath, at))
		{
			if (File.Exists(name) || Directory.Exists(name))
				continue;
			FileStream stream;
			try
			{
				stream = new FileStream(name, FileMode.CreateNew, FileAccess.Write, FileShare.None);
			}
			catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && (File.Exists(name) || Directory.Exists(name)))
			{
				continue;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				why = $"the safety copy {Path.GetFileName(name)} could not be created ({ex.Message}).";
				return null;
			}
			try
			{
				using (stream)
				{
					stream.Write(opened);
					stream.Flush(flushToDisk: true);
					if (ProofBackupWriteFails)
						throw new IOException("planted");
				}
				var back = File.ReadAllBytes(name);
				if (!back.AsSpan().SequenceEqual(opened))
					throw new IOException($"{Path.GetFileName(name)} does not read back as written");
				backups.Add(name);
				return name;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				TryDelete(name);
				why = $"the safety copy {Path.GetFileName(name)} could not be written ({ex.Message}).";
				return null;
			}
		}
		why = string.Create(CultureInfo.InvariantCulture, $"no free name for the safety copy: {BackupTries} names beside {FileName} for {at.ToString(StampFormat, CultureInfo.InvariantCulture)} are taken.");
		return null;
	}

	private static void WriteChecked(string path, byte[] data, FileMode mode)
	{
		using (var stream = new FileStream(path, mode, FileAccess.Write, FileShare.None))
		{
			stream.Write(data);
			stream.Flush(flushToDisk: true);
		}
		var back = File.ReadAllBytes(path);
		if (!back.AsSpan().SequenceEqual(data))
			throw new IOException($"{Path.GetFileName(path)} does not read back as written");
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
				File.Delete(path);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
		}
	}
}
