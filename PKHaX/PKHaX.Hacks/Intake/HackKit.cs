using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;

namespace PKHaX.Hacks.Intake;

public sealed record KitSource(string FileName, long Size, string? PatchSha1 = null, string? BaseSha1 = null, string? PatchFormat = null, string? Note = null, string? RomPath = null)
{
    public bool FromPatch => PatchSha1 is not null;
    public bool FromProfile { get; init; }
}

public sealed record HackKit
{
    public const string Schema = "hackkit/1";
    public const int CurrentDeriveVersion = 1;
    public const string FileName = "kit.json";
    public const string ProfilePrefix = "kit-";
    public const string Vanilla = "vanilla";

    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Platform { get; init; }
    public string? BaseGame { get; init; }
    public string? GameCode { get; init; }
    public string? Engine { get; init; }
    public string SaveFamily { get; init; } = Vanilla;
    public string? Shape { get; init; }
    public HackDataCounts Counts { get; init; } = new(0, 0, 0, 0);
    public IReadOnlyList<string> Report { get; init; } = [];
    public int DeriveVersion { get; init; } = CurrentDeriveVersion;
    public int ReaderVersion { get; init; }
    public required KitSource Source { get; init; }
    public DateTimeOffset At { get; init; }

    public bool Stale => DeriveVersion < CurrentDeriveVersion;

    public string ProfileId => ProfilePrefix + Key;

    public bool NativeFamily => SaveFamily is SaveShape.Cfru or SaveShape.Expansion;

    public GameVersion? Version => BaseGame is { } name && Enum.TryParse<GameVersion>(name, out var v) && v.IsValidSavedVersion() ? v : null;

    public string Summary => string.Create(CultureInfo.InvariantCulture, $"{Name} ({BaseGame ?? Platform}): {Counts.Species} species, {Counts.Moves} moves, {Counts.Items} items, {Counts.Abilities} abilities; save {SaveFamily}{(Stale ? $"; {IntakeText.StaleKit}" : "")}");

    public const string GbTitleSource = "pret Makefiles, rgbfix -t: pokered \"POKEMON RED\" and \"POKEMON BLUE\", pokeyellow \"POKEMON YELLOW\", pokegold POKEMON_GLD and POKEMON_SLV, pokecrystal PM_CRYSTAL, at ROM 0x134";

    private static readonly (string Title, GameVersion Game, string Engine)[] GbTitles =
    [
        ("POKEMON RED", GameVersion.RD, HackTables.Gb1Engine),
        ("POKEMON BLUE", GameVersion.BU, HackTables.Gb1Engine),
        ("POKEMON YELLOW", GameVersion.YW, HackTables.Gb1Engine),
        ("POKEMON_GLD", GameVersion.GD, HackTables.Gb2Engine),
        ("POKEMON_SLV", GameVersion.SI, HackTables.Gb2Engine),
        ("PM_CRYSTAL", GameVersion.C, HackTables.Gb2Engine),
    ];

    public static string? GbTitle(ReadOnlySpan<byte> rom)
    {
        if (rom.Length < 0x150)
            return null;
        var title = rom.Slice(0x134, 16);
        var end = 0;
        while (end < title.Length && title[end] is >= 0x20 and <= 0x7E)
            end++;
        return end == 0 ? null : System.Text.Encoding.ASCII.GetString(title[..end]);
    }

    public static string? BaseGameOf(string? platform, string? gameCode, string? engine, string? gbTitle = null)
    {
        var code = gameCode is { Length: >= 3 } c ? c[..3] : "";
        GameVersion? v = platform switch
        {
            HackTables.Gba => code switch
            {
                "AXV" => (GameVersion?)GameVersion.R,
                "AXP" => GameVersion.S,
                "BPE" => GameVersion.E,
                "BPR" => GameVersion.FR,
                "BPG" => GameVersion.LG,
                _ => null,
            },
            HackTables.Nds => code switch
            {
                "ADA" => (GameVersion?)GameVersion.D,
                "APA" => GameVersion.P,
                "CPU" => GameVersion.Pt,
                "IPK" => GameVersion.HG,
                "IPG" => GameVersion.SS,
                "IRB" => GameVersion.B,
                "IRA" => GameVersion.W,
                "IRE" => GameVersion.B2,
                "IRD" => GameVersion.W2,
                _ => null,
            },
            HackTables.Gb => GbGame(gbTitle, engine),
            _ => null,
        };
        return v?.ToString();
    }

