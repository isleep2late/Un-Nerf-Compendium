using System;

namespace PKHaX.Hacks.Records;

public interface IHackRecordCodec
{
    RecordCodecKey Key { get; }
    int? PartySize { get; }
    int? StoredSize { get; }
    bool Encrypted { get; }
    HackMon? Decode(ReadOnlySpan<byte> record, out string? why);
}

internal static class RecordShapes
{
    public static bool TryShape(IHackRecordCodec codec, int length, out RecordShape shape, out string? why)
    {
        if (codec.PartySize == length)
        {
            shape = RecordShape.Party;
            why = null;
            return true;
        }
        if (codec.StoredSize == length)
        {
            shape = RecordShape.Stored;
            why = null;
            return true;
        }
        shape = default;
        why = $"{codec.Key} takes {Sizes(codec)} bytes, got {length}";
        return false;
    }

    private static string Sizes(IHackRecordCodec codec) => (codec.PartySize, codec.StoredSize) switch
    {
        ({ } p, { } s) when p != s => $"{p} (party) or {s} (stored)",
        ({ } p, _) => $"{p}",
        (_, { } s) => $"{s}",
        _ => "no",
    };
}
