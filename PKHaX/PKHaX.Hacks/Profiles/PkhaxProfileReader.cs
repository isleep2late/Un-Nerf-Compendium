using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PKHaX.Hacks.Profiles;

public sealed record ProfileLoadResult(PkhaxProfile? Profile, string? Refusal)
{
    public bool Ok => Profile is not null;

    public static ProfileLoadResult Refused(string why) => new(null, why);
}

public static partial class PkhaxProfileReader
{
    public const int GameProfileSchema = 1;

    public static readonly IReadOnlySet<string> KnownKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "schema", "id", "displayName", "kind", "platform", "base", "parent", "match", "engine",
        "save", "data", "ram", "unNerf", "editor", "codes", "evidence", "sources",
    };

    private static readonly IReadOnlyList<string> AnyAbilityKinds = ["none", "pk3-u16-0x1E", "bits"];
    private static readonly IReadOnlyList<string> DataSources = ["rom", "embedded", "pkhex"];

    [GeneratedRegex("^[a-z0-9][a-z0-9.+-]{0,95}\\z")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[a-z0-9][A-Za-z0-9.:+/_-]*\\z")]
    private static partial Regex VariantPattern();

    [GeneratedRegex("^([0-9A-Fa-f]{32}|[0-9A-Fa-f]{40}(00)*)\\z")]
    private static partial Regex BuildIdPattern();

    [GeneratedRegex("^(none|[0-9A-Fa-f]{64})\\z")]
    private static partial Regex ModSetPattern();

    public static ProfileLoadResult Load(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 64 });
        }
        catch (JsonException ex)
        {
            return ProfileLoadResult.Refused($"the file is not JSON ({ex.Message}).");
        }
        try
        {
            return node is JsonObject o ? Load(o) : ProfileLoadResult.Refused("the file is not a JSON object, so it is not a pkhax-profile/1 profile.");
        }
        catch (ArgumentException ex)
        {
            return ProfileLoadResult.Refused($"the file repeats a key in one object ({ex.Message}).");
        }
    }

    public static ProfileLoadResult Load(JsonObject source)
    {
        var root = (JsonObject)source.DeepClone();
        if (Text(root, "schema") != PkhaxProfile.Schema)
            return ProfileLoadResult.Refused(root["schema"] is JsonValue sv && sv.TryGetValue(out int n) && n == GameProfileSchema
                ? "this is an isleep2late GameProfile (schema 1); export it as pkhax-profile/1 first (FromGameProfile)."
                : $"\"schema\" is not \"{PkhaxProfile.Schema}\".");
        if (Text(root, "id") is not { } id || !IdPattern().IsMatch(id))
            return ProfileLoadResult.Refused("\"id\" must be a lower-case profile id (a-z, 0-9, '.', '+', '-'; at most 96 characters).");
        if (Text(root, "displayName") is not { Length: > 0 } name || name.EnumerateRunes().Count() > 120)
            return ProfileLoadResult.Refused($"{id}: \"displayName\" must be a name of 1-120 characters.");
        var kind = Text(root, "kind");
        if (kind is null || !PkhaxProfile.Kinds.Contains(kind))
            return ProfileLoadResult.Refused($"{id}: \"kind\" is '{kind}'; it must be one of {string.Join(", ", PkhaxProfile.Kinds)} (DESIGN-1 ids, so a hack + Un-Nerf build is \"hack+unnerf\").");
        var platform = Text(root, "platform");
        if (platform is null || !PkhaxProfile.Platforms.Contains(platform))
            return ProfileLoadResult.Refused($"{id}: \"platform\" is '{platform}'; it must be one of {string.Join(", ", PkhaxProfile.Platforms)}.");
        if (Text(root, "base") is not { } b || !IdPattern().IsMatch(b))
            return ProfileLoadResult.Refused($"{id}: \"base\" must be a profile id.");
        if (root.ContainsKey("parent") && root["parent"] is not null && (Text(root, "parent") is not { } p || !IdPattern().IsMatch(p)))
            return ProfileLoadResult.Refused($"{id}: \"parent\" must be a profile id or null.");
        if (root.ContainsKey("unnerf"))
            return ProfileLoadResult.Refused($"{id}: the Un-Nerf block is \"unNerf\" (DESIGN-1), not \"unnerf\".");
        if (ReadMatch(root, platform, out var match) is { } matchError)
            return ProfileLoadResult.Refused($"{id}: {matchError}");
        if ((CheckSave(root) ?? CheckData(root) ?? CheckUnNerf(root) ?? CheckBlocks(root)) is { } blockError)
            return ProfileLoadResult.Refused($"{id}: {blockError}");
        return new ProfileLoadResult(new PkhaxProfile(root, match), null);
    }

    public static JsonObject FromGameProfile(JsonObject gameProfile)
    {
        var o = (JsonObject)gameProfile.DeepClone();
        o["schema"] = PkhaxProfile.Schema;
        return o;
    }

    public static JsonObject ToGameProfile(PkhaxProfile profile)
    {
        var o = profile.ToJsonObject();
        o["schema"] = GameProfileSchema;
        return o;
    }

    private static string? ReadMatch(JsonObject root, string platform, out IReadOnlyList<ProfileMatch> match)
    {
        match = [];
        if (root["match"] is not JsonArray rows || rows.Count == 0)
            return "\"match\" must hold at least one exact image identity; a profile without one is refused (DESIGN-5 6.4).";
        var list = new List<ProfileMatch>();
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i] is not JsonObject row)
                return $"match[{i}] is not an object.";
            if (Text(row, "label") is not { } label || !IdPattern().IsMatch(label))
                return $"match[{i}] needs a \"label\" (a lower-case id).";
            string? error;
            var pm = platform switch
            {
                "gb" or "gbc" or "gba" or "nds" => RomRow(row, label, i, out error),
                "3ds" => CtrRow(row, label, i, out error),
                _ => NxRow(row, label, i, out error),
            };
            if (pm is null)
                return error;
            if (list.Any(x => x.Key == pm.Key))
                return $"match[{i}] repeats the image of an earlier row.";
            list.Add(pm);
        }
        match = list;
        return null;
    }

    private static ProfileMatch? RomRow(JsonObject row, string label, int i, out string? error)
    {
        error = null;
        var sha1 = Text(row, "sha1");
        if (!ProfileImageKey.IsLowerHex(sha1, 40))
        {
            error = $"match[{i}] needs the image's \"sha1\" (40 lower-case hex digits) on this platform.";
            return null;
        }
        if (row.ContainsKey("titleId") || row.ContainsKey("codeSha1") || row.ContainsKey("mainBuildId") || row.ContainsKey("imageRoot"))
        {
            error = $"match[{i}] mixes 3DS or Switch fields into a cartridge image row.";
            return null;
        }
        var crc = Text(row, "crc32");
        if (row.ContainsKey("crc32") && !ProfileImageKey.IsLowerHex(crc, 8))
        {
            error = $"match[{i}] \"crc32\" must be 8 lower-case hex digits.";
            return null;
        }
        var size = Integer(row["size"]);
        if (row.ContainsKey("size") && size is not > 0)
        {
            error = $"match[{i}] \"size\" must be a positive integer.";
            return null;
        }
        return new ProfileMatch(MatchShape.RomFile, sha1!, label, Sha1: sha1, Crc32: crc, Size: size);
    }

    private static ProfileMatch? CtrRow(JsonObject row, string label, int i, out string? error)
    {
        error = null;
        if (row.ContainsKey("imageRoot"))
        {
            if (ProfileImageKey.CtrBaked(Text(row, "imageRoot")) is not { } baked || !ProfileImageKey.IsLowerHex(Text(row, "imageRoot"), 64))
            {
                error = $"match[{i}] \"imageRoot\" must be 64 lower-case hex digits.";
                return null;
            }
            return new ProfileMatch(MatchShape.CtrBaked, baked, label, Sha1: Text(row, "sha1"), ImageRoot: baked);
        }
        var tid = Text(row, "titleId");
        var code = Text(row, "codeSha1");
        var mods = Text(row, "modSet");
        if (mods is not null && !ModSetPattern().IsMatch(mods))
        {
            error = $"match[{i}] \"modSet\" must be a mod set hash (\"none\" or 64 hex digits).";
            return null;
        }
        if (!ProfileImageKey.TryParseTitleId(tid, out var titleId) || tid!.Any(char.IsLower) || !ProfileImageKey.IsLowerHex(code, 40)
            || mods is null || Version(row) is not { } update)
        {
            error = $"match[{i}] on 3ds needs {{titleId (16 upper-case hex), codeSha1, modSet, updateVersion}} or a baked {{imageRoot}} (DESIGN-1 R1-4; codeCrc32 is not an identity).";
            return null;
        }
        try
        {
            return new ProfileMatch(MatchShape.CtrMods, ProfileImageKey.ForCtrMods(titleId, code!, mods, update), label,
                TitleId: tid, CodeSha1: code, ModSet: ProfileImageKey.CanonicalModSet(mods), UpdateVersion: update);
        }
        catch (FormatException ex)
        {
            error = $"match[{i}]: {ex.Message}";
            return null;
        }
    }

    private static ProfileMatch? NxRow(JsonObject row, string label, int i, out string? error)
    {
        error = null;
        var tid = Text(row, "titleId");
        var build = Text(row, "mainBuildId");
        var mods = Text(row, "modSet");
        if (build is not null && !BuildIdPattern().IsMatch(build))
        {
            error = $"match[{i}] \"mainBuildId\" must be 32 or 40 hex digits, optionally padded with zeros.";
            return null;
        }
        if (mods is not null && !ModSetPattern().IsMatch(mods))
        {
            error = $"match[{i}] \"modSet\" must be a mod set hash (\"none\" or 64 hex digits).";
            return null;
        }
        if (!ProfileImageKey.TryParseTitleId(tid, out var titleId) || tid!.Any(char.IsLower) || build is null || mods is null || Version(row) is not { } update)
        {
            error = $"match[{i}] on switch needs {{titleId (16 upper-case hex), mainBuildId, updateVersion, modSet}}.";
            return null;
        }
        try
        {
            return new ProfileMatch(MatchShape.Switch, ProfileImageKey.ForSwitch(titleId, build, update, mods), label,
                TitleId: tid, MainBuildId: ProfileImageKey.CanonicalBuildId(build), ModSet: ProfileImageKey.CanonicalModSet(mods), UpdateVersion: update);
        }
        catch (FormatException ex)
        {
            error = $"match[{i}]: {ex.Message}";
            return null;
        }
    }

    private static uint? Version(JsonObject row) => Integer(row["updateVersion"]) is { } n && n is >= 0 and <= uint.MaxValue ? (uint)n : null;

    internal static long? Integer(JsonNode? node) =>
        node is JsonValue v && v.GetValueKind() == JsonValueKind.Number
        && decimal.TryParse(v.ToJsonString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)
        && d == decimal.Truncate(d) && d >= long.MinValue && d <= long.MaxValue ? (long)d : null;

    private static string? CheckSave(JsonObject root)
    {
        if (!root.ContainsKey("save"))
            return null;
        if (root["save"] is not JsonObject save)
            return "\"save\" must be an object.";
        if (Text(save, "variant") is not { } v || !VariantPattern().IsMatch(v))
            return "\"save.variant\" must name the save variant (e.g. \"vanilla:SAV3E\", \"gen3-expansion/1\").";
        if (save.ContainsKey("writeEvidence") && !PkhaxProfile.WriteEvidenceLevels.Contains(Text(save, "writeEvidence") ?? ""))
            return $"\"save.writeEvidence\" must be one of {string.Join(", ", PkhaxProfile.WriteEvidenceLevels)}.";
        foreach (var key in new[] { "container", "party", "boxes", "trainer", "money", "bag" })
            if (save.ContainsKey(key) && save[key] is not (null or JsonObject))
                return $"\"save.{key}\" must be an object or null.";
        if (save.ContainsKey("preserve") && (save["preserve"] is not JsonArray pa || pa.Any(x => x is not JsonValue xv || !xv.TryGetValue(out string? _))))
            return "\"save.preserve\" must be a list of names.";
        return null;
    }

    private static string? CheckData(JsonObject root)
    {
        if (!root.ContainsKey("data"))
            return null;
        if (root["data"] is not JsonObject data)
            return "\"data\" must be an object.";
        if (data.ContainsKey("reader") && Text(data, "reader") is null)
            return "\"data.reader\" must be the reader's name.";
        if (data.ContainsKey("readerVersion") && Integer(data["readerVersion"]) is not >= 1)
            return "\"data.readerVersion\" must be an integer of 1 or more.";
        if (data.ContainsKey("source") && !DataSources.Contains(Text(data, "source") ?? ""))
            return $"\"data.source\" must be one of {string.Join(", ", DataSources)}.";
        if (data["romSha1"] is not null && !ProfileImageKey.IsLowerHex(Text(data, "romSha1"), 40))
            return "\"data.romSha1\" must be 40 lower-case hex digits or null.";
        if (data["embedded"] is JsonObject e && Text(e, "schema") != Analysis.HackData.Schema)
            return $"\"data.embedded\" must be a {Analysis.HackData.Schema} table set (DESIGN-5 3.3).";
        if (data.ContainsKey("embedded") && data["embedded"] is not (null or JsonObject))
            return "\"data.embedded\" must be an object or null.";
        return null;
    }

    private static string? CheckUnNerf(JsonObject root)
    {
        if (!root.ContainsKey("unNerf"))
            return null;
        if (root["unNerf"] is not JsonObject u)
            return "\"unNerf\" must be an object.";
        foreach (var key in new[] { "anyAbility", "deoxysForm" })
        {
            if (!u.ContainsKey(key))
                continue;
            if (u[key] is not JsonObject a || !AnyAbilityKinds.Contains(Text(a, "kind") ?? ""))
                return $"\"unNerf.{key}.kind\" must be one of {string.Join(", ", AnyAbilityKinds)}.";
            if (Text(a, "kind") == "bits" && (a["fields"] is not JsonArray f || f.Count == 0))
                return $"\"unNerf.{key}\" of kind bits needs its \"fields\".";
        }
        if (u.ContainsKey("frontierShift") && Integer(u["frontierShift"]) is not (0 or 4))
            return "\"unNerf.frontierShift\" must be 0 or 4 (R5-6: 4 only for vanilla Emerald Un-Nerf Full).";
        return null;
    }

    private static string? CheckBlocks(JsonObject root)
    {
        foreach (var key in new[] { "engine", "ram", "editor", "evidence" })
            if (root.ContainsKey(key) && root[key] is not JsonObject)
                return $"\"{key}\" must be an object.";
        foreach (var key in new[] { "codes", "sources" })
            if (root.ContainsKey(key) && root[key] is not JsonArray)
                return $"\"{key}\" must be a list.";
        return null;
    }

    private static string? Text(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
}
