using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Editing;

namespace PKHaX.Hacks.Records;

public sealed class RecordEncodeException(string message) : Exception(message);

public readonly record struct FieldBits(int Offset, int Bit, int Width)
{
    public int End => (Offset * 8) + Bit + Width;
}

public sealed record EncodedField(EditFieldSpec Spec, IReadOnlyList<FieldBits> Bits)
{
    public string Field => Spec.Field;

    public static EncodedField Of(string field, EditFieldKind kind, int min, int max, params FieldBits[] bits) => new(new EditFieldSpec(field, kind, min, max, null), bits);

    public static byte[] Mask(int length, IEnumerable<FieldBits> runs)
    {
        var mask = new byte[length];
        foreach (var run in runs)
        {
            for (var i = 0; i < run.Width; i++)
            {
                var bit = (run.Offset * 8) + run.Bit + i;
                if (bit >= 0 && bit / 8 < length)
                    mask[bit / 8] |= (byte)(1 << (bit % 8));
            }
        }
        return mask;
    }
}

public static class Pk3CfruPlainEncoder
{
    private const int G = Gen3Record.SecureOffset;
    private const int A = G + Gen3Record.SubstructSize;
    private const int E = A + Gen3Record.SubstructSize;
    private const int M = E + Gen3Record.SubstructSize;
    private const int Tail = Gen3Record.StoredSize;

    public static IReadOnlyList<EncodedField> Fields { get; } =
    [
        EncodedField.Of("Species", EditFieldKind.SpeciesId, 1, RecordPlausibility.CfruSpeciesCap, new FieldBits(G, 0, 16)),
        EncodedField.Of("HeldItem", EditFieldKind.ItemId, 0, 0xFFFF, new FieldBits(G + 2, 0, 16)),
        EncodedField.Of("Exp", EditFieldKind.Number, 0, int.MaxValue, new FieldBits(G + 4, 0, 32)),
        EncodedField.Of("Friendship", EditFieldKind.Number, 0, 255, new FieldBits(G + 9, 0, 8)),
        EncodedField.Of("Ball", EditFieldKind.Number, 0, 255, new FieldBits(G + 10, 0, 8)),
        EncodedField.Of("Moves", EditFieldKind.MoveId, 0, 0xFFFF, new FieldBits(A, 0, 64)),
        EncodedField.Of("Evs", EditFieldKind.Number, 0, 255, new FieldBits(E, 0, 48)),
        EncodedField.Of("Ivs", EditFieldKind.Number, 0, 31, new FieldBits(M + 4, 0, 30)),
        EncodedField.Of("IsEgg", EditFieldKind.Flag, 0, 1, new FieldBits(M + 4, 30, 1), new FieldBits(0x13, 2, 1)),
        EncodedField.Of("HiddenAbility", EditFieldKind.Flag, 0, 1, new FieldBits(M + 4, 31, 1)),
        EncodedField.Of(EditableFields.Level, EditFieldKind.Number, 1, RecordPlausibility.MaxLevel, new FieldBits(Tail + 4, 0, 8)),
        EncodedField.Of(EditFieldSpecs.Nickname, EditFieldKind.Text, 1, NicknameLength, new FieldBits(Nick, 0, NicknameLength * 8)),
    ];

    public const int Nick = 0x08;
    public const int NicknameLength = 10;

    public static IReadOnlyList<FieldBits> Derived { get; } = [];

    public static byte[] Plain(ReadOnlySpan<byte> record) => record.ToArray();

