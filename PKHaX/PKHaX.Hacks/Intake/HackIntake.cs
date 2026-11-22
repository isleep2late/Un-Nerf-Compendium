using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Profiles;
using PKHaX.Hacks.Readers;

namespace PKHaX.Hacks.Intake;

public sealed record IntakeResult(StoredKit? Stored, string? Refusal)
{
    public bool Ok => Stored is not null && Refusal is null;

    public HackKit? Kit => Stored?.Kit;

    public bool FromStore { get; init; }

    public PatchProbe? Probe { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public string? StoreProblem { get; init; }

    public string? Sentence => Refusal ?? (Stored is { Kit: { } kit } ? IntakeText.Done(kit.Name, kit.Counts.Species, kit.Counts.Moves, kit.Counts.Items, kit.Counts.Abilities, kit.Shape ?? kit.SaveFamily) : null);

    public static IntakeResult Refused(string why, PatchProbe? probe = null) => new(null, why) { Probe = probe };
}

public static class IntakeHash
{
    private static readonly uint[] Table = Build();

    private static uint[] Build()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    public static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    public static string Sha1(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA1.HashData(data));
}

public sealed class HackIntake
{
    private readonly Func<ReadOnlyMemory<byte>, string?, HackDataResult> tables;
    private readonly Func<ReadOnlyMemory<byte>, SaveShape> shapes;
    private int readerCalls;

    public HackIntake(HackKitStore store, Func<ReadOnlyMemory<byte>, string?, HackDataResult>? tables = null, Func<ReadOnlyMemory<byte>, SaveShape>? shapes = null,
        long maxRomBytes = long.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(store);
        Store = store;
        this.tables = tables ?? ((image, sym) => HackTables.Read(image, sym));
        this.shapes = shapes ?? (image => Gen3ShapeFinder.Derive(image));
        MaxRomBytes = maxRomBytes;
    }

    public HackKitStore Store { get; }

    public long MaxRomBytes { get; }

    public int ReaderCalls => readerCalls;