    private static GameVersion? GbGame(string? title, string? engine)
    {
        if (title is null)
            return null;
        foreach (var (prefix, game, gbEngine) in GbTitles)
        {
            if (title.StartsWith(prefix, StringComparison.Ordinal) && (engine is null || engine == gbEngine))
                return game;
        }
        return null;
    }

    public string ToJson()
    {
        var o = new JsonObject
        {
            ["schema"] = Schema,
            ["key"] = Key,
            ["name"] = Name,
            ["platform"] = Platform,
            ["baseGame"] = BaseGame,
            ["gameCode"] = GameCode,
            ["engine"] = Engine,
            ["saveFamily"] = SaveFamily,
            ["shape"] = Shape,
            ["counts"] = new JsonObject
            {
                ["species"] = Counts.Species, ["moves"] = Counts.Moves, ["items"] = Counts.Items, ["abilities"] = Counts.Abilities,
                ["forms"] = Counts.Forms, ["levelUp"] = Counts.LevelUp,
            },
            ["report"] = new JsonArray(Report.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray()),
            ["deriveVersion"] = DeriveVersion,
            ["readerVersion"] = ReaderVersion,
            ["source"] = new JsonObject
            {
                ["fileName"] = Source.FileName,
                ["size"] = Source.Size,
                ["patchSha1"] = Source.PatchSha1,
                ["baseSha1"] = Source.BaseSha1,
                ["patchFormat"] = Source.PatchFormat,
                ["note"] = Source.Note,
                ["romPath"] = Source.RomPath,
                ["fromProfile"] = Source.FromProfile,
            },
            ["at"] = At.ToString("O", CultureInfo.InvariantCulture),
        };
        return o.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    public string SameIgnoringSource() => (this with { Name = "", Source = new KitSource("", 0), At = default, Report = [] }).ToJson();

    public static HackKit? FromJson(string json, string key)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject o || (string?)o["schema"] != Schema || !string.Equals((string?)o["key"], key, StringComparison.OrdinalIgnoreCase)
                || (string?)o["name"] is not { } name || (string?)o["platform"] is not { } platform || o["source"] is not JsonObject s || (string?)s["fileName"] is not { } file)
                return null;
            var counts = o["counts"] as JsonObject;
            return new HackKit
            {
                Key = key.ToLowerInvariant(),
                Name = name,
                Platform = platform,
                BaseGame = (string?)o["baseGame"],
                GameCode = (string?)o["gameCode"],
                Engine = (string?)o["engine"],
                SaveFamily = (string?)o["saveFamily"] ?? Vanilla,
                Shape = (string?)o["shape"],
                Counts = new HackDataCounts((int?)counts?["species"] ?? 0, (int?)counts?["moves"] ?? 0, (int?)counts?["items"] ?? 0, (int?)counts?["abilities"] ?? 0,
                    (int?)counts?["forms"] ?? 0, (int?)counts?["levelUp"] ?? 0),
                Report = o["report"] is JsonArray r ? r.Where(x => x is not null).Select(x => (string)x!).ToList() : [],
                DeriveVersion = (int?)o["deriveVersion"] ?? 0,
                ReaderVersion = (int?)o["readerVersion"] ?? 0,
                Source = new KitSource(file, (long?)s["size"] ?? 0, (string?)s["patchSha1"], (string?)s["baseSha1"], (string?)s["patchFormat"], (string?)s["note"], (string?)s["romPath"])
                {
                    FromProfile = (bool?)s["fromProfile"] ?? false,
                },
                At = DateTimeOffset.TryParse((string?)o["at"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : default,
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or InvalidCastException or ArgumentException)
        {
            return null;
        }
    }
}
