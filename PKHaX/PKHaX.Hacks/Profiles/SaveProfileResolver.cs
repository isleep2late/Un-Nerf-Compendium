using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace PKHaX.Hacks.Profiles;

public enum ResolveHow
{
    Hint,
    Signature,
    SiblingRom,
    ProfileFile,
    Pairing,
    Ask,
    Answer,
}

public sealed record ProfileHint(string? ProfileId = null, string? ImageKey = null, PkhaxProfile? Profile = null, string? RomPath = null)
{
    public bool NamesAnImage => Profile is not null || ProfileId is not null || ImageKey is not null;
}

public sealed record SaveResolution(PkhaxProfile? Profile, ResolveHow How, IReadOnlyList<PkhaxProfile> Alternatives, IReadOnlyList<string> Trail)
{
    public const string Question = "Which game is this save from?";

    public static IReadOnlyList<string> ChoicesFor(string? detectedClass) =>
        ["Choose ROM...", "Choose profile file...", detectedClass is { Length: > 0 } c ? $"Vanilla {c}" : "Vanilla"];

    public bool Resolved => Profile is not null;

    public string? RomPath { get; init; }

    public string? RomSha1 { get; init; }
}

public sealed class SaveProfileResolver
{
    public const uint VanillaGen3Signature = 0x08012025;
    public const int Gen3SectorSize = 0x1000;
    public const int Gen3SignatureOffset = 0xFF8;
    public const int Gen3SectionsPerSlot = 14;
    public const int Gen3MinSave = 0x10000;
    public const int Gen3MaxSave = 0x20000;
    public const int Gen3FooterSlack = 0x40;
    public const long DefaultMaxRomBytes = 512L * 1024 * 1024;

    public static readonly IReadOnlyList<string> RomExtensions = [".gba", ".gb", ".gbc", ".nds"];

    private static readonly ConcurrentDictionary<(string Path, long Size, DateTime Mtime), string> RomHashes = new();

    private readonly ProfileStore store;
    private readonly PairingMemory? pairings;
    private readonly long maxRomBytes;
    private int hashesComputed;

    public SaveProfileResolver(ProfileStore store, PairingMemory? pairings = null, long maxRomBytes = DefaultMaxRomBytes)
    {
        this.store = store;
        this.pairings = pairings;
        this.maxRomBytes = maxRomBytes;
    }

    public int HashesComputed => hashesComputed;
    public long MaxRomBytes => maxRomBytes;
    public ProfileStore Store => store;
    public PairingMemory? Pairings => pairings;

    public SaveResolution Resolve(ReadOnlySpan<byte> save, string? savePath, ProfileHint? hint = null, SaveTrainer? trainer = null,
        string? pickedProfileFile = null, Func<PkhaxProfile, bool>? fits = null)
    {
        var resolution = Decide(save, savePath, hint, trainer, pickedProfileFile, fits, out var romPath, out var romSha1);
        return romPath is null ? resolution : resolution with { RomPath = romPath, RomSha1 = romSha1 };
    }