    public static string NameOf(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? "");
        foreach (var suffix in new[] { PkhaxProfile.FileSuffix, ProfileBundleReader.Extension })
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return name[..^suffix.Length];
        }
        var stem = Path.GetFileNameWithoutExtension(name);
        return stem.Length > 0 ? stem : "hack";
    }

    public IntakeResult Derive(ReadOnlyMemory<byte> rom, string name, string? sym = null, IProgress<string>? progress = null, string? romPath = null) =>
        DeriveFrom(rom, name, sym, progress, new KitSource(Path.GetFileName(name), rom.Length, RomPath: romPath), []);

    private IntakeResult DeriveFrom(ReadOnlyMemory<byte> rom, string name, string? sym, IProgress<string>? progress, KitSource source, IReadOnlyList<string> notes)
    {
        if (rom.Length > MaxRomBytes)
            return IntakeResult.Refused(IntakeText.TooLarge(rom.Length, MaxRomBytes));
        progress?.Report("hashing the image");
        var key = IntakeHash.Sha1(rom.Span);
        if (Store.Usable(key) is { } stored)
            return Completed(stored, rom, source, notes, progress);
        var platform = HackTables.Sniff(rom.Span);
        if (platform == HackTables.Ctr)
            return IntakeResult.Refused(IntakeText.CannotRead);
        progress?.Report("reading the tables");
        readerCalls++;
        var read = tables(rom, sym);
        if (read.Data is not { } data)
            return IntakeResult.Refused($"PKHaX could not read this hack's tables: {read.Refusal ?? "the reader gave no reason"}");
        if (!string.Equals(data.RomSha1, key, StringComparison.Ordinal))
        {
            notes = [.. notes, $"the reader named the image {data.RomSha1}; the kit is keyed by the image's own SHA-1 {key}"];
            data = data with { RomSha1 = key };
        }
        platform = data.Platform ?? platform ?? HackTables.Gb;
        SaveShape? shape = null;
        if (platform == HackTables.Gba)
        {
            progress?.Report("reading the save shape");
            shape = shapes(rom);
        }
        var family = shape is null ? HackKit.Vanilla : shape.Known ? shape.Family : SaveShape.UnknownFamily;
        var report = new List<string> { HackTables.StatusLine(data) };
        report.AddRange(data.Notes);
        if (shape is not null)
        {
            report.Add($"save shape: {shape.Describe()}");
            report.AddRange(shape.Notes);
        }
        report.AddRange(notes);
        var kit = new HackKit
        {
            Key = key,
            Name = NameOf(name),
            Platform = platform,
            BaseGame = HackKit.BaseGameOf(platform, data.GameCode, data.Engine, HackKit.GbTitle(rom.Span)),
            GameCode = data.GameCode,
            Engine = data.Engine,
            SaveFamily = family,
            Shape = shape?.Describe(),
            Counts = data.Counts,
            Report = report,
            ReaderVersion = data.ReaderVersion,
            Source = source,
            At = DateTimeOffset.UtcNow,
        };
        try
        {
            progress?.Report("storing the tables");
            var saved = Store.Save(kit, data, shape);
            var (learned, said) = Wanted(saved, rom, notes);
            return new IntakeResult(learned, null) { Notes = said };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new IntakeResult(new StoredKit(kit, data, shape, HackKitStore.ProfileFor(kit)), null) { Notes = notes, StoreProblem = $"the kit could not be stored ({ex.Message})" };
        }
    }

    private IntakeResult Completed(StoredKit stored, ReadOnlyMemory<byte> rom, KitSource source, IReadOnlyList<string> notes, IProgress<string>? progress)
    {
        var result = Refreshed(stored, rom, source, notes, progress);
        if (result.Stored is not { } kept)
            return result;
        var (learned, said) = Wanted(kept, rom, result.Notes);
        return result with { Stored = learned, Notes = said };
    }

    private (StoredKit Stored, IReadOnlyList<string> Notes) Wanted(StoredKit stored, ReadOnlyMemory<byte> rom, IReadOnlyList<string> notes)
    {
        IReadOnlyList<uint> wanted;
        try
        {
            wanted = Store.WantedSignatures(stored.Kit.Key);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return (stored, notes);
        }
        if (wanted.Count == 0 || stored.Shape is not { Known: true } shape || rom.Length is < 0x200 or > GbaImage.MaxLength)
            return (stored, notes);
        var image = new GbaImage(rom);
        var learned = shape;
        var found = new List<string>();
        var dropped = new List<string>();
        var absent = new List<uint>();
        foreach (var signature in wanted)
        {
            if (Gen3ShapeFinder.AcceptSignatureValue(learned, image, signature, out var why) is { } accepted)
            {
                if (!ReferenceEquals(accepted, learned))
                    found.Add(IntakeText.SignatureFound(signature));
                learned = accepted;
            }
            else
            {
                absent.Add(signature);
                dropped.Add(IntakeText.SignatureNotFound(signature, why ?? "the save layout did not take it"));
            }
        }
        var said = new List<string>(notes);
        var kept = stored;
        if (!ReferenceEquals(learned, shape))
        {
            try
            {
                kept = stored.Data is { } data ? Store.Save(stored.Kit with { Shape = learned.Describe() }, data, learned) : stored with { Shape = learned };
                if (stored.Data is null)
                    Store.UpdateShape(learned);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                said.AddRange(dropped);
                said.Add($"the signatures a save asked for were found in this ROM but could not be stored ({ex.Message}), so add the ROM again to retry");
                return (stored, said);
            }
        }
        said.AddRange(found);
        said.AddRange(dropped);
        try
        {
            Store.ForgetWanted(stored.Kit.Key, absent);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            said.Add($"the list of signatures a save asked for could not be cleared ({ex.Message})");
        }
        return (kept, said);
    }

    private IntakeResult Refreshed(StoredKit stored, ReadOnlyMemory<byte> rom, KitSource source, IReadOnlyList<string> notes, IProgress<string>? progress)
    {
        var addPath = source.RomPath is { Length: > 0 } && stored.Kit.Source.RomPath is null;
        var addShape = stored.Kit.Platform == HackTables.Gba && stored.Shape is null;
        if (!addPath && !addShape)
            return new IntakeResult(stored, null) { FromStore = true, Notes = notes };
        var kit = stored.Kit;
        var shape = stored.Shape;
        var added = new List<string>(notes);
        if (addPath)
        {
            kit = kit with { Source = kit.Source with { RomPath = source.RomPath } };
            added.Add("the stored kit now remembers where its ROM is");
        }
        if (addShape)
        {
            progress?.Report("reading the save shape");
            shape = shapes(rom);
            kit = kit with
            {
                SaveFamily = shape.Known ? shape.Family : SaveShape.UnknownFamily,
                Shape = shape.Describe(),
                Report = [.. kit.Report, $"save shape: {shape.Describe()}", .. shape.Notes],
            };
            added.Add($"the stored kit had no save shape; read from this ROM: {shape.Describe()}");
        }
        try
        {
            return new IntakeResult(Store.Save(kit, stored.Data!, shape), null) { FromStore = true, Notes = added };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new IntakeResult(new StoredKit(kit, stored.Data, shape, HackKitStore.ProfileFor(kit)), null) { FromStore = true, Notes = added, StoreProblem = $"the kit could not be updated ({ex.Message})" };
        }
    }

    public IntakeResult FromPatch(ReadOnlyMemory<byte> patch, ReadOnlyMemory<byte> baseRom, IRomPatcher patcher, string patchName, string? basePath = null, IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(patcher);
        PatchProbe probe;
        try
        {
            probe = patcher.Inspect(patch.Span);
        }
        catch (RomPatchRefusedException ex)
        {
            return IntakeResult.Refused($"the patch does not read: {ex.Message}");
        }
        if (!probe.Known)
            return IntakeResult.Refused("the file is not a patch PKHaX applies (IPS, IPS32, UPS, BPS or xdelta)", probe);
        if (probe.TargetSize is { } targetSize && targetSize > MaxRomBytes)
            return IntakeResult.Refused(IntakeText.TooLarge(targetSize, MaxRomBytes), probe);
        var baseCrc = IntakeHash.Crc32(baseRom.Span);
        var baseSize = (long)baseRom.Length;
        if (probe.SourceCrc32 is { } wanted && (wanted != baseCrc || (probe.SourceSize is { } wantedSize && wantedSize != baseSize)))
            return IntakeResult.Refused(IntakeText.WrongBase(probe.Name, wanted, probe.SourceSize, baseCrc, baseSize), probe);
        progress?.Report("applying the patch in memory");
        byte[] image;
        try
        {
            image = patcher.Apply(baseRom.Span, patch.Span);
        }
        catch (RomPatchRefusedException ex)
        {
            return IntakeResult.Refused(ex.WrongSource ? IntakeText.WrongBaseUnstated(probe.Name, baseCrc, baseSize, ex.Message) : $"the patch did not apply: {ex.Message}", probe);
        }
        var ips = probe.Format is PatchKind.Ips or PatchKind.Ips32;
        var notes = new List<string>();
        if (ips)
        {
            var before = DropClassifier.Classify(baseRom.Span, baseRom.Length, null);
            var after = DropClassifier.Classify(image, image.Length, null);
            if (!before.IsRom || !after.IsRom || before.Platform != after.Platform)
                return IntakeResult.Refused(IntakeText.IpsNotARom, probe);
            notes.Add(IntakeText.IpsNote);
        }
        var baseSha1 = IntakeHash.Sha1(baseRom.Span);
        var source = new KitSource(Path.GetFileName(patchName), patch.Length, IntakeHash.Sha1(patch.Span), baseSha1, probe.Name, ips ? IntakeText.IpsNote : null);
        var result = DeriveFrom(image, patchName, null, progress, source, notes) with { Probe = probe };
        if (result.Ok && basePath is { Length: > 0 })
        {
            try
            {
                Store.RememberBase(new KitBase(Path.GetFullPath(basePath), baseSize, baseCrc, baseSha1));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                result = result with { Notes = [.. result.Notes, $"the base was not remembered ({ex.Message})"] };
            }
        }
        return result;
    }

    public IntakeResult FromProfile(ReadOnlyMemory<byte> file, string name)
    {
        PkhaxProfile? profile;
        HackData? data;
        var head = file.Span[..Math.Min(file.Length, DropClassifier.HeadBytes)];
        if (DropClassifier.Classify(head, file.Length, name).Kind == DropKind.Bundle)
        {
            var read = ProfileBundleReader.Read(file);
            if (!read.Ok)
                return IntakeResult.Refused($"the bundle is not used: {read.Refusal}");
            profile = read.Profile;
            data = read.Data;
        }
        else
        {
            if (file.Length > ProfileStore.MaxProfileBytes)
                return IntakeResult.Refused($"the profile is larger than {ProfileStore.MaxProfileBytes:N0} bytes");
            var loaded = PkhaxProfileReader.Load(Encoding.UTF8.GetString(file.Span));
            if (loaded.Profile is not { } p)
                return IntakeResult.Refused($"the profile is not used: {loaded.Refusal}");
            profile = p;
            data = ProfileBundleReader.KeyOf(p) is { } key ? ProfileBundleReader.Embedded(p, key) : null;
            if (data is null)
                return IntakeResult.Refused($"the profile holds no embedded {HackData.Schema} tables, so there is nothing to keep for it; export it with its tables");
        }
        if (profile is null || data is null)
            return IntakeResult.Refused("the profile or its tables are missing");
        var platform = data.Platform ?? profile.Platform switch
        {
            "gba" => HackTables.Gba,
            "gb" or "gbc" => HackTables.Gb,
            "nds" => HackTables.Nds,
            "3ds" => HackTables.Ctr,
            _ => HackTables.Nx,
        };
        var kit = new HackKit
        {
            Key = data.RomSha1,
            Name = profile.DisplayName,
            Platform = platform,
            BaseGame = HackKit.BaseGameOf(platform, data.GameCode, data.Engine),
            GameCode = data.GameCode,
            Engine = data.Engine,
            SaveFamily = profile.SaveVariant is { } variant && HackSaveContext.IsVanilla(variant) ? HackKit.Vanilla : SaveShape.UnknownFamily,
            Counts = data.Counts,
            Report = [HackTables.StatusLine(data), $"from the profile {profile.Id}; no ROM was read on this device", .. data.Notes],
            ReaderVersion = data.ReaderVersion,
            Source = new KitSource(Path.GetFileName(name), file.Length) { FromProfile = true },
            At = DateTimeOffset.UtcNow,
        };
        if (!HackKitStore.IsKey(kit.Key))
            return IntakeResult.Refused($"the profile's tables name '{kit.Key}', not a SHA-1, so they cannot be keyed");
        if (Store.Usable(kit.Key) is { Kit.Source.FromProfile: false } rom)
            return new IntakeResult(rom, null) { FromStore = true, Notes = ["a kit read from the ROM is already stored; the profile was not used"] };
        try
        {
            return new IntakeResult(Store.Save(kit, data, null), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new IntakeResult(new StoredKit(kit, data, null, HackKitStore.ProfileFor(kit)), null) { StoreProblem = $"the kit could not be stored ({ex.Message})" };
        }
    }
}
