using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Profiles;

namespace PKHaX.Hacks.Intake;

public sealed record StoredKit(HackKit Kit, HackData? Data, SaveShape? Shape, PkhaxProfile? Profile)
{
    public bool Usable => !Kit.Stale && Data is not null;

    public string? Problem => Kit.Stale ? IntakeText.StaleKit
        : Data is null ? $"its tables ({HackDataCache.FileName}) are missing or were made by an older reader: drop the ROM again"
        : null;
}

public sealed record KitBase(string Path, long Size, uint Crc32, string Sha1);

public sealed class HackKitStore
{
    public const string BasesFile = "bases.json";
    public const string BasesSchema = "pkhax-bases/1";
    public const string ProfileFile = "profile" + PkhaxProfile.FileSuffix;
    public const int MaxBases = 200;
    public const string WantedFile = "wanted-signatures.json";
    public const string WantedSchema = "pkhax-wanted-signatures/1";
    public const int MaxWanted = 4;
    public const int MaxWantedBytes = 4096;

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public HackKitStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = root;
    }

    public string Root { get; }

    public static string DefaultRoot() => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create);

    public string HacksDir => Path.Combine(Root, "pkhax", "hacks");

    public string BasesPath => Path.Combine(Root, "pkhax", BasesFile);

    public string PairingsPath => PairingMemory.PathFor(Root);

    public string DirFor(string key) => Path.Combine(HacksDir, Checked(key));

    public static bool IsKey(string? key) => key is { Length: 40 } && key.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static string Checked(string key) => IsKey(key) ? key : throw new ArgumentException($"'{key}' is not a lower-case SHA-1", nameof(key));

    public IReadOnlyList<StoredKit> List()
    {
        var result = new List<StoredKit>();
        if (!Directory.Exists(HacksDir))
            return result;
        foreach (var dir in Directory.EnumerateDirectories(HacksDir).Order(StringComparer.Ordinal))
        {
            var key = Path.GetFileName(dir);
            if (IsKey(key) && Load(key) is { } kit)
                result.Add(kit);
        }
        return result;
    }

    public StoredKit? Load(string key)
    {
        if (!IsKey(key))
            return null;
        var path = Path.Combine(DirFor(key), HackKit.FileName);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > ProfileStore.MaxProfileBytes)
                return null;
            if (HackKit.FromJson(File.ReadAllText(path), key) is not { } kit)
                return null;
            var data = HackDataCache.Load(Root, key);
            var shape = SaveShapeCache.Load(Root, key);
            PkhaxProfile? profile = null;
            var profilePath = Path.Combine(DirFor(key), ProfileFile);
            if (File.Exists(profilePath) && new FileInfo(profilePath).Length <= ProfileStore.MaxProfileBytes)
                profile = PkhaxProfileReader.Load(File.ReadAllText(profilePath)).Profile;
            return new StoredKit(kit, data, shape, profile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public StoredKit? Usable(string key) => Load(key) is { Usable: true } kit ? kit : null;

    public StoredKit Save(HackKit kit, HackData data, SaveShape? shape)
    {
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(data);
        if (!string.Equals(kit.Key, data.RomSha1, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"the kit {kit.Key} and its tables {data.RomSha1} name different images", nameof(data));
        var dir = DirFor(kit.Key);
        Directory.CreateDirectory(dir);
        HackDataCache.Save(Root, data);
        if (shape is not null && string.Equals(shape.RomSha1, kit.Key, StringComparison.OrdinalIgnoreCase))
            SaveShapeCache.Save(Root, shape);
        var profile = ProfileFor(kit);
        if (profile is not null)
            Write(Path.Combine(dir, ProfileFile), profile.ToJson());
        Write(Path.Combine(dir, HackKit.FileName), kit.ToJson());
        return new StoredKit(kit, data, shape, profile);
    }

    public void UpdateShape(SaveShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (IsKey(shape.RomSha1) && Directory.Exists(DirFor(shape.RomSha1)))
            SaveShapeCache.Save(Root, shape);
    }

    public string WantedPath(string key) => Path.Combine(DirFor(key), WantedFile);

    public IReadOnlyList<uint> WantedSignatures(string key) => ReadWanted(key).Wanted;

    public IReadOnlyList<uint> RefusedSignatures(string key) => ReadWanted(key).Refused;

    public bool WantSignature(string key, uint signature)
    {
        if (!IsKey(key) || signature is 0 or uint.MaxValue || !Directory.Exists(DirFor(key)))
            return false;
        var (wanted, refused) = ReadWanted(key);
        if (refused.Contains(signature))
            return false;
        WriteWanted(key, wanted.Where(s => s != signature).Append(signature).TakeLast(MaxWanted).ToList(), refused);
        return true;
    }

    public bool ForgetWanted(string key, IEnumerable<uint>? notInRom = null)
    {
        if (!IsKey(key))
            return false;
        var path = WantedPath(key);
        if (!File.Exists(path))
            return false;
        var added = notInRom is null ? new List<uint>() : notInRom.Where(s => s is not 0 and not uint.MaxValue).Distinct().ToList();
        var kept = ReadWanted(key).Refused.Where(s => !added.Contains(s)).Concat(added).TakeLast(MaxWanted).ToList();
        if (kept.Count == 0)
            File.Delete(path);
        else
            WriteWanted(key, [], kept);
        return true;
    }

    private (IReadOnlyList<uint> Wanted, IReadOnlyList<uint> Refused) ReadWanted(string key)
    {
        if (!IsKey(key))
            return ([], []);
        var path = WantedPath(key);
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxWantedBytes)
                return ([], []);
            if (JsonNode.Parse(File.ReadAllText(path)) is not JsonObject o || (string?)o["schema"] != WantedSchema || o["signatures"] is not JsonArray rows)
                return ([], []);
            return (Values(rows), o["refused"] is JsonArray refused ? Values(refused) : []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return ([], []);
        }
    }

    private static List<uint> Values(JsonArray rows)
    {
        var list = new List<uint>();
        foreach (var row in rows)
        {
            if (row is not null && (long?)row is { } value && value is > 0 and < uint.MaxValue && !list.Contains((uint)value))
                list.Add((uint)value);
        }
        return list.TakeLast(MaxWanted).ToList();
    }

    private void WriteWanted(string key, IReadOnlyList<uint> wanted, IReadOnlyList<uint> refused)
    {
        var o = new JsonObject
        {
            ["schema"] = WantedSchema,
            ["signatures"] = new JsonArray(wanted.Select(s => (JsonNode)(long)s).ToArray()),
            ["refused"] = new JsonArray(refused.Select(s => (JsonNode)(long)s).ToArray()),
        };
        Write(WantedPath(key), o.ToJsonString(Indented));
    }

    private static void Write(string path, string text)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }

    public static string? ProfilePlatform(HackKit kit) => kit.Platform switch
    {
        HackTables.Gba => "gba",
        HackTables.Gb => kit.Engine == HackTables.Gb2Engine ? "gbc" : "gb",
        HackTables.Nds => "nds",
        _ => null,
    };

    public static string SaveVariant(HackKit kit) => kit.SaveFamily switch
    {
        SaveShape.Cfru => Saves.Cfru.CfruSave.ShapeVariantId,
        SaveShape.Expansion => Saves.Expansion.ExpansionSave.VariantId,
        _ => Readers.HackSaveContext.VanillaContainer,
    };

    public static PkhaxProfile? ProfileFor(HackKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        if (ProfilePlatform(kit) is not { } platform)
            return null;
        var name = kit.Name.Trim();
        if (name.Length == 0)
            name = kit.Key[..8];
        if (name.Length > 120)
            name = name[..120];
        var o = new JsonObject
        {
            ["schema"] = PkhaxProfile.Schema,
            ["id"] = kit.ProfileId,
            ["displayName"] = name,
            ["kind"] = "imported",
            ["platform"] = platform,
            ["base"] = kit.ProfileId,
            ["match"] = new JsonArray(new JsonObject { ["sha1"] = kit.Key, ["label"] = "rom" }),
            ["save"] = new JsonObject { ["variant"] = SaveVariant(kit), ["writeEvidence"] = "Unproven" },
            ["data"] = new JsonObject { ["source"] = "rom", ["romSha1"] = kit.Key },
        };
        return PkhaxProfileReader.Load(o).Profile;
    }

    public string? ExportProfile(string key)
    {
        if (Usable(key) is not { Data: { } data } kit || ProfileFor(kit.Kit) is not { } profile)
            return null;
        if (JsonNode.Parse(profile.ToJson()) is not JsonObject o || JsonNode.Parse(HackDataCache.ToJson(data)) is not JsonObject tables)
            return null;
        o["data"] = new JsonObject { ["source"] = "embedded", ["romSha1"] = kit.Kit.Key, ["embedded"] = tables };
        if (PkhaxProfileReader.Load(o).Profile is not { } exported || ProfileBundleReader.Embedded(exported, kit.Kit.Key) is null)
            return null;
        var text = exported.ToJson();
        return System.Text.Encoding.UTF8.GetByteCount(text) <= ProfileStore.MaxProfileBytes ? text : null;
    }

    public IEnumerable<ProfileDocument> ProfileDocuments()
    {
        foreach (var kit in List())
        {
            if (kit.Usable && kit.Profile is { } profile)
                yield return new ProfileDocument($"kit:{kit.Kit.Key}", profile.ToJson());
        }
    }

    public bool Forget(string key, PairingMemory? pairings = null)
    {
        if (!IsKey(key))
            return false;
        var dir = DirFor(key);
        var profileId = HackKit.ProfilePrefix + key;
        var memory = pairings ?? new PairingMemory(PairingsPath);
        foreach (var row in memory.Entries.Where(e => e.ProfileId == profileId).ToList())
            memory.Forget(row.SavePath);
        if (!Directory.Exists(dir))
            return false;
        Directory.Delete(dir, recursive: true);
        return true;
    }

    public IReadOnlyList<KitBase> Bases()
    {
        try
        {
            if (!File.Exists(BasesPath) || new FileInfo(BasesPath).Length > ProfileStore.MaxProfileBytes)
                return [];
            if (JsonNode.Parse(File.ReadAllText(BasesPath)) is not JsonObject o || (string?)o["schema"] != BasesSchema || o["bases"] is not JsonArray rows)
                return [];
            var list = new List<KitBase>();
            foreach (var row in rows.OfType<JsonObject>())
            {
                if ((string?)row["path"] is { } path && (long?)row["size"] is { } size && (long?)row["crc32"] is { } crc && (string?)row["sha1"] is { } sha1 && IsKey(sha1))
                    list.Add(new KitBase(path, size, (uint)crc, sha1));
            }
            return list;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException)
        {
            return [];
        }
    }

    public KitBase? FindBase(uint crc32, long? size) => Bases().LastOrDefault(b => b.Crc32 == crc32 && (size is null || b.Size == size) && File.Exists(b.Path));

    public void RememberBase(KitBase b)
    {
        ArgumentNullException.ThrowIfNull(b);
        var rows = Bases().Where(x => x.Path != b.Path && x.Sha1 != b.Sha1).Append(b).TakeLast(MaxBases).ToList();
        var o = new JsonObject
        {
            ["schema"] = BasesSchema,
            ["bases"] = new JsonArray(rows.Select(r => (JsonNode)new JsonObject { ["path"] = r.Path, ["size"] = r.Size, ["crc32"] = (long)r.Crc32, ["sha1"] = r.Sha1 }).ToArray()),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(BasesPath)!);
        Write(BasesPath, o.ToJsonString(Indented));
    }
}
