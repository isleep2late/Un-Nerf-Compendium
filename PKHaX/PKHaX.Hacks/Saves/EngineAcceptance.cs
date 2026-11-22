using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PKHaX.Hacks.Saves;

public sealed record AcceptanceKey(string Family, string ChecksumRule, string Stride, string PartyCodec, string BoxCodec, string Areas)
{
    public override string ToString() => $"{Family}, {ChecksumRule} checksums, stride {Stride}, party {PartyCodec}, boxes {BoxCodec}, areas {Areas}";
}

public sealed record AcceptanceRow(AcceptanceKey Key, string ResultFile, IReadOnlyList<string> Images, IReadOnlyList<string> Fields);

public static class EngineAcceptance
{
    public const string ResultsFolder = "docs/handoffs/results";
    public const string Money = "Money";

    private static readonly char[] Separators = ['/', '\\'];

    public static IReadOnlyList<AcceptanceRow> Rows { get; } =
    [
        new AcceptanceRow(
            new AcceptanceKey("gen3-cfru", "SectionSizes", "0xFF0", "pk3-cfru-plain", "pk3-cfru-compact58", "SaveBlock1+SaveBlock2+Sectors30And31+Storage"),
            "20261121-v103-accept-cfru.md",
            ["b4776b82a4c7915d0fadeaa27e013523f99dfd94", "964f951a0fdaf209e4ea1344883ef0d557bb3a80"],
            ["Ball", "Evs", "Exp", "Friendship", "HeldItem", "HiddenAbility", "Ivs", "Moves", "Nickname", "Party.Level", "Species"]),
        new AcceptanceRow(
            new AcceptanceKey("gen3-expansion", "FullWindows+SectionSizes+UniformStride", "0xF80", "pk3x", "pk3x", "Storage"),
            "20261121-v103-accept-expansion.md",
            ["b9f4d332d30fc88c379f9e037f9eae3b2755ead4"],
            ["AbilitySlot", "Ball", "Evs", "Friendship", "HeldItem", "Ivs", "Moves", "Nature", "Nickname"]),
    ];

    public const string NoAreas = "none";

    public static AcceptanceRow? Find(AcceptanceKey key, IReadOnlyList<AcceptanceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(rows);
        return rows.FirstOrDefault(r => Covers(r.Key, key));
    }

    public static bool Covers(AcceptanceKey accepted, AcceptanceKey key)
    {
        ArgumentNullException.ThrowIfNull(accepted);
        ArgumentNullException.ThrowIfNull(key);
        return accepted.Family == key.Family && accepted.Stride == key.Stride && accepted.PartyCodec == key.PartyCodec && accepted.BoxCodec == key.BoxCodec
            && Parts(accepted.ChecksumRule).Overlaps(Parts(key.ChecksumRule))
            && Parts(key.Areas).IsSubsetOf(Parts(accepted.Areas));
    }

    private static HashSet<string> Parts(string joined) => joined == NoAreas
        ? new HashSet<string>(StringComparer.Ordinal)
        : new HashSet<string>(joined.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);

    public static IReadOnlyList<string> Problems(IReadOnlyList<AcceptanceRow> rows, string resultsFolder)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var problems = new List<string>();
        foreach (var row in rows)
        {
            var name = row.ResultFile;
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Separators) >= 0 || !name.EndsWith(".md", StringComparison.Ordinal))
                problems.Add($"{row.Key}: the result file '{name}' is not a bare .md file name under {ResultsFolder}");
            else if (!File.Exists(Path.Combine(resultsFolder, name)))
                problems.Add($"{row.Key}: the result file {ResultsFolder}/{name} does not exist");
            if (row.Images.Count == 0 || row.Images.Any(i => i.Length != 40 || !i.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))))
                problems.Add($"{row.Key}: the images must be one or more lowercase SHA-1s");
            if (row.Fields.Count == 0)
                problems.Add($"{row.Key}: the row lists no field the game kept");
            if (rows.Count(r => r.Key == row.Key) > 1)
                problems.Add($"{row.Key}: the key is listed more than once");
        }
        return problems.Distinct().ToList();
    }
}
