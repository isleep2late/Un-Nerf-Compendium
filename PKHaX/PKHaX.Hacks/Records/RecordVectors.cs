using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PKHaX.Hacks.Records;

public sealed record RecordVector(string Codec, string? Version, string File, string Source, string Oracle, JsonElement Expect, string? Note, string? Refuse)
{
    public string Label => Version is null ? Codec : $"{Codec}@{Version}";
    public bool Known => RecordVectors.KnownOracles.Contains(Oracle) && Refuse is null && Expect.ValueKind == JsonValueKind.Object
        && Expect.EnumerateObject().Any(p => !RecordVectors.IdentityFields.Contains(p.Name));
}

public sealed record RecordVectorResult(RecordVector Vector, bool Passed, IReadOnlyList<string> Failures, HackMon? Mon);

public static class RecordVectors
{
    public const string Schema = "record-vectors/1";

    public static readonly IReadOnlySet<string> KnownOracles = new HashSet<string>(StringComparer.Ordinal) { "given", "pokedaisy", "statcheck", "nickname", "pkhex" };

    public static readonly IReadOnlySet<string> UnknownOracles = new HashSet<string>(StringComparer.Ordinal) { "observed", "checksum", "plant" };

    public static readonly IReadOnlySet<string> IdentityFields = new HashSet<string>(StringComparer.Ordinal) { "species", "national", "heldItem" };

    public static IReadOnlyList<RecordVector> Load(string root)
    {
        var list = new List<RecordVector>();
        foreach (var path in Directory.EnumerateFiles(root, "vectors*.json").Order(StringComparer.Ordinal))
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllBytes(path));
            var top = doc.RootElement;
            if (!top.TryGetProperty("schema", out var schema) || schema.GetString() != Schema)
                throw new InvalidDataException($"{path}: schema is not {Schema}");
            foreach (var v in top.GetProperty("vectors").EnumerateArray())
            {
                var oracle = Str(v, "oracle") ?? "observed";
                if (!KnownOracles.Contains(oracle) && !UnknownOracles.Contains(oracle))
                    throw new InvalidDataException($"{path}: {Str(v, "file")}: unknown oracle '{oracle}'");
                list.Add(new RecordVector(
                    v.GetProperty("codec").GetString()!,
                    Str(v, "version"),
                    v.GetProperty("file").GetString()!,
                    Str(v, "source") ?? "",
                    oracle,
                    v.TryGetProperty("expect", out var e) ? e.Clone() : default,
                    Str(v, "note"),
                    Str(v, "refuse")));
            }
        }
        return list;
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    public static RecordVectorResult Check(string root, RecordVector vector)
    {
        var failures = new List<string>();
        if (!RecordCodecs.TryGet(vector.Codec, vector.Version, out var codec, out var keyWhy))
        {
            if (vector.Refuse is { } r && keyWhy.Contains(r, StringComparison.OrdinalIgnoreCase))
                return new RecordVectorResult(vector, true, failures, null);
            failures.Add($"codec refused: {keyWhy}");
            return new RecordVectorResult(vector, false, failures, null);
        }
        var bytes = System.IO.File.ReadAllBytes(Path.Combine(root, vector.File));
        var mon = codec.Decode(bytes, out var why);
        if (vector.Refuse is { } refuse)
        {
            if (mon is not null)
                failures.Add($"decoded although the vector expects a refusal containing '{refuse}'");
            else if (why is null || !why.Contains(refuse, StringComparison.OrdinalIgnoreCase))
                failures.Add($"refused with '{why}', expected '{refuse}'");
            return new RecordVectorResult(vector, failures.Count == 0, failures, mon);
        }
        if (mon is null)
        {
            failures.Add($"no decode: {why}");
            return new RecordVectorResult(vector, false, failures, null);
        }
        if (vector.Expect.ValueKind == JsonValueKind.Object)
            failures.AddRange(Compare(mon, vector.Expect));
        return new RecordVectorResult(vector, failures.Count == 0, failures, mon);
    }

    public static IReadOnlyList<string> Compare(HackMon mon, JsonElement expect)
    {
        var failures = new List<string>();
        foreach (var p in expect.EnumerateObject())
        {
            var actual = Field(mon, p.Name);
            if (actual is null && p.Value.ValueKind != JsonValueKind.Null)
            {
                failures.Add($"{p.Name}: the decode has no such field");
                continue;
            }
            var want = Render(p.Value);
            if (!string.Equals(want, actual, StringComparison.Ordinal))
                failures.Add($"{p.Name}: expected {want}, decoded {actual}");
        }
        return failures;
    }

    private static string? Field(HackMon m, string name) => name switch
    {
        "species" => m.Species.ToString(),
        "national" => m.National?.ToString() ?? "null",
        "form" => m.Form.ToString(),
        "pid" => m.Pid.ToString(),
        "tid" => m.Tid16.ToString(),
        "sid" => m.Sid16.ToString(),
        "nickname" => Quote(m.Nickname),
        "otName" => Quote(m.OtName),
        "language" => m.Language?.ToString() ?? "null",
        "exp" => m.Exp.ToString(),
        "level" => m.Level?.ToString() ?? "null",
        "moves" => List(m.Moves.Select(x => (int)x)),
        "pp" => m.Pp is null ? "null" : List(m.Pp.Select(x => (int)x)),
        "pp0" => m.Pp is null ? "null" : m.Pp[0].ToString(),
        "ppUps" => m.PpUps is null ? "null" : List(m.PpUps.Select(x => (int)x)),
        "heldItem" => m.HeldItem.ToString(),
        "catchRate" => m.CatchRate?.ToString() ?? "null",
        "ball" => m.Ball?.ToString() ?? "null",
        "ivs" => m.Ivs is { } iv ? Stats(iv) : "null",
        "evs" => Stats(m.Evs),
        "nature" => m.Nature?.ToString() ?? "null",
        "abilitySlot" => m.AbilitySlot?.ToString() ?? "null",
        "ability" => m.Ability?.ToString() ?? "null",
        "hiddenAbility" => Bool(m.HiddenAbility),
        "isEgg" => m.IsEgg ? "true" : "false",
        "isShiny" => Bool(m.IsShiny),
        "shinyXor" => m.ShinyXor?.ToString() ?? "null",
        "friendship" => m.Friendship?.ToString() ?? "null",
        "teraType" => m.TeraType?.ToString() ?? "null",
        "currentHp" => m.Party?.CurrentHp.ToString() ?? "null",
        "maxHp" => m.Party?.Stats.Hp.ToString() ?? "null",
        "status" => m.Party?.Status.ToString() ?? "null",
        "stats" => m.Party is { } p ? Stats(p.Stats) : "null",
        _ => null,
    };

    private static string Bool(bool? b) => b is null ? "null" : b.Value ? "true" : "false";
    private static string Quote(string? s) => s is null ? "null" : JsonSerializer.Serialize(s);
    private static string List(IEnumerable<int> values) => "[" + string.Join(",", values) + "]";
    private static string Stats(StatSet s) => List([s.Hp, s.Atk, s.Def, s.SpA, s.SpD, s.Spe]);

    private static string Render(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Array => "[" + string.Join(",", e.EnumerateArray().Select(Render)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(e.GetString()),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => "null",
        _ => e.GetRawText(),
    };
}