    public string? RomSha1Of(string romPath)
    {
        try
        {
            return HashRom(romPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private SaveResolution Decide(ReadOnlySpan<byte> save, string? savePath, ProfileHint? hint, SaveTrainer? trainer,
        string? pickedProfileFile, Func<PkhaxProfile, bool>? fits, out string? romPath, out string? romSha1)
    {
        romPath = null;
        romSha1 = null;
        var trail = new List<string>();
        bool Fit(PkhaxProfile? p, string step)
        {
            if (p is null)
                return false;
            if (fits is null || fits(p))
                return true;
            trail.Add($"{step}: {p.Id} ({p.Platform}) does not fit this save, so it is not used");
            return false;
        }

        if (FromHint(hint, trail) is { } h && Fit(h, "hint"))
            return new SaveResolution(h, ResolveHow.Hint, [], trail);

        var signed = BySignature(save, trail).Where(p => Fit(p, "signature")).ToList();
        if (signed.Count == 1)
            return new SaveResolution(signed[0], ResolveHow.Signature, [], trail);
        romPath = hint?.RomPath ?? SiblingRom(savePath);
        var rom = romPath is null ? null : ByRom(romPath, trail, out romSha1);
        if (!Fit(rom, "ROM"))
            rom = null;
        if (signed.Count > 1)
        {
            if (rom is not null && signed.Contains(rom))
            {
                trail.Add($"the ROM {SafeName(romPath)} picks {rom.Id} among the signed profiles");
                return new SaveResolution(rom, ResolveHow.Signature, signed.Where(p => p != rom).ToList(), trail);
            }
            return new SaveResolution(null, ResolveHow.Ask, signed, trail);
        }
        if (rom is not null)
            return new SaveResolution(rom, ResolveHow.SiblingRom, [], trail);

        var file = ByProfileFile(pickedProfileFile ?? ProfileFileFor(savePath), trail);
        if (Fit(file, "profile file"))
            return new SaveResolution(file, ResolveHow.ProfileFile, [], trail);

        var pair = ByPairing(savePath, trainer, hint?.NamesAnImage == true, trail, out var several);
        if (Fit(pair, "pairing"))
            return new SaveResolution(pair, ResolveHow.Pairing, [], trail);
        several = several.Where(p => Fit(p, "pairing")).ToList();
        if (several.Count > 0)
            return new SaveResolution(null, ResolveHow.Ask, several, trail);
        trail.Add(SaveResolution.Question);
        return new SaveResolution(null, ResolveHow.Ask, [], trail);
    }

    public bool Remember(string savePath, SaveTrainer? trainer, PkhaxProfile profile) => pairings?.Remember(savePath, trainer, profile.Id) ?? false;

    public static string? SiblingRom(string? savePath) =>
        Beside(savePath, out var stem) ? RomExtensions.Select(e => stem + e).FirstOrDefault(File.Exists) : null;

    public static string? ProfileFileFor(string? savePath) =>
        Beside(savePath, out var stem) && File.Exists(stem + PkhaxProfile.FileSuffix) ? stem + PkhaxProfile.FileSuffix : null;

    private static bool Beside(string? savePath, out string stem)
    {
        stem = "";
        if (PairingMemory.Normalise(savePath) is not { } full || PairingMemory.IsContentToken(full))
            return false;
        try
        {
            stem = Path.Combine(Path.GetDirectoryName(full) ?? "", Path.GetFileNameWithoutExtension(full));
            return stem.Length > 0;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static IReadOnlyList<uint> Gen3Signatures(ReadOnlySpan<byte> save)
    {
        if (save.Length < Gen3MinSave || save.Length > Gen3MaxSave + Gen3FooterSlack || save.Length % Gen3SectorSize > Gen3FooterSlack)
            return [];
        var counts = new SortedDictionary<uint, int>();
        for (var at = 0; at + Gen3SectorSize <= save.Length; at += Gen3SectorSize)
        {
            var sig = BinaryPrimitives.ReadUInt32LittleEndian(save.Slice(at + Gen3SignatureOffset, 4));
            if (sig is not (0 or 0xFFFFFFFF))
                counts[sig] = counts.TryGetValue(sig, out var n) ? n + 1 : 1;
        }
        return counts.Where(kv => kv.Value >= Gen3SectionsPerSlot).Select(kv => kv.Key).ToList();
    }

    private PkhaxProfile? FromHint(ProfileHint? hint, List<string> trail)
    {
        if (hint is null)
            return null;
        if (hint.Profile is { } p)
        {
            trail.Add($"hint: the booted image's profile {p.Id}");
            return p;
        }
        if (store.ById(hint.ProfileId) is { } byId)
        {
            trail.Add($"hint: profile id {byId.Id}");
            return byId;
        }
        var list = store.ForImage(hint.ImageKey);
        if (list.Count == 1)
        {
            trail.Add($"hint: image {hint.ImageKey} is profile {list[0].Id}");
            return list[0];
        }
        if (hint.NamesAnImage)
            trail.Add($"hint: {hint.ProfileId ?? hint.ImageKey} names {(list.Count == 0 ? "no stored profile" : $"{list.Count} profiles")}; the other steps decide, without trainer pairings");
        return null;
    }

    private IReadOnlyList<PkhaxProfile> BySignature(ReadOnlySpan<byte> save, List<string> trail)
    {
        var result = new List<PkhaxProfile>();
        foreach (var sig in Gen3Signatures(save).Where(s => s != VanillaGen3Signature))
        {
            var list = store.WithSaveSignature(sig);
            trail.Add($"signature 0x{sig:X8}: {(list.Count == 0 ? "no profile" : string.Join(", ", list.Select(p => p.Id)))}");
            result.AddRange(list.Where(p => !result.Contains(p)));
        }
        return result;
    }

    private PkhaxProfile? ByRom(string romPath, List<string> trail, out string? sha1)
    {
        sha1 = null;
        try
        {
            sha1 = HashRom(romPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            trail.Add($"ROM {SafeName(romPath)}: not read ({ex.Message})");
            return null;
        }
        if (sha1 is null)
        {
            trail.Add($"ROM {SafeName(romPath)}: larger than {maxRomBytes} bytes, not hashed here");
            return null;
        }
        var list = store.ForImage(sha1);
        trail.Add($"ROM {SafeName(romPath)} sha1 {sha1}: {(list.Count == 0 ? "no profile (Analyse ROM... makes one)" : string.Join(", ", list.Select(p => p.Id)))}");
        return list.Count == 1 ? list[0] : null;
    }

    private string? HashRom(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > maxRomBytes)
            return null;
        var key = (info.FullName, info.Length, info.LastWriteTimeUtc);
        if (RomHashes.TryGetValue(key, out var known))
            return known;
        using var stream = File.OpenRead(path);
        var sha1 = Convert.ToHexStringLower(SHA1.HashData(stream));
        System.Threading.Interlocked.Increment(ref hashesComputed);
        RomHashes[key] = sha1;
        return sha1;
    }

    private static PkhaxProfile? ByProfileFile(string? path, List<string> trail)
    {
        if (path is null)
            return null;
        try
        {
            if (new FileInfo(path).Length > ProfileStore.MaxProfileBytes)
            {
                trail.Add($"profile file {SafeName(path)}: larger than {ProfileStore.MaxProfileBytes} bytes");
                return null;
            }
            var r = PkhaxProfileReader.Load(File.ReadAllText(path));
            trail.Add($"profile file {SafeName(path)}: {(r.Ok ? r.Profile!.Id : "refused: " + r.Refusal)}");
            return r.Profile;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            trail.Add($"profile file {SafeName(path)}: not read ({ex.Message})");
            return null;
        }
    }

    private PkhaxProfile? ByPairing(string? savePath, SaveTrainer? trainer, bool imageKnown, List<string> trail, out IReadOnlyList<PkhaxProfile> several)
    {
        several = [];
        if (pairings is null)
            return null;
        if (pairings.ByPath(savePath) is { } byPath)
        {
            if (!byPath.SameTrainer(trainer))
                trail.Add($"pairing: {SafeName(savePath)} was paired with {byPath.ProfileId} for trainer {byPath.TrainerName} ({byPath.TrainerId}), but this save's trainer is {trainer!.Name} ({trainer.Id}); not used");
            else if (store.ById(byPath.ProfileId) is { } p)
            {
                trail.Add($"pairing: {SafeName(savePath)} was paired with {p.Id}");
                return p;
            }
            else
                trail.Add($"pairing: {SafeName(savePath)} was paired with {byPath.ProfileId}, which is no longer stored");
        }
        if (imageKnown)
            return null;
        var ids = pairings.ByTrainer(trainer).Where(id => store.ById(id) is not null).ToList();
        if (ids.Count == 1)
        {
            trail.Add($"pairing: trainer {trainer!.Name} ({trainer.Id}) was paired with {ids[0]}");
            return store.ById(ids[0]);
        }
        if (ids.Count > 1)
        {
            trail.Add($"pairing: trainer {trainer!.Name} ({trainer.Id}) was paired with {string.Join(", ", ids)}");
            several = ids.Select(id => store.ById(id)!).ToList();
        }
        return null;
    }

    private static string SafeName(string? path)
    {
        try
        {
            return Path.GetFileName(path) ?? "";
        }
        catch (ArgumentException)
        {
            return "(unreadable path)";
        }
    }
}
