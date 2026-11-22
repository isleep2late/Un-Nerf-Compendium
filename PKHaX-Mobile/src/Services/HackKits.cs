using System.Globalization;
using Isleep2late.Files.Services.Pkhax;
using PKHaX.Hacks.Intake;

namespace PKHaX.Mobile.Services;

public sealed record PickedFile(byte[]? Bytes, string Name, long? Size, string? Refusal)
{
	public bool Ok => Bytes is not null && Refusal is null;

	public static PickedFile Refused(string name, long? size, string why) => new(null, name, size, why);
}

public sealed record HackAddResult(string Sentence, bool Stored)
{
	public static HackAddResult From(IntakeResult result)
	{
		var lines = new List<string> { result.Sentence ?? "PKHaX read nothing from this file." };
		if (result.Ok)
		{
			lines.Add(result.FromStore ? $"{result.Kit!.Name} was already stored; the stored tables are used." : IntakeText.Stored);
			if (result.StoreProblem is { } problem)
				lines.Add(problem);
		}
		lines.AddRange(result.Notes);
		return new HackAddResult(string.Join("\n", lines), result.Ok && result.StoreProblem is null);
	}
}

public sealed class HackKits
{
	public const long MaxRomBytes = 64L * 1024 * 1024;
	public const int ChunkBytes = 81920;
	public const string SaveNotKit = "This is a save, not a ROM hack. Open it with Open save file.";
	public const string NativeReadOnly = "PKHaX Mobile shows this hack's own save format read-only: the party list only, no edits.";
	public const string OverlayClosed = "This hack's names and stats were switched off when another file was checked. Open this save again before saving it.";
	public const string OtherOverlay = "Another save's ROM hack names and stats are active, so this save opens read-only. Open it again before saving it.";
	public const string StateHack = "This save state holds a ROM hack's own save format. PKHaX Mobile does not open ROM hack saves inside save states; open the hack's save file instead.";
	public const string ShorterThanSize = "The file ended before the size its folder reported, so PKHaX Mobile did not use it.";
	public const string LongerThanSize = "The file is longer than the size its folder reported, so PKHaX Mobile did not use it.";
	public const string AnswerOnMainPage = "Open it from the main page to answer.";
	public const string NotABase = "That file is not a ROM, so it cannot be the clean game this patch was made for.";
	public const string CheckFailed = "PKHaX could not check this file against the stored ROM hacks, so it was not opened:";
	public const string BackupFailed = "Nothing was written: the backup of the save could not be made.";
	public const string StockChoice = "None of these: open it as the game it says it is";
	public const string NoKits = "No ROM hacks stored yet.";
	public const string Cancelled = "Cancelled.";
	public const string ChooseGame = "Choose the game";

