using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Overlay;
using PKHaX.Hacks.Profiles;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;

namespace PKHaX.Hacks.Intake;

public enum OpenKind
{
    Stock,
    Native,
    Ask,
    Refused,
}

public sealed record OpenHint
{
    public string? KitKey { get; init; }
    public bool Answered { get; init; }
    public bool NoKit { get; init; }
    public bool Live { get; init; }
    public bool Unmodified { get; init; }
    public ReadOnlyMemory<byte> Rom { get; init; }
    public string Language { get; init; } = "en";
}

public sealed class OpenResult : IDisposable
{
    private IDisposable? scope;
    private readonly string? stockRefusal;
    private int disposed;
    private int asking;

    internal OpenResult(OpenKind kind, string? sentence, StoredKit? kit, IReadOnlyList<StoredKit> choices, IReadOnlyList<string> notes)
    {
        Kind = kind;
        Sentence = sentence;
        Kit = kit;
        Choices = choices;
        Notes = notes;
        Legality = kit is null ? LegalityLevel.Advisory : LegalityMode.For(ImageKind.Imported);
    }

    internal OpenResult(SaveFile stock, StoredKit? kit, IDisposable? scope, string? stockRefusal, int? uneditedDiff, IReadOnlyList<StoredKit> choices, IReadOnlyList<string> notes)
        : this(OpenKind.Stock, null, kit, choices, notes)
    {
        Stock = stock;
        this.scope = scope;
        this.stockRefusal = stockRefusal;
        UneditedDiff = uneditedDiff;
    }

    internal OpenResult(SaveFile stock, int uneditedDiff, IReadOnlyList<StoredKit> choices, IReadOnlyList<string> notes, string question, string confirmLabel, bool answered)
        : this(stock, null, null, null, uneditedDiff, choices, notes)
    {
        UnmodifiedQuestion = question;
        ConfirmLabel = confirmLabel;
        asking = answered ? 0 : 1;
    }

    internal OpenResult(HackSave hack, HackSaveModel model, StoredKit kit, SaveShape shape, TrialResult trial, WriteGateVerdict gate, ResolveHow? how, IReadOnlyList<string> notes)
        : this(OpenKind.Native, null, kit, [], notes)
    {
        Hack = hack;
        Native = model;
        Shape = shape;
        Trial = trial;
        Gate = gate;
        How = how;
    }

    public OpenKind Kind { get; }
    public string? Sentence { get; }
    public StoredKit? Kit { get; }
    public IReadOnlyList<StoredKit> Choices { get; }
    public IReadOnlyList<string> Notes { get; }
    public LegalityLevel Legality { get; }
    public SaveFile? Stock { get; }
    public HackSave? Hack { get; }
    public HackSaveModel? Native { get; }
    public SaveShape? Shape { get; }
    public TrialResult? Trial { get; }
    public WriteGateVerdict? Gate { get; }
    public ResolveHow? How { get; init; }
    public int? UneditedDiff { get; }
    public string? UnmodifiedQuestion { get; }
    public string? ConfirmLabel { get; }

    public bool AsksUnmodified => UnmodifiedQuestion is not null && Volatile.Read(ref asking) == 1;

    public bool ConfirmedUnmodified => UnmodifiedQuestion is not null && Volatile.Read(ref asking) == 0;

    public string? Banner => AsksUnmodified ? RetailQuestion.Banner(UnmodifiedQuestion) : null;

    public bool ConfirmUnmodified() => UnmodifiedQuestion is not null && Interlocked.Exchange(ref asking, 0) == 1;

    public bool ShowsLegality => LegalityMode.ShowsLegalityPanel(Legality);

    public bool OverlayActive => scope is not null && Volatile.Read(ref disposed) == 0;

    public string? WriteRefusal => Kind switch
    {
        OpenKind.Native => Hack?.ReadOnlyReason,
        OpenKind.Stock => AsksUnmodified ? RetailQuestion.Refusal(UnmodifiedQuestion) : stockRefusal,
        _ => Sentence ?? IntakeText.NotASave,
    };

    public bool CanWrite => WriteRefusal is null;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        Interlocked.Exchange(ref scope, null)?.Dispose();
    }
}

public static class HackOpen
{
    private static readonly object Gate = new();
    private static OpenResult? current;

    public static IReadOnlyList<ResolveHow> WritableMatches { get; } = [ResolveHow.Signature, ResolveHow.SiblingRom, ResolveHow.ProfileFile, ResolveHow.Answer];

    public static OpenResult? Current
    {
        get
        {
            lock (Gate)
                return current;
        }
    }

    public static void Close()
    {
        lock (Gate)
        {
            current?.Dispose();
            current = null;
        }
    }

