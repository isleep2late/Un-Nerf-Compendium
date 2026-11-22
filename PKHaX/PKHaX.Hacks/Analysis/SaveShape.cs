using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Cfru;

namespace PKHaX.Hacks.Analysis;

public enum ShapeTier
{
    Derived,
    Family,
    Unknown,
}

public sealed record ShapeHeader(int Shift, int SaveBlock2Size, int SaveBlock1Size, int PartyCountOffset, int PartyOffset, int TrainerIdOffset, int PlayerNameOffset);

public sealed record ShapeBoxSource(int FirstBox, int Count, CfruBoxArea Area, int Offset, string Origin, ShapeTier Tier, bool NeedsSaveCheck, bool WritableFromRomAlone)
{
    public int LastBox => FirstBox + Count - 1;
}

public sealed record ShapeMoney(int Offset, int? KeyOffset, string Origin, ShapeTier Tier);

public sealed record ShapePocket(int Offset, int Capacity);

public sealed record ShapeBag(IReadOnlyList<ShapePocket> Pockets, string Origin, ShapeTier Tier);

public sealed record SaveShape
{
    public const string Schema = "saveshape/1";
    public const int CurrentVersion = 1;
    public const string Retail = "gen3-retail";
    public const string Cfru = "gen3-cfru";
    public const string Expansion = "gen3-expansion";
    public const string Gb1Retail = "gb1-retail";
    public const string UnknownFamily = "unknown";

    public static IReadOnlyList<string> Families { get; } = [Retail, Cfru, Expansion, Gb1Retail, UnknownFamily];

    public required string RomSha1 { get; init; }
    public int Version { get; init; } = CurrentVersion;
    public string Family { get; init; } = UnknownFamily;
    public string? Reason { get; init; }
    public ShapeTier FamilyTier { get; init; } = ShapeTier.Unknown;
    public string FamilyOrigin { get; init; } = "";
    public IReadOnlyList<ShapeHeader> Headers { get; init; } = [];
    public RecordCodecKey? PartyCodec { get; init; }
    public RecordCodecKey? BoxCodec { get; init; }
    public IReadOnlyList<RecordCodecKey> PartyCodecCandidates { get; init; } = [];
    public int? FixedStride { get; init; }
    public int? FixedStorageSize { get; init; }
    public ShapeTier LayoutTier { get; init; } = ShapeTier.Unknown;
    public IReadOnlyList<ShapeBoxSource> BoxSources { get; init; } = [];
    public IReadOnlyList<string> BoxNotes { get; init; } = [];
    public IReadOnlyList<uint> Signatures { get; init; } = [];
    public ShapeMoney? Money { get; init; }
    public ShapeBag? Bag { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];

    public bool Known => Family != UnknownFamily;

    public static SaveShape Unknown(string romSha1, string reason) => new() { RomSha1 = romSha1.ToLowerInvariant(), Reason = reason };