	public HackKits(string root)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(root);
		Store = new HackKitStore(root);
	}

	public HackKitStore Store { get; }

	public HackIntake NewIntake() => new(Store, maxRomBytes: MaxRomBytes);

	public IReadOnlyList<StoredKit> List()
	{
		try
		{
			return Store.List();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return [];
		}
	}

	public int Count()
	{
		try
		{
			return Directory.Exists(Store.HacksDir) ? Directory.EnumerateDirectories(Store.HacksDir).Count(d => HackKitStore.IsKey(Path.GetFileName(d))) : 0;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return 0;
		}
	}

	public string? Forget(StoredKit kit)
	{
		try
		{
			return Store.Forget(kit.Kit.Key) ? null : $"{kit.Kit.Name} was not stored.";
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return $"{kit.Kit.Name} could not be forgotten: {ex.Message}";
		}
	}

	public static string CountLine(int count) => count == 0 ? NoKits
		: string.Create(CultureInfo.InvariantCulture, $"{count} ROM hack{(count == 1 ? "" : "s")} stored");

	public static DropVerdict Classify(PickedFile file) => DropClassifier.Classify(file.Bytes, file.Bytes?.Length ?? 0, file.Name);

	public static PatchProbe? Inspect(PickedFile patch, out string? why)
	{
		try
		{
			why = null;
			return PkhaxRomPatcher.Shared.Inspect(patch.Bytes);
		}
		catch (RomPatchRefusedException ex)
		{
			why = $"the patch does not read: {ex.Message}";
			return null;
		}
	}

	public static string PatchPrompt(PatchProbe probe) =>
		probe.Format is PatchKind.Ips or PatchKind.Ips32 ? IntakeText.IpsPatch
		: probe.SourceCrc32 is { } crc && probe.SourceSize is { } size ? IntakeText.Patch(probe.Name, crc, size)
		: IntakeText.PatchNoCrc(probe.Name);

	public Task<HackAddResult> DeriveAsync(PickedFile rom, IProgress<string>? progress) =>
		Run(() => NewIntake().Derive(rom.Bytes, rom.Name, null, progress));

	public Task<HackAddResult> FromPatchAsync(PickedFile patch, PickedFile baseRom, IProgress<string>? progress) =>
		Run(() => NewIntake().FromPatch(patch.Bytes, baseRom.Bytes, PkhaxRomPatcher.Shared, patch.Name, null, progress));

	public Task<HackAddResult> FromProfileAsync(PickedFile profile) =>
		Run(() => NewIntake().FromProfile(profile.Bytes, profile.Name));

	private static Task<HackAddResult> Run(Func<IntakeResult> work) => Task.Run(() =>
	{
		try
		{
			return HackAddResult.From(work());
		}
		catch (Exception ex)
		{
			return new HackAddResult($"PKHaX could not read this file: {ex.Message}", false);
		}
	});

	public static string TooLarge(ReadOnlySpan<byte> head, long? size, string name)
	{
		var verdict = DropClassifier.Classify(head, size ?? MaxRomBytes + 1, name);
		if (verdict.IsRom)
		{
			return size is { } known ? IntakeText.TooLarge(known, MaxRomBytes)
				: string.Create(CultureInfo.InvariantCulture, $"{IntakeText.CannotRead} (the image is larger than the {MaxRomBytes:N0} bytes this device reads.)");
		}
		return string.Create(CultureInfo.InvariantCulture, $"{name} is larger than {MaxRomBytes:N0} bytes, so PKHaX Mobile does not read it.");
	}

	public static async Task<PickedFile> ReadCappedAsync(Stream input, string name, long? size)
	{
		if (size is { } known && known <= MaxRomBytes)
			return await ReadKnownAsync(input, name, known);
		using var buffer = new MemoryStream();
		var chunk = new byte[ChunkBytes];
		int n;
		while ((n = await input.ReadAsync(chunk.AsMemory())) > 0)
		{
			if (buffer.Length + n > MaxRomBytes)
			{
				buffer.Write(chunk, 0, Math.Min(n, (int)Math.Max(0, DropClassifier.HeadBytes - buffer.Length)));
				var head = buffer.GetBuffer().AsSpan(0, (int)Math.Min(buffer.Length, DropClassifier.HeadBytes));
				return PickedFile.Refused(name, size, TooLarge(head, size, name));
			}
			buffer.Write(chunk, 0, n);
		}
		return new PickedFile(buffer.ToArray(), name, buffer.Length, null);
	}

	private static async Task<PickedFile> ReadKnownAsync(Stream input, string name, long size)
	{
		var data = new byte[size];
		var got = 0;
		int n;
		while (got < data.Length && (n = await input.ReadAsync(data.AsMemory(got))) > 0)
			got += n;
		if (got < data.Length)
			return PickedFile.Refused(name, size, ShorterThanSize);
		var probe = new byte[1];
		if (await input.ReadAsync(probe.AsMemory()) > 0)
			return PickedFile.Refused(name, size, LongerThanSize);
		return new PickedFile(data, name, size, null);
	}

	public static async Task<PickedFile?> PickAsync(ISaveFileGateway fallback)
	{
#if ANDROID
		return await PickAndroidAsync();
#else
		if (await fallback.PickSaveAsync() is not { } picked)
			return null;
		if (picked.Bytes.LongLength > MaxRomBytes)
			return PickedFile.Refused(picked.Handle.DisplayName, picked.Bytes.LongLength, TooLarge(picked.Bytes.AsSpan(0, Math.Min(picked.Bytes.Length, DropClassifier.HeadBytes)), picked.Bytes.LongLength, picked.Handle.DisplayName));
		return new PickedFile(picked.Bytes, picked.Handle.DisplayName, picked.Bytes.LongLength, null);
#endif
	}

#if ANDROID
	private static async Task<PickedFile?> PickAndroidAsync()
	{
		var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionOpenDocument);
		intent.AddCategory(global::Android.Content.Intent.CategoryOpenable);
		intent.SetType("*/*");
		intent.PutExtra(global::Android.Content.Intent.ExtraAllowMultiple, false);
		intent.AddFlags(global::Android.Content.ActivityFlags.GrantReadUriPermission);
		var uri = await global::PKHaX.Mobile.Platforms.Android.SafBridge.PickAsync(intent);
		if (uri is null)
			return null;
		var resolver = global::Android.App.Application.Context.ContentResolver
			?? throw new InvalidOperationException("No ContentResolver.");
		var (name, size) = Describe(resolver, uri);
		using var input = resolver.OpenInputStream(uri)
			?? throw new InvalidOperationException("Could not open the file for reading.");
		if (size is { } known && known > MaxRomBytes)
		{
			var head = new byte[DropClassifier.HeadBytes];
			var got = 0;
			int n;
			while (got < head.Length && (n = await input.ReadAsync(head.AsMemory(got))) > 0)
				got += n;
			return PickedFile.Refused(name, known, TooLarge(head.AsSpan(0, got), known, name));
		}
		return await ReadCappedAsync(input, name, size);
	}

	private static (string Name, long? Size) Describe(global::Android.Content.ContentResolver resolver, global::Android.Net.Uri uri)
	{
		var name = uri.LastPathSegment ?? "file";
		long? size = null;
		try
		{
			using var cursor = resolver.Query(uri, null, null, null, null);
			if (cursor is not null && cursor.MoveToFirst())
			{
				var ni = cursor.GetColumnIndex(global::Android.Provider.IOpenableColumns.DisplayName);
				if (ni >= 0 && !cursor.IsNull(ni) && cursor.GetString(ni) is { Length: > 0 } shown)
					name = shown;
				var si = cursor.GetColumnIndex(global::Android.Provider.IOpenableColumns.Size);
				if (si >= 0 && !cursor.IsNull(si))
					size = cursor.GetLong(si);
			}
		}
		catch (Exception)
		{
			size = null;
		}
		return (name, size is >= 0 ? size : null);
	}
#endif
}
