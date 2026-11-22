using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PKHaX.Hacks.Analysis;

public static class HackDataCache
{
    public const string FileName = "data.json";

    public static string PathFor(string root, string imageKey) => Path.Combine(root, "pkhax", "hacks", imageKey, FileName);

    public static string ToJson(HackData d)
    {
        var o = new JsonObject
        {
            ["schema"] = HackData.Schema,
            ["romSha1"] = d.RomSha1,
            ["reader"] = d.Reader,
            ["readerVersion"] = d.ReaderVersion,
            ["rhhVersion"] = d.RhhVersion,
            ["platform"] = d.Platform,
            ["gameCode"] = d.GameCode,
            ["engine"] = d.Engine,
            ["tableKey"] = d.TableKey,
            ["counts"] = new JsonObject
            {
                ["species"] = d.Counts.Species, ["moves"] = d.Counts.Moves, ["abilities"] = d.Counts.Abilities, ["items"] = d.Counts.Items,
                ["forms"] = d.Counts.Forms, ["levelUp"] = d.Counts.LevelUp,
            },
            ["tables"] = new JsonArray(d.Tables.Select(t => (JsonNode)new JsonObject
            {
                ["table"] = t.Table, ["status"] = t.Status, ["reason"] = t.Reason, ["aligned"] = t.Aligned,
            }).ToArray()),
            ["species"] = new JsonArray(d.Species.Select(SpeciesNode).ToArray()),
            ["forms"] = new JsonArray(d.Forms.Select(SpeciesNode).ToArray()),
            ["moves"] = new JsonArray(d.Moves.Select(m => (JsonNode)new JsonObject
            {
                ["id"] = m.Id, ["name"] = m.Name, ["national"] = m.National, ["power"] = m.Power, ["type"] = m.Type, ["accuracy"] = m.Accuracy, ["pp"] = m.Pp,
                ["category"] = m.Category, ["categoryByType"] = m.CategoryByType,
            }).ToArray()),
            ["items"] = new JsonArray(d.Items.Select(i => (JsonNode)new JsonObject
            {
                ["id"] = i.Id, ["name"] = i.Name, ["national"] = i.National, ["pocket"] = i.Pocket, ["price"] = i.Price,
            }).ToArray()),
            ["abilities"] = new JsonArray(d.Abilities.Select(a => (JsonNode)new JsonObject { ["id"] = a.Id, ["name"] = a.Name, ["national"] = a.National }).ToArray()),
            ["learnsets"] = new JsonObject
            {
                ["levelUp"] = new JsonObject(d.LevelUp.OrderBy(kv => kv.Key).Select(kv => new KeyValuePair<string, JsonNode?>(
                    kv.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    new JsonArray(kv.Value.Select(l => (JsonNode)new JsonArray(l.Level, l.Move)).ToArray())))),
            },
            ["unmapped"] = new JsonObject
            {
                ["species"] = Ints(d.UnmappedSpecies), ["moves"] = Ints(d.UnmappedMoves), ["items"] = Ints(d.UnmappedItems), ["abilities"] = Ints(d.UnmappedAbilities),
            },
            ["typeNames"] = new JsonArray(d.TypeNames.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
            ["notes"] = new JsonArray(d.Notes.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray()),
        };
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonNode SpeciesNode(HackSpecies s) => new JsonObject
    {
        ["id"] = s.Id, ["name"] = s.Name, ["national"] = s.National, ["form"] = s.Form,
        ["types"] = Ints(s.Types), ["base"] = Ints(s.BaseStats), ["abilities"] = Ints(s.Abilities),
        ["growth"] = s.Growth, ["gender"] = s.GenderRatio, ["catch"] = s.CatchRate, ["exp"] = s.ExpYield,
        ["genderRead"] = s.GenderRead, ["dex"] = s.Dex, ["baseSpecies"] = s.BaseSpecies, ["formName"] = s.FormName,
    };

    private static JsonArray Ints(IEnumerable<int> values) => new(values.Select(v => (JsonNode)JsonValue.Create(v)).ToArray());

    public static HackData? FromJson(string json, string romSha1) => FromJson(json, romSha1, HackData.MinReaderVersion);

    public static HackData? FromJson(string json, string romSha1, int minReaderVersion)
    {
        try
        {
            return Parse(json, romSha1, minReaderVersion);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NullReferenceException
            or OverflowException or ArgumentException or IndexOutOfRangeException or InvalidCastException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static HackData? Parse(string json, string romSha1, int minReaderVersion)
    {
        var root = JsonNode.Parse(json);
        if (root is not JsonObject o || (string?)o["schema"] != HackData.Schema || (int?)o["readerVersion"] is not { } version
            || version < minReaderVersion || version > HackData.CurrentReaderVersion
            || !string.Equals((string?)o["romSha1"], romSha1, StringComparison.OrdinalIgnoreCase) || (string?)o["reader"] is not { } reader)
            return null;
        return new HackData
        {
            RomSha1 = romSha1.ToLowerInvariant(),
            Reader = reader,
            ReaderVersion = version,
            RhhVersion = (string?)o["rhhVersion"],
            Platform = (string?)o["platform"],
            GameCode = (string?)o["gameCode"],
            Engine = (string?)o["engine"],
            TableKey = (string?)o["tableKey"],
            Tables = Array(o["tables"]).Select(StatusRow).OfType<HackTableStatus>().ToList(),
            TypeNames = Array(o["typeNames"]).Select(n => (string)n!).ToList(),
            Species = Array(o["species"]).Select(SpeciesRow).ToList(),
            Forms = Array(o["forms"]).Select(SpeciesRow).ToList(),
            Moves = Array(o["moves"]).Select(m => new HackMove((int)m["id"]!, (string)m["name"]!, (int?)m["national"], (int)m["power"]!, (int)m["type"]!, (int)m["accuracy"]!, (int)m["pp"]!)
            {
                Category = (int?)m["category"],
                CategoryByType = (bool?)m["categoryByType"] ?? false,
            }).ToList(),
            Items = Array(o["items"]).Select(i => new HackItem((int)i["id"]!, (string)i["name"]!, (int?)i["national"], (int)i["pocket"]!, (int)i["price"]!)).ToList(),
            Abilities = Array(o["abilities"]).Select(a => new HackAbility((int)a["id"]!, (string)a["name"]!, (int?)a["national"])).ToList(),
            LevelUp = o["learnsets"]?["levelUp"] is JsonObject lu
                ? lu.ToDictionary(kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture),
                    kv => (IReadOnlyList<HackLearn>)Array(kv.Value).Select(e => new HackLearn((int)e[0]!, (int)e[1]!)).ToList())
                : new Dictionary<int, IReadOnlyList<HackLearn>>(),
            Notes = Array(o["notes"]).Select(n => (string)n!).ToList(),
        };
    }

    private static HackTableStatus? StatusRow(JsonNode t) =>
        (string?)t["table"] is { } table && HackTableStatus.Kinds.Contains(table) && (string?)t["status"] is { } status && HackTableStatus.Statuses.Contains(status)
            ? new HackTableStatus(table, status, (string?)t["reason"], (bool?)t["aligned"])
            : null;

    private static HackSpecies SpeciesRow(JsonNode s)
    {
        var row = new HackSpecies((int)s["id"]!, (string)s["name"]!, (int?)s["national"], (int)s["form"]!,
            IntList(s["types"]), IntList(s["base"]), IntList(s["abilities"]), (int?)s["growth"], (int)s["gender"]!, (int?)s["catch"], (int?)s["exp"])
        {
            Dex = (int?)s["dex"],
            BaseSpecies = (int?)s["baseSpecies"],
            FormName = (string?)s["formName"],
        };
        return (bool?)s["genderRead"] is { } genderRead ? row with { GenderRead = genderRead } : row;
    }

    private static IEnumerable<JsonNode> Array(JsonNode? node) => node is JsonArray a ? a.Where(x => x is not null).Select(x => x!) : [];

    private static List<int> IntList(JsonNode? node) => Array(node).Select(x => (int)x).ToList();

    public static HackData? Load(string root, string imageKey)
    {
        var path = PathFor(root, imageKey);
        try
        {
            return File.Exists(path) ? FromJson(File.ReadAllText(path), imageKey) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string root, HackData data)
    {
        var path = PathFor(root, data.RomSha1);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, ToJson(data));
        File.Move(temp, path, overwrite: true);
    }

    public static bool IsGba(ReadOnlySpan<byte> rom) => rom.Length >= 0xC0 && rom[0xB2] == 0x96;

    public static HackDataResult ReadOrBuild(string root, ReadOnlyMemory<byte> rom, IdMapper? mapper = null)
    {
        var key = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(rom.Span));
        var cached = Load(root, key);
        if (cached is { ReaderVersion: >= HackData.CurrentReaderVersion })
            return new HackDataResult(cached, null);
        var built = HackTables.Read(rom, null, mapper);
        if (built.Data is null && cached is not null)
            return new HackDataResult(cached, null);
        if (built.Data is { } data)
        {
            try
            {
                Save(root, data);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return built with { Data = data with { Notes = [.. data.Notes, $"the cache could not be written ({ex.Message})"] } };
            }
        }
        return built;
    }
}
