using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PKHaX.Hacks.Profiles;

public sealed record SaveTrainer(uint Id, string Name);

public sealed record Pairing(string SavePath, uint? TrainerId, string? TrainerName, string ProfileId, DateTimeOffset At)
{
    public bool SameTrainer(SaveTrainer? trainer) => trainer is null || TrainerId is null || (TrainerId == trainer.Id && TrainerName == trainer.Name);
}

public sealed class PairingMemory
{
    public const string Schema = "pkhax-pairings/1";
    public const string FileName = "pairings.json";
    public const int MaxEntries = 2000;

    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string? file;
    private readonly object gate = new();
    private readonly List<Pairing> entries = [];
    private bool keepFile;

    public PairingMemory(string? file)
    {
        this.file = file;
        Load();
    }

    public static string PathFor(string appDataRoot) => Path.Combine(appDataRoot, "pkhax", FileName);

    public string? LoadProblem { get; private set; }
    public string? SaveProblem { get; private set; }

    public IReadOnlyList<Pairing> Entries
    {
        get
        {
            lock (gate)
                return entries.ToList();
        }
    }

    public static string? Normalise(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (IsContentToken(path))
            return path;
        if (path.Contains("://", StringComparison.Ordinal))
            return null;
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public static bool IsContentToken(string? path) =>
        path is not null && path.StartsWith("content://", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(path, UriKind.Absolute, out _);

    public Pairing? ByPath(string? savePath)
    {
        if (Normalise(savePath) is not { } p)
            return null;
        lock (gate)
            return entries.LastOrDefault(e => string.Equals(e.SavePath, p, PathComparison));
    }

    public IReadOnlyList<string> ByTrainer(SaveTrainer? trainer)
    {
        if (trainer is null || trainer.Name.Length == 0)
            return [];
        lock (gate)
            return entries.Where(e => e.TrainerId == trainer.Id && e.TrainerName == trainer.Name).Select(e => e.ProfileId).Distinct(StringComparer.Ordinal).ToList();
    }

    public bool Remember(string savePath, SaveTrainer? trainer, string profileId, DateTimeOffset? at = null)
    {
        if (Normalise(savePath) is not { } p)
            return false;
        lock (gate)
        {
            entries.RemoveAll(e => string.Equals(e.SavePath, p, PathComparison));
            entries.Add(new Pairing(p, trainer?.Id, trainer?.Name, profileId, at ?? DateTimeOffset.UtcNow));
            if (entries.Count > MaxEntries)
                entries.RemoveRange(0, entries.Count - MaxEntries);
            return Save();
        }
    }

    public bool Forget(string savePath)
    {
        if (Normalise(savePath) is not { } p)
            return false;
        lock (gate)
            return entries.RemoveAll(e => string.Equals(e.SavePath, p, PathComparison)) > 0 && Save();
    }

    private void Load()
    {
        if (file is null || !File.Exists(file))
            return;
        try
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject o || o["schema"] is not JsonValue sv || !sv.TryGetValue(out string? schema) || o["pairings"] is not JsonArray list)
            {
                Keep($"{file} is not a {Schema} file");
                return;
            }
            if (schema != Schema)
            {
                Keep($"{file} is a {schema} file, which this version does not write");
                return;
            }
            foreach (var n in list)
            {
                if (n is not JsonObject e || Str(e, "savePath") is not { } path || Str(e, "profileId") is not { } id)
                    continue;
                uint? tid = PkhaxProfileReader.Integer(e["trainerId"]) is { } t && t is >= 0 and <= uint.MaxValue ? (uint)t : null;
                var at = Str(e, "at") is { } s && DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var d) ? d : DateTimeOffset.MinValue;
                entries.Add(new Pairing(path, tid, Str(e, "trainerName"), id, at));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            entries.Clear();
            Keep($"{file} could not be read ({ex.Message})");
        }
    }

    private void Keep(string why)
    {
        keepFile = true;
        LoadProblem = why + "; it is left as it is and answers are kept for this session only.";
    }

    private bool Save()
    {
        if (file is null || keepFile)
            return false;
        var o = new JsonObject
        {
            ["schema"] = Schema,
            ["pairings"] = new JsonArray(entries.Select(e => (JsonNode)new JsonObject
            {
                ["savePath"] = e.SavePath,
                ["trainerId"] = e.TrainerId,
                ["trainerName"] = e.TrainerName,
                ["profileId"] = e.ProfileId,
                ["at"] = e.At.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            }).ToArray()),
        };
        var temp = $"{file}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
            File.WriteAllText(temp, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, file, true);
            SaveProblem = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveProblem = $"{file} was not written ({ex.Message}); the answer is kept for this session only.";
            try
            {
                File.Delete(temp);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }
            return false;
        }
    }

    private static string? Str(JsonObject o, string key) => o[key] is JsonValue v && v.TryGetValue(out string? s) ? s : null;
}
