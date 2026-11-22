using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PKHaX.Hacks.Records;

public enum RecordCodecId
{
    Pk1,
    Pk2,
    Pk3,
    Pk3CfruPlain,
    Pk3CfruCompact58,
    Pk3x,
    Pk3SoulGold96,
    Pk4,
    Pk5,
    Pk6,
    Pk7,
    Pb7,
    Pb8,
}

public static class RecordCodecIds
{
    private static readonly string[] Names =
    [
        "pk1",
        "pk2",
        "pk3",
        "pk3-cfru-plain",
        "pk3-cfru-compact58",
        "pk3x",
        "pk3-soulgold96",
        "pk4",
        "pk5",
        "pk6",
        "pk7",
        "pb7",
        "pb8",
    ];

    public static IReadOnlyList<string> All => Names;

    public static string Name(this RecordCodecId id) => Names[(int)id];

    public static bool TryParse(string? name, out RecordCodecId id)
    {
        var index = name is null ? -1 : Array.IndexOf(Names, name);
        id = index < 0 ? default : (RecordCodecId)index;
        return index >= 0;
    }

    public static bool NeedsVersion(this RecordCodecId id) => id == RecordCodecId.Pk3x;
}

public readonly record struct RecordCodecKey(RecordCodecId Id, string? Version)
{
    public override string ToString() => Version is null ? Id.Name() : $"{Id.Name()}@{Version}";

    public static bool TryCreate(string? codec, string? version, out RecordCodecKey key, [NotNullWhen(false)] out string? why)
    {
        key = default;
        if (!RecordCodecIds.TryParse(codec, out var id))
        {
            why = $"unknown record codec id '{codec}' (known: {string.Join(", ", RecordCodecIds.All)})";
            return false;
        }
        if (!id.NeedsVersion())
        {
            if (!string.IsNullOrEmpty(version))
            {
                why = $"record codec '{codec}' takes no version (got '{version}')";
                return false;
            }
            key = new RecordCodecKey(id, null);
            why = null;
            return true;
        }
        if (!ExpansionVersions.TryNormalize(version, out var normalized, out why))
            return false;
        key = new RecordCodecKey(id, normalized);
        return true;
    }
}