    public static string? CfruContainer(ReadOnlyMemory<byte> file)
    {
        var data = GbaSectorContainer.TrimFooter(file);
        if (data.Length != GbaSectorContainer.FileSize)
            return null;
        foreach (var variant in CfruVariant.All)
        {
            try
            {
                if (CfruSave.TryOpen(data, WriteEvidence.Unproven, out _, variant.Id) is not null)
                    return SignatureOf(data.Span, variant) is { } signature && signature != GbaSectorContainer.RetailSignature ? $"signature 0x{signature:X8}" : "retail signature";
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException)
            {
            }
        }
        return null;
    }

    private static uint? SignatureOf(ReadOnlySpan<byte> data, CfruVariant variant)
    {
        for (var at = 0; at + SectionLayout.SectorSize <= data.Length; at += SectionLayout.SectorSize)
        {
            var signature = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(at + GbaSectorContainer.SignatureOffset, 4));
            if (variant.AcceptedSignatures.Contains(signature))
                return signature;
        }
        return null;
    }

    public static SaveProfileResolver ResolverFor(HackKitStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return new SaveProfileResolver(ProfileStore.Create(store.ProfileDocuments()), new PairingMemory(store.PairingsPath));
    }

    public static string? KeyOfProfile(PkhaxProfile? profile) =>
        profile?.Id is { } id && id.StartsWith(HackKit.ProfilePrefix, StringComparison.Ordinal) && HackKitStore.IsKey(id[HackKit.ProfilePrefix.Length..]) ? id[HackKit.ProfilePrefix.Length..] : null;

    public static OpenResult Open(ReadOnlyMemory<byte> save, string? savePath, HackKitStore store, SaveProfileResolver? resolver = null, OpenHint? hint = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        Close();
        hint ??= new OpenHint();
        var result = Decide(save.ToArray(), savePath, store, resolver, hint);
        if (hint.Answered && savePath is not null && result is { Kind: OpenKind.Stock, Stock: { } stock, Kit: { } kit })
            Pair(store, resolver, savePath, new SaveTrainer(stock.ID32, stock.OT), kit);
        if (result.OverlayActive)
        {
            lock (Gate)
                current = result;
        }
        return result;
    }

    public static bool Remember(HackKitStore store, string path, SaveFile save, StoredKit kit)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(kit);
        return Pair(store, null, path, new SaveTrainer(save.ID32, save.OT), kit);
    }

    private static bool Pair(HackKitStore store, SaveProfileResolver? resolver, string path, SaveTrainer? trainer, StoredKit kit)
    {
        if (string.IsNullOrWhiteSpace(path) || !HackKitStore.IsKey(kit.Kit.Key))
            return false;
        try
        {
            return (resolver?.Pairings ?? new PairingMemory(store.PairingsPath)).Remember(path, trainer, kit.Kit.ProfileId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static OpenResult Decide(byte[] bytes, string? savePath, HackKitStore store, SaveProfileResolver? resolver, OpenHint hint)
    {
        var notes = new List<string>();
        var cfru = CfruContainer(bytes);
        StoredKit? kit = null;
        ResolveHow? how = null;
        IReadOnlyList<StoredKit> asked = [];
        if (hint.KitKey is { } wantedKey)
        {
            var named = store.Load(wantedKey.ToLowerInvariant());
            if (named is { Usable: true })
            {
                kit = named;
                how = hint.Answered ? ResolveHow.Answer : ResolveHow.Hint;
            }
            else
            {
                notes.Add(named is null ? $"no kit {wantedKey} is stored" : $"{named.Kit.Name}: {named.Problem}");
            }
        }
        else
        {
            SaveResolution resolution;
            try
            {
                resolution = (resolver ?? ResolverFor(store)).Resolve(bytes, savePath, null, null, null, p => KeyOfProfile(p) is { } k && store.Usable(k) is not null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
            {
                resolution = new SaveResolution(null, ResolveHow.Ask, [], [$"the kit match stopped: {ex.Message}"]);
            }
            if (KeyOfProfile(resolution.Profile) is { } key && store.Usable(key) is { } found)
            {
                kit = found;
                how = resolution.How;
            }
            else
            {
                asked = resolution.Alternatives.Select(p => KeyOfProfile(p) is { } k ? store.Usable(k) : null).OfType<StoredKit>().ToList();
            }
        }

        if (kit is null)
        {
            var fitting = store.List().Where(k => k.Usable).ToList();
            if (cfru is not null)
            {
                List<StoredKit> gba = hint.NoKit ? [] : fitting.Where(k => k.Kit.Platform == HackTables.Gba).ToList();
                return new OpenResult(gba.Count > 0 ? OpenKind.Ask : OpenKind.Refused, gba.Count > 0 ? $"{IntakeText.NativeNeedsKit} {IntakeText.Question}" : IntakeText.NativeNeedsKit, null, gba, notes);
            }
            if (asked.Count > 0 && !hint.NoKit)
                return new OpenResult(OpenKind.Ask, IntakeText.Question, null, asked, notes);
            if (!SaveUtil.TryGetSaveFile(bytes, out var plain, savePath))
                return new OpenResult(OpenKind.Refused, IntakeText.NotASave, null, fitting, notes);
            if (!hint.NoKit && PlatformOf(plain) is { } platform)
            {
                var generation = plain.Generation;
                var same = fitting.Where(k => k.Kit.Platform == platform && (k.Kit.Version is not { } v || v.Generation == generation)).ToList();
                if (same.Count > 0)
                {
                    notes.Add($"{same.Count} stored kit(s) fit a Gen {generation} save and none was matched to this one");
                    return new OpenResult(OpenKind.Ask, IntakeText.Question, null, same, notes);
                }
            }
            return PlainStock(plain, bytes, savePath, hint, fitting, notes, how);
        }

        if (kit.Kit.NativeFamily)
        {
            return kit.Shape is { Known: true } shape && shape.Family == kit.Kit.SaveFamily
                ? OpenNative(bytes, store, kit, shape, how, hint, notes)
                : new OpenResult(OpenKind.Refused, IntakeText.NoShapeYet, kit, [], notes);
        }
        if (cfru is not null)
            return new OpenResult(OpenKind.Refused, kit.Kit.SaveFamily == SaveShape.UnknownFamily ? IntakeText.NoShapeYet : $"{IntakeText.NativeNeedsKit} ({kit.Kit.Name} reads as {kit.Kit.SaveFamily}, and this save is a CFRU save)", kit, [], notes);
        return OpenStock(bytes, savePath, kit, hint, notes, how);
    }

    private static OpenResult PlainStock(SaveFile plain, byte[] bytes, string? savePath, OpenHint hint, IReadOnlyList<StoredKit> fitting, List<string> notes, ResolveHow? how)
    {
        if (plain is not SAV3 || RetailQuestion.GameOf(plain) is not { } name)
            return new OpenResult(plain, null, null, null, null, fitting, notes) { How = how };
        var diff = RetailWriteDiff(bytes, savePath);
        if (diff != 0)
            return new OpenResult(plain, null, null, RetailQuestion.NotReproducible(name, diff), diff, fitting, notes) { How = how };
        return new OpenResult(plain, diff, fitting, notes, RetailQuestion.Ask(name, plain is SAV3E), RetailQuestion.ConfirmLabel(name), hint.Unmodified) { How = how };
    }

    public static int RetailWriteDiff(ReadOnlySpan<byte> original, string? savePath = null)
    {
        if (original.IsEmpty)
            return -1;
        try
        {
            if (!SaveUtil.TryGetSaveFile(original.ToArray(), out var copy, savePath) || copy is not SAV3)
                return -1;
            return Differ(original, copy.Write().Span);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException)
        {
            return -1;
        }
    }

    private static OpenResult OpenNative(byte[] bytes, HackKitStore store, StoredKit kit, SaveShape shape, ResolveHow? how, OpenHint hint, List<string> notes)
    {
        var save = TryNative(bytes, shape, out var why, out var trial);
        var rom = save is null ? RomFor(kit, hint) : null;
        if (save is null && rom is null && Gen3ShapeFinder.UnlistedSignature(shape, bytes) is { } wanted)
        {
            try
            {
                var said = store.RefusedSignatures(kit.Kit.Key).Contains(wanted) ? IntakeText.SignatureAbsent(wanted)
                    : store.WantSignature(kit.Kit.Key, wanted) ? IntakeText.SignatureWanted(wanted)
                    : null;
                if (said is not null)
                    why = why is null ? said : $"{why.TrimEnd('.')}. {said}";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                notes.Add($"the signature the save asked for was not noted ({ex.Message})");
            }
        }
        if (rom is not null)
        {
            if (Gen3ShapeFinder.AcceptSignature(shape, rom, bytes, out var signatureWhy) is { } signed && !ReferenceEquals(signed, shape))
            {
                shape = signed;
                try
                {
                    store.UpdateShape(signed);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    notes.Add($"the second signature was not stored ({ex.Message})");
                }
                save = TryNative(bytes, shape, out why, out trial);
            }
            else if (signatureWhy is { } notTaken)
            {
                notes.Add(notTaken);
            }
        }
        if (save is null)
            return new OpenResult(OpenKind.Refused, $"{kit.Kit.Name}: {why ?? "no layout of this hack fits the file"}", kit, [], notes);
        var input = new WriteGateInput
        {
            Shape = shape,
            Trial = trial,
            ProfileClaim = WriteEvidence.Unproven,
            MatchAllowsWrite = how is { } h && WritableMatches.Contains(h),
            FromFile = !hint.Live,
        };
        var verdict = WriteGate.Admit(save, input);
        var model = new HackSaveModel(save, HackNames.From(kit.Data));
        return new OpenResult(save, model, kit, shape, trial, verdict, how, notes);
    }

    private static HackSave? TryNative(byte[] bytes, SaveShape shape, out string? why, out TrialResult trial)
    {
        try
        {
            return HackSaves.TryOpen(bytes, shape, WriteEvidence.Unproven, out why, out trial);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IndexOutOfRangeException or FormatException)
        {
            why = $"the hack save reader stopped: {ex.Message}";
            trial = new TrialResult([], [], why, null, 0);
            return null;
        }
    }

    private static GbaImage? RomFor(StoredKit kit, OpenHint hint)
    {
        ReadOnlyMemory<byte> rom = hint.Rom;
        if (rom.IsEmpty && kit.Kit.Source.RomPath is { } path)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length <= GbaImage.MaxLength)
                    rom = File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        if (rom.Length is < 0x200 or > GbaImage.MaxLength || IntakeHash.Sha1(rom.Span) != kit.Kit.Key)
            return null;
        return new GbaImage(rom);
    }

    private static OpenResult OpenStock(byte[] bytes, string? savePath, StoredKit kit, OpenHint hint, List<string> notes, ResolveHow? how)
    {
        if (!SaveUtil.TryGetSaveFile(bytes.ToArray(), out var sav, savePath))
            return new OpenResult(OpenKind.Refused, IntakeText.NotASave, kit, [], notes);
        var version = kit.Kit.Version ?? (sav.Version.IsValidSavedVersion() ? sav.Version : (GameVersion?)null);
        if (kit.Kit.Version is { } baseGame && baseGame.Generation != sav.Generation)
            return new OpenResult(OpenKind.Refused, $"{kit.Kit.Name} is built on {baseGame} (Gen {baseGame.Generation}), and this save is a Gen {sav.Generation} save", kit, [], notes);
        var data = kit.Data!;
        var past = version is { } v ? PastStock(data, v) : 0;
        var unmapped = data.UnmappedSpecies.Count > 0 || data.UnmappedMoves.Count > 0 || data.UnmappedItems.Count > 0 || past > 0;
        if (unmapped)
            notes.Add($"unmapped: {data.UnmappedSpecies.Count} species, {data.UnmappedMoves.Count} moves, {data.UnmappedItems.Count} items have no PKHeX id; {past} species lie past the stock table");
        var noShape = kit.Kit.Platform == HackTables.Gba && kit.Kit.SaveFamily == SaveShape.UnknownFamily;
        if (noShape)
            notes.Add($"{kit.Kit.Name}'s save family is not known, so this save opens for reading only");
        IDisposable? scope = null;
        if (version is { } overlayVersion)
        {
            try
            {
                var overlay = HackOverlay.From(data, overlayVersion);
                scope = overlay.Use(hint.Language);
                notes.AddRange(overlay.Notes);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                notes.Add($"the hack's own stats and names were not applied: {ex.Message}");
            }
        }
        else
        {
            notes.Add("the hack's base game is not known, so its stats and names are not applied");
        }
        if (scope is not null && sav is SAV3FRLG && SaveUtil.TryGetSaveFile(bytes.ToArray(), out var fresh, savePath))
            sav = fresh;
        var diff = Differ(bytes, sav.Write().Span);
        string? refusal = unmapped ? IntakeText.Unmapped : noShape ? IntakeText.NoShapeYet : diff > 0 ? IntakeText.WriteDiffers(diff) : null;
        return new OpenResult(sav, kit, scope, refusal, diff, [], notes) { How = how };
    }

    public static string? PlatformOf(SaveFile save)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save is SAV7b)
            return HackTables.Nx;
        return save.Generation switch
        {
            1 or 2 => HackTables.Gb,
            3 => HackTables.Gba,
            4 or 5 => HackTables.Nds,
            6 or 7 => HackTables.Ctr,
            >= 8 => HackTables.Nx,
            _ => null,
        };
    }

    public static int Differ(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var n = Math.Abs(a.Length - b.Length);
        var common = Math.Min(a.Length, b.Length);
        for (var i = 0; i < common; i++)
        {
            if (a[i] != b[i])
                n++;
        }
        return n;
    }

    public static int PastStock(HackData data, GameVersion version)
    {
        ArgumentNullException.ThrowIfNull(data);
        var max = GameData.GetPersonal(version).MaxSpeciesID;
        var generation = version.Generation;
        return data.Species.Count(s => s.Id > 0 && HackOverlay.SpeciesIndex(generation, s) is { } index && index > max);
    }
}