    public string ToJson()
    {
        var o = new JsonObject
        {
            ["schema"] = Schema,
            ["version"] = Version,
            ["romSha1"] = RomSha1,
            ["family"] = Family,
            ["reason"] = Reason,
            ["familyTier"] = FamilyTier.ToString(),
            ["familyOrigin"] = FamilyOrigin,
            ["headers"] = new JsonArray(Headers.Select(h => (JsonNode)new JsonObject
            {
                ["shift"] = h.Shift, ["saveBlock2Size"] = h.SaveBlock2Size, ["saveBlock1Size"] = h.SaveBlock1Size, ["partyCount"] = h.PartyCountOffset,
                ["party"] = h.PartyOffset, ["trainerId"] = h.TrainerIdOffset, ["playerName"] = h.PlayerNameOffset,
            }).ToArray()),
            ["partyCodec"] = Codec(PartyCodec),
            ["boxCodec"] = Codec(BoxCodec),
            ["partyCodecCandidates"] = new JsonArray(PartyCodecCandidates.Select(c => (JsonNode)Codec(c)!).ToArray()),
            ["fixedStride"] = FixedStride,
            ["fixedStorageSize"] = FixedStorageSize,
            ["layoutTier"] = LayoutTier.ToString(),
            ["boxSources"] = new JsonArray(BoxSources.Select(b => (JsonNode)new JsonObject
            {
                ["firstBox"] = b.FirstBox, ["count"] = b.Count, ["area"] = b.Area.ToString(), ["offset"] = b.Offset, ["origin"] = b.Origin,
                ["tier"] = b.Tier.ToString(), ["needsSaveCheck"] = b.NeedsSaveCheck, ["writableFromRomAlone"] = b.WritableFromRomAlone,
            }).ToArray()),
            ["boxNotes"] = Strings(BoxNotes),
            ["signatures"] = new JsonArray(Signatures.Select(s => (JsonNode)JsonValue.Create((long)s)).ToArray()),
            ["money"] = Money is { } m ? new JsonObject { ["offset"] = m.Offset, ["keyOffset"] = m.KeyOffset, ["origin"] = m.Origin, ["tier"] = m.Tier.ToString() } : null,
            ["bag"] = Bag is { } bag ? new JsonObject
            {
                ["pockets"] = new JsonArray(bag.Pockets.Select(p => (JsonNode)new JsonObject { ["offset"] = p.Offset, ["capacity"] = p.Capacity }).ToArray()),
                ["origin"] = bag.Origin, ["tier"] = bag.Tier.ToString(),
            } : null,
            ["notes"] = Strings(Notes),
        };
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static JsonArray Strings(IEnumerable<string> values) => new(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray());

    private static JsonNode? Codec(RecordCodecKey? key) => key is { } k ? new JsonObject { ["id"] = k.Id.Name(), ["version"] = k.Version } : null;

    public static SaveShape? FromJson(string json, string romSha1) => FromJson(json, romSha1, CurrentVersion);

    public static SaveShape? FromJson(string json, string romSha1, int? wantedVersion)
    {
        try
        {
            return Parse(json, romSha1, wantedVersion);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or NullReferenceException
            or OverflowException or ArgumentException or IndexOutOfRangeException or InvalidCastException or KeyNotFoundException)
        {
            return null;
        }
    }

    private static SaveShape? Parse(string json, string romSha1, int? wantedVersion)
    {
        if (JsonNode.Parse(json) is not JsonObject o || (string?)o["schema"] != Schema || (int?)o["version"] is not { } version
            || (wantedVersion is { } wanted && version != wanted)
            || !string.Equals((string?)o["romSha1"], romSha1, StringComparison.OrdinalIgnoreCase)
            || (string?)o["family"] is not { } family || !Families.Contains(family))
            return null;
        return new SaveShape
        {
            RomSha1 = romSha1.ToLowerInvariant(),
            Version = version,
            Family = family,
            Reason = (string?)o["reason"],
            FamilyTier = Tier(o["familyTier"]),
            FamilyOrigin = (string?)o["familyOrigin"] ?? "",
            Headers = Array(o["headers"]).Select(h => new ShapeHeader((int)h["shift"]!, (int)h["saveBlock2Size"]!, (int)h["saveBlock1Size"]!, (int)h["partyCount"]!,
                (int)h["party"]!, (int)h["trainerId"]!, (int)h["playerName"]!)).ToList(),
            PartyCodec = Key(o["partyCodec"]),
            BoxCodec = Key(o["boxCodec"]),
            PartyCodecCandidates = Array(o["partyCodecCandidates"]).Select(c => Key(c) ?? throw new FormatException("codec")).ToList(),
            FixedStride = (int?)o["fixedStride"],
            FixedStorageSize = (int?)o["fixedStorageSize"],
            LayoutTier = Tier(o["layoutTier"]),
            BoxSources = Array(o["boxSources"]).Select(b => new ShapeBoxSource((int)b["firstBox"]!, (int)b["count"]!, Enum.Parse<CfruBoxArea>(Text(b["area"])), (int)b["offset"]!,
                Text(b["origin"]), Tier(b["tier"]), (bool)b["needsSaveCheck"]!, (bool)b["writableFromRomAlone"]!)).ToList(),
            BoxNotes = Array(o["boxNotes"]).Select(n => Text(n)).ToList(),
            Signatures = Array(o["signatures"]).Select(s => checked((uint)(long)s)).ToList(),
            Money = o["money"] is JsonObject m ? new ShapeMoney((int)m["offset"]!, (int?)m["keyOffset"], Text(m["origin"]), Tier(m["tier"])) : null,
            Bag = o["bag"] is JsonObject bag
                ? new ShapeBag(Array(bag["pockets"]).Select(p => new ShapePocket((int)p["offset"]!, (int)p["capacity"]!)).ToList(), Text(bag["origin"]), Tier(bag["tier"]))
                : null,
            Notes = Array(o["notes"]).Select(n => Text(n)).ToList(),
        };
    }

    private static string Text(JsonNode? node) => (string?)node ?? throw new FormatException("a text value is missing");

    private static ShapeTier Tier(JsonNode? node) => Enum.Parse<ShapeTier>(Text(node));

    private static RecordCodecKey? Key(JsonNode? node)
    {
        if (node is not JsonObject o)
            return null;
        if (!RecordCodecKey.TryCreate((string?)o["id"], (string?)o["version"], out var key, out var why))
            throw new FormatException(why);
        return key;
    }

    private static IEnumerable<JsonNode> Array(JsonNode? node) => node is JsonArray a ? a.Where(x => x is not null).Select(x => x!) : [];

    public string Describe() => Known
        ? string.Create(CultureInfo.InvariantCulture, $"{Family} ({FamilyTier}), {Headers.Count} header candidate(s), party {PartyCodec?.ToString() ?? string.Join(" or ", PartyCodecCandidates)}, boxes {BoxCodec?.ToString() ?? "unknown"}, {BoxSources.Sum(b => b.Count)} box(es) located, {Signatures.Count} signature(s)")
        : $"unknown ({Reason})";
}
