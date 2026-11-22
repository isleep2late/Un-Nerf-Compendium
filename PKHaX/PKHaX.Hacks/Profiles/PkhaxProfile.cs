using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Overlay;
using PKHaX.Hacks.Readers;

namespace PKHaX.Hacks.Profiles;

public enum MatchShape
{
    RomFile,
    CtrMods,
    CtrBaked,
    Switch,
}

public sealed record ProfileMatch(
    MatchShape Shape,
    string Key,
    string Label,
    string? Sha1 = null,
    string? Crc32 = null,
    long? Size = null,
    string? TitleId = null,
    string? CodeSha1 = null,
    string? ModSet = null,
    long? UpdateVersion = null,
    string? ImageRoot = null,
    string? MainBuildId = null);

public sealed class PkhaxProfile
{
    public const string Schema = "pkhax-profile/1";
    public const string FileSuffix = ".pkhaxprofile.json";

    public static readonly IReadOnlyList<string> Kinds = ["vanilla", "unnerf", "hack", "hack+unnerf", "owner-build", "imported"];
    public static readonly IReadOnlyList<string> Platforms = ["gb", "gbc", "gba", "nds", "3ds", "switch"];
    public static readonly IReadOnlyList<string> WriteEvidenceLevels = ["Unproven", "RoundTrip", "Confined", "GameAccepted"];

    private readonly JsonObject root;

    internal PkhaxProfile(JsonObject root, IReadOnlyList<ProfileMatch> match)
    {
        this.root = root;
        Match = match;
    }

    public string Id => Text("id")!;
    public string DisplayName => Text("displayName")!;
    public string Kind => Text("kind")!;
    public string Platform => Text("platform")!;
    public string Base => Text("base")!;
    public string? Parent => Text("parent");
    public IReadOnlyList<ProfileMatch> Match { get; }
    public IEnumerable<string> ImageKeys => Match.Select(m => m.Key);

    public JsonObject? Save => Clone("save");
    public JsonObject? Data => Clone("data");
    public JsonObject? Ram => Clone("ram");
    public JsonObject? UnNerf => Clone("unNerf");
    public JsonObject? Editor => Clone("editor");

    public string? SaveVariant => Text("save", "variant");
    public string? EngineFamily => Text("engine", "family");
    public string WriteEvidence => Text("save", "writeEvidence") ?? "Unproven";
    public string? DataRomSha1 => Text("data", "romSha1");
    public bool HasEmbeddedData => root["data"] is JsonObject d && d["embedded"] is JsonObject;
    public bool IsHack => Kind is "hack" or "hack+unnerf" or "imported";
    public int FrontierShift => (int)(PkhaxProfileReader.Integer(root["unNerf"]?["frontierShift"]) ?? 0);
    public bool IsVanillaContainer => SaveVariant is null || HackSaveContext.IsVanilla(SaveVariant);

    public HackSaveContext ToSaveContext() => new()
    {
        ProfileId = Id,
        Kind = ImageKinds.Parse(Kind),
        SaveVariant = SaveVariant ?? HackSaveContext.VanillaContainer,
        FrontierShift = FrontierShift,
    };

    public bool Matches(string imageKey) => Match.Any(m => string.Equals(m.Key, imageKey, StringComparison.OrdinalIgnoreCase));

    public JsonObject ToJsonObject() => (JsonObject)root.DeepClone();

    public string ToJson() => root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    public IReadOnlyList<string> UnknownKeys() => root.Select(kv => kv.Key).Where(k => !PkhaxProfileReader.KnownKeys.Contains(k)).ToList();

    private string? Text(string key, string? inner = null)
    {
        var node = root[key];
        if (inner is not null)
            node = node is JsonObject o ? o[inner] : null;
        return node is JsonValue v && v.TryGetValue(out string? s) ? s : null;
    }

    private JsonObject? Clone(string key) => root[key] is JsonObject o ? (JsonObject)o.DeepClone() : null;
}
