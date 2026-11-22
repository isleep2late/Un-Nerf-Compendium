using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Editing;

namespace PKHaX.Hacks.Records;

public static class Pk3CfruCompact58Encoder
{
    public const int Size = 58;
    public const int MoveBits = 10;

    public static IReadOnlyList<EncodedField> Fields { get; } =
    [
        EncodedField.Of("Species", EditFieldKind.SpeciesId, 1, RecordPlausibility.CfruSpeciesCap, new FieldBits(0x1C, 0, 16)),
        EncodedField.Of("HeldItem", EditFieldKind.ItemId, 0, 0xFFFF, new FieldBits(0x1E, 0, 16)),
        EncodedField.Of("Exp", EditFieldKind.Number, 0, int.MaxValue, new FieldBits(0x20, 0, 32)),
        EncodedField.Of("Friendship", EditFieldKind.Number, 0, 255, new FieldBits(0x25, 0, 8)),
        EncodedField.Of("Ball", EditFieldKind.Number, 0, 255, new FieldBits(0x26, 0, 8)),
        EncodedField.Of("Moves", EditFieldKind.MoveId, 0, (1 << MoveBits) - 1, new FieldBits(0x27, 0, 4 * MoveBits)),
        EncodedField.Of("Evs", EditFieldKind.Number, 0, 255, new FieldBits(0x2C, 0, 48)),
        EncodedField.Of("Ivs", EditFieldKind.Number, 0, 31, new FieldBits(0x36, 0, 30)),
        EncodedField.Of("IsEgg", EditFieldKind.Flag, 0, 1, new FieldBits(0x36, 30, 1), new FieldBits(0x13, 2, 1)),
        EncodedField.Of("HiddenAbility", EditFieldKind.Flag, 0, 1, new FieldBits(0x36, 31, 1)),
        EncodedField.Of(EditFieldSpecs.Nickname, EditFieldKind.Text, 1, Pk3CfruPlainEncoder.NicknameLength, new FieldBits(Pk3CfruPlainEncoder.Nick, 0, Pk3CfruPlainEncoder.NicknameLength * 8)),
    ];

    public static IReadOnlyList<FieldBits> Derived { get; } = [];

    public static byte[] Plain(ReadOnlySpan<byte> record) => record.ToArray();

    public static byte[] Encode(HackMon edited)
    {
        if (edited.Codec.Id != RecordCodecId.Pk3CfruCompact58 || edited.Raw.Length != Size)
            throw new RecordEncodeException($"the CFRU box encoder takes a {Size}-byte pk3-cfru-compact58 record, got {edited.Codec} of {edited.Raw.Length} bytes");
        var codec = RecordCodecs.For(RecordCodecId.Pk3CfruCompact58);
        var before = codec.Decode(edited.Raw.Span, out var why) ?? throw new RecordEncodeException($"the original record does not decode: {why}");
        var refused = Unsupported(before, edited).ToList();
        if (refused.Count > 0)
            throw new RecordEncodeException($"these fields cannot be written into a CFRU box record: {string.Join(", ", refused)}");
        var bytes = edited.Raw.ToArray();
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x1C..], edited.Species);
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x1E..], edited.HeldItem);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x20..], edited.Exp);
        if (edited.PpUps is { } ups)
            span[0x24] = (byte)((ups[0] & 3) | ((ups[1] & 3) << 2) | ((ups[2] & 3) << 4) | ((ups[3] & 3) << 6));
        if (edited.Friendship is { } friendship)
            span[0x25] = friendship;
        if (edited.Ball is { } ball)
            span[0x26] = ball;
        ulong packed = 0;
        for (var i = 0; i < 4; i++)
            packed |= (ulong)(edited.Moves[i] & 0x3FF) << (MoveBits * i);
        for (var i = 0; i < 5; i++)
            span[0x27 + i] = (byte)(packed >> (8 * i));
        if (edited.Nickname != before.Nickname)
            Gen3Record.EncodeText(edited.Nickname, edited.Language, Pk3CfruPlainEncoder.NicknameLength).CopyTo(span[Pk3CfruPlainEncoder.Nick..]);
        span[0x2C] = (byte)edited.Evs.Hp;
        span[0x2D] = (byte)edited.Evs.Atk;
        span[0x2E] = (byte)edited.Evs.Def;
        span[0x2F] = (byte)edited.Evs.Spe;
        span[0x30] = (byte)edited.Evs.SpA;
        span[0x31] = (byte)edited.Evs.SpD;
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(span[0x36..]);
        if (edited.Ivs is { } ivs)
            ivWord = (ivWord & 0xC000_0000) | (uint)ivs.Hp | ((uint)ivs.Atk << 5) | ((uint)ivs.Def << 10) | ((uint)ivs.Spe << 15) | ((uint)ivs.SpA << 20) | ((uint)ivs.SpD << 25);
        ivWord = edited.IsEgg ? ivWord | (1u << 30) : ivWord & ~(1u << 30);
        if (edited.HiddenAbility is { } hidden)
            ivWord = hidden ? ivWord | (1u << 31) : ivWord & ~(1u << 31);
        BinaryPrimitives.WriteUInt32LittleEndian(span[0x36..], ivWord);
        span[0x13] = (byte)((span[0x13] & ~4) | (edited.IsEgg ? 4 : 0));
        if (codec.Decode(bytes, out why) is not { } after)
            throw new RecordEncodeException($"the edited record would not decode as a CFRU box record: {why}");
        if (after.Nickname != edited.Nickname)
            throw new RecordEncodeException($"the edited record would read the nickname \"{after.Nickname}\", not \"{edited.Nickname}\"");
        return bytes;
    }

    public static IEnumerable<string> Unsupported(HackMon before, HackMon after)
    {
        foreach (var field in Pk3CfruPlainEncoder.Unsupported(before, after))
            yield return field;
        if (before.Level != after.Level)
            yield return "Level (it follows EXP)";
        if (after.Moves.Any(m => m > 0x3FF))
            yield return "a move above 1023 (box records pack moves in 10 bits)";
        if (after.Species is 0 or > RecordPlausibility.CfruSpeciesCap)
            yield return $"Species (1 to {RecordPlausibility.CfruSpeciesCap})";
    }
}
