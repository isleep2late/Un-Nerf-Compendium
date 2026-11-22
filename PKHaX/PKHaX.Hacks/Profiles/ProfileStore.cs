using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;

namespace PKHaX.Hacks.Profiles;

public sealed record ProfileDocument(string Source, string? Json, string? Error = null);

public sealed class ProfileStore
{
    public const string BuiltInSource = "built-in";
    public const long MaxProfileBytes = 16 * 1024 * 1024;

    private const string BuiltInPrefix = "PKHaX.Hacks.Profiles.BuiltIn.";

    private readonly Dictionary<string, PkhaxProfile> byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> sourceOf = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PkhaxProfile>> byImage = new(StringComparer.Ordinal);
    private readonly List<string> problems = [];
    private readonly List<string> notes = [];

    public IReadOnlyList<PkhaxProfile> All => byId.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    public IReadOnlyList<string> Problems => problems;
    public IReadOnlyList<string> Notes => notes;

    public static ProfileStore Create(IEnumerable<ProfileDocument> documents)
    {
        var store = new ProfileStore();
        foreach (var d in documents)
            store.Add(d);
        store.Index();
        return store;
    }

    public static ProfileStore Load(string? userFolder) => Create(BuiltIns().Concat(userFolder is null ? [] : Folder(userFolder)));

    public static IEnumerable<ProfileDocument> BuiltIns()
    {
        var asm = typeof(ProfileStore).Assembly;
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith(BuiltInPrefix, StringComparison.Ordinal) && n.EndsWith(PkhaxProfile.FileSuffix, StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal))
        {
            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null)
                continue;
            using var reader = new StreamReader(stream);
            yield return new ProfileDocument($"{BuiltInSource}:{name[BuiltInPrefix.Length..]}", reader.ReadToEnd());
        }
    }

    public static IEnumerable<ProfileDocument> Folder(string folder)
    {
        if (!Directory.Exists(folder))
            yield break;
        foreach (var file in Directory.EnumerateFiles(folder, "*" + PkhaxProfile.FileSuffix, SearchOption.TopDirectoryOnly).OrderBy(f => f, StringComparer.Ordinal))
        {
            string? text = null;
            string? error = null;
            try
            {
                text = new FileInfo(file).Length > MaxProfileBytes ? null : File.ReadAllText(file);
                if (text is null)
                    error = $"is larger than {MaxProfileBytes} bytes";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = ex.Message;
            }
            yield return new ProfileDocument(file, text, error);
        }
    }

    public PkhaxProfile? ById(string? id) => id is not null && byId.TryGetValue(id, out var p) ? p : null;

    public string? SourceOf(string id) => sourceOf.TryGetValue(id, out var s) ? s : null;

    public IReadOnlyList<PkhaxProfile> ForImage(string? imageKey) =>
        imageKey is not null && byImage.TryGetValue(imageKey.ToLowerInvariant(), out var list) ? list : [];

    public IReadOnlyList<PkhaxProfile> WithSaveSignature(uint signature) =>
        All.Where(p => SaveSignatures(p).Contains(signature)).ToList();

    public static IReadOnlyList<uint> SaveSignatures(PkhaxProfile profile)
    {
        var found = new List<uint>();
        foreach (var s in DeclaredSaveSignatures(profile))
            if (ParseSignature(s) is { } sig && !found.Contains(sig))
                found.Add(sig);
        return found;
    }

    public static IReadOnlyList<string> DeclaredSaveSignatures(PkhaxProfile profile)
    {
        if (profile.Save?["container"] is not JsonObject c)
            return [];
        var declared = new List<string>();
        if (c["signatures"] is JsonArray list)
            declared.AddRange(list.Select(n => n is JsonValue v && v.TryGetValue(out string? s) ? s : n?.ToJsonString() ?? "null"));
        if (c["signature"] is JsonNode single)
            declared.Add(single is JsonValue v && v.TryGetValue(out string? s) ? s : single.ToJsonString());
        return declared;
    }

    public static uint? ParseSignature(string text) =>
        text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
        && uint.TryParse(text.AsSpan(2), System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var sig)
            ? sig
            : null;

    private void Add(ProfileDocument d)
    {
        if (d.Json is null)
        {
            problems.Add($"{d.Source}: not read ({d.Error ?? "no text"}).");
            return;
        }
        var r = PkhaxProfileReader.Load(d.Json);
        if (r.Profile is not { } p)
        {
            problems.Add($"{d.Source}: refused: {r.Refusal}");
            return;
        }
        if (sourceOf.TryGetValue(p.Id, out var earlier))
            notes.Add($"{d.Source}: profile {p.Id} replaces the one from {earlier}.");
        byId[p.Id] = p;
        sourceOf[p.Id] = d.Source;
    }

    private void Index()
    {
        foreach (var p in All)
            foreach (var key in p.ImageKeys.Distinct(StringComparer.Ordinal))
            {
                if (!byImage.TryGetValue(key, out var list))
                    byImage[key] = list = [];
                list.Add(p);
            }
    }
}
