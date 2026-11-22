using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PKHeX.Core;

namespace PKHaX.Hacks.Records;

public static class RecordCodecs
{
    private static readonly ConcurrentDictionary<RecordCodecKey, IHackRecordCodec> Cache = new();

    public static IReadOnlyList<string> Ids => RecordCodecIds.All;

    public static bool TryGet(string? codec, string? version, [NotNullWhen(true)] out IHackRecordCodec? result, [NotNullWhen(false)] out string? why)
    {
        result = null;
        if (!RecordCodecKey.TryCreate(codec, version, out var key, out why))
            return false;
        result = Cache.GetOrAdd(key, Create);
        return true;
    }

    public static IHackRecordCodec For(string codec, string? version = null)
    {
        if (!TryGet(codec, version, out var result, out var why))
            throw new ArgumentException(why, nameof(codec));
        return result;
    }

    public static IHackRecordCodec For(RecordCodecId id, string? version = null) => For(id.Name(), version);

    private static IHackRecordCodec Create(RecordCodecKey key) => key.Id switch
    {
        RecordCodecId.Pk1 => new Pk1Codec(),
        RecordCodecId.Pk2 => new Pk2Codec(),
        RecordCodecId.Pk3 => new Pk3Codec(),
        RecordCodecId.Pk3CfruPlain => new Pk3CfruPlainCodec(),
        RecordCodecId.Pk3CfruCompact58 => new Pk3CfruCompact58Codec(),
        RecordCodecId.Pk3x => new Pk3xCodec(key.Version!),
        RecordCodecId.Pk3SoulGold96 => new Pk3SoulGold96Codec(),
        RecordCodecId.Pk4 => new PkhexEntityCodec(key.Id, 236, 136, data => new PK4(data)),
        RecordCodecId.Pk5 => new PkhexEntityCodec(key.Id, 220, 136, data => new PK5(data)),
        RecordCodecId.Pk6 => new PkhexEntityCodec(key.Id, 260, 232, data => new PK6(data)),
        RecordCodecId.Pk7 => new PkhexEntityCodec(key.Id, 260, 232, data => new PK7(data)),
        RecordCodecId.Pb7 => new PkhexEntityCodec(key.Id, 260, 260, data => new PB7(data)),
        RecordCodecId.Pb8 => new PkhexEntityCodec(key.Id, 344, 328, data => new PB8(data)),
        _ => throw new ArgumentOutOfRangeException(nameof(key)),
    };
}