    public static byte[] Encode(HackMon edited)
    {
        if (edited.Codec.Id != RecordCodecId.Pk3CfruPlain || edited.Shape != RecordShape.Party || edited.Raw.Length != Gen3Record.PartySize)
            throw new RecordEncodeException($"the CFRU party encoder takes a {Gen3Record.PartySize}-byte pk3-cfru-plain party record, got {edited.Codec} {edited.Shape} of {edited.Raw.Length} bytes");
        var codec = RecordCodecs.For(RecordCodecId.Pk3CfruPlain);
        var before = codec.Decode(edited.Raw.Span, out var why) ?? throw new RecordEncodeException($"the original record does not decode: {why}");
        var refused = Unsupported(before, edited).ToList();
        if (refused.Count > 0)
            throw new RecordEncodeException($"these fields cannot be written into a CFRU party record yet: {string.Join(", ", refused)}");
        var bytes = edited.Raw.ToArray();
        var span = bytes.AsSpan();
        BinaryPrimitives.WriteUInt16LittleEndian(span[G..], edited.Species);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(G + 2)..], edited.HeldItem);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(G + 4)..], edited.Exp);
        if (edited.PpUps is { } ups)
            span[G + 8] = PackPpUps(ups);
        if (edited.Friendship is { } friendship)
            span[G + 9] = friendship;
        if (edited.Ball is { } ball)
            span[G + 10] = ball;
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(span[(A + (i * 2))..], edited.Moves[i]);
        if (edited.Pp is { } pp)
        {
            for (var i = 0; i < 4; i++)
                span[A + 8 + i] = pp[i];
        }
        if (edited.Nickname != before.Nickname)
            Gen3Record.EncodeText(edited.Nickname, edited.Language, NicknameLength).CopyTo(span[Nick..]);
        WriteEvs(span[E..], edited.Evs);
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(span[(M + 4)..]);
        if (edited.Ivs is { } ivs)
            ivWord = (ivWord & 0xC000_0000) | IvBits(ivs);
        ivWord = Bit(ivWord, 30, edited.IsEgg);
        if (edited.HiddenAbility is { } hidden)
            ivWord = Bit(ivWord, 31, hidden);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(M + 4)..], ivWord);
        span[0x13] = (byte)((span[0x13] & ~4) | (edited.IsEgg ? 4 : 0));
        if (edited.Party is { } party)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[Tail..], party.Status);
            span[Tail + 4] = party.Level;
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 6)..], (ushort)party.CurrentHp);
            var s = party.Stats;
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 8)..], (ushort)s.Hp);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 10)..], (ushort)s.Atk);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 12)..], (ushort)s.Def);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 14)..], (ushort)s.Spe);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 16)..], (ushort)s.SpA);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 18)..], (ushort)s.SpD);
        }
        if (codec.Decode(bytes, out why) is not { } after)
            throw new RecordEncodeException($"the edited record would not decode as a CFRU party record: {why}");
        if (after.Nickname != edited.Nickname)
            throw new RecordEncodeException($"the edited record would read the nickname \"{after.Nickname}\", not \"{edited.Nickname}\"");
        return bytes;
    }

    public static IEnumerable<string> Unsupported(HackMon before, HackMon after)
    {
        if (before.Pid != after.Pid)
            yield return "Pid";
        if (before.Tid16 != after.Tid16 || before.Sid16 != after.Sid16)
            yield return "Tid/Sid";
        if (Gen3Record.NicknameChange(before, after, NicknameLength) is { } nickname)
            yield return nickname;
        if (before.OtName != after.OtName)
            yield return "OtName";
        if (before.Language != after.Language)
            yield return "Language";
        if (before.Nature != after.Nature || before.PidNature != after.PidNature)
            yield return "Nature";
        if (before.Ability != after.Ability || before.AbilitySlot != after.AbilitySlot)
            yield return "Ability";
        if (before.Form != after.Form)
            yield return "Form";
        if (before.Level != after.Level && before.Party?.Level == after.Party?.Level)
            yield return "Level (set Party.Level)";
        if (before.IsShiny != after.IsShiny || before.ShinyXor != after.ShinyXor)
            yield return "shininess (it follows the PID)";
        if (before.CatchRate != after.CatchRate)
            yield return "CatchRate";
        if (((before.HeaderFlags ?? 0) & ~4) != ((after.HeaderFlags ?? 0) & ~4))
            yield return "header flags other than the egg bit";
        if (before.ShinyModifier != after.ShinyModifier || before.TeraType != after.TeraType || before.HiddenNatureModifier != after.HiddenNatureModifier)
            yield return "expansion-only fields";
        if ((before.Pp is null) != (after.Pp is null) || (before.PpUps is null) != (after.PpUps is null) || (before.Ivs is null) != (after.Ivs is null) || (before.Party is null) != (after.Party is null))
            yield return "a field the record has became empty, or the reverse";
        if (after.Moves.Count != 4 || after.Pp is { Count: not 4 } || after.PpUps is { Count: not 4 } || after.PpUps?.Any(u => u > 3) == true)
            yield return "Moves/Pp/PpUps need 4 entries (PP Ups 0..3)";
        if (after.Evs.Hp > 255 || after.Evs.Atk > 255 || after.Evs.Def > 255 || after.Evs.SpA > 255 || after.Evs.SpD > 255 || after.Evs.Spe > 255 || Enumerable.Range(0, 6).Any(i => after.Evs[i] < 0))
            yield return "Evs (0..255 each)";
        if (after.Ivs is { } ivs && Enumerable.Range(0, 6).Any(i => ivs[i] is < 0 or > 31))
            yield return "Ivs (0..31 each)";
    }

    private static byte PackPpUps(IReadOnlyList<byte> ups)
    {
        var b = 0;
        for (var i = 0; i < 4; i++)
            b |= (ups[i] & 3) << (2 * i);
        return (byte)b;
    }

    private static void WriteEvs(Span<byte> e, StatSet evs)
    {
        e[0] = (byte)evs.Hp;
        e[1] = (byte)evs.Atk;
        e[2] = (byte)evs.Def;
        e[3] = (byte)evs.Spe;
        e[4] = (byte)evs.SpA;
        e[5] = (byte)evs.SpD;
    }

    private static uint IvBits(StatSet ivs) =>
        (uint)ivs.Hp | ((uint)ivs.Atk << 5) | ((uint)ivs.Def << 10) | ((uint)ivs.Spe << 15) | ((uint)ivs.SpA << 20) | ((uint)ivs.SpD << 25);

    private static uint Bit(uint word, int bit, bool on) => on ? word | (1u << bit) : word & ~(1u << bit);
}
