using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Editing;

namespace PKHaX.Hacks.Records;

public static class Pk3SoulGold96Encoder
{
    private const int G = 0x20;
    private const int A = 0x2C;
    private const int E = 0x38;
    private const int M = 0x44;
    public const int MaxPp = 64;
    public const int HpLost = 0x1E;
    public const int HpLostMask = 0x3FFF;
    public const int PartyCurrentHp = 0x52;

    public static IReadOnlyList<EncodedField> Fields { get; } =
    [
        EncodedField.Of("Species", EditFieldKind.SpeciesId, 1, 0x7FF, new FieldBits(G, 0, 11)),
        EncodedField.Of("HeldItem", EditFieldKind.ItemId, 0, 0x3FF, new FieldBits(G + 2, 0, 10)),
        EncodedField.Of("Exp", EditFieldKind.Number, 0, 0x1FFFFF, new FieldBits(G + 4, 0, 21)),
        EncodedField.Of("Friendship", EditFieldKind.Number, 0, 255, new FieldBits(G + 11, 0, 8)),
        EncodedField.Of("Moves", EditFieldKind.MoveId, 0, 0x7FF, new FieldBits(A, 0, 11), new FieldBits(A + 2, 0, 11), new FieldBits(A + 4, 0, 11), new FieldBits(A + 6, 0, 11)),
        EncodedField.Of("Evs", EditFieldKind.Number, 0, 255, new FieldBits(E, 0, 48)),
        EncodedField.Of("Ivs", EditFieldKind.Number, 0, 31, new FieldBits(M + 4, 0, 30)),
        EncodedField.Of("IsEgg", EditFieldKind.Flag, 0, 1, new FieldBits(M + 4, 30, 1), new FieldBits(0x15, 2, 1)),
        EncodedField.Of(EditFieldSpecs.Nature, EditFieldKind.Nature, 0, 24, new FieldBits(0x14, 3, 5)),
        EncodedField.Of(EditFieldSpecs.Nickname, EditFieldKind.Text, 1, NicknameLength, new FieldBits(Nick, 0, NicknameLength * 8)),
    ];

    public static EncodedField PartyLevel { get; } = EncodedField.Of(EditableFields.Level, EditFieldKind.Number, 1, RecordPlausibility.MaxLevel, new FieldBits(Tail + 4, 0, 8));

    public const int Nick = 0x08;
    public const int NicknameLength = 12;
    public const int Tail = 0x4C;

    public static IReadOnlyList<FieldBits> Derived { get; } = [];

    public static byte[] Plain(ReadOnlySpan<byte> record) => record.ToArray();

    public static byte[] Encode(HackMon edited)
    {
        if (edited.Codec.Id != RecordCodecId.Pk3SoulGold96 || edited.Raw.Length is not (Pk3SoulGold96Codec.Party or Pk3SoulGold96Codec.Box))
            throw new RecordEncodeException($"the SoulGold encoder takes a pk3-soulgold96 record of {Pk3SoulGold96Codec.Box} or {Pk3SoulGold96Codec.Party} bytes, got {edited.Codec} of {edited.Raw.Length}");
        var codec = RecordCodecs.For(RecordCodecId.Pk3SoulGold96);
        var before = codec.Decode(edited.Raw.Span, out var why) ?? throw new RecordEncodeException($"the original record does not decode: {why}");
        var refused = Unsupported(before, edited).ToList();
        if (refused.Count > 0)
            throw new RecordEncodeException($"these fields cannot be written into a SoulGold record yet: {string.Join(", ", refused)}");
        var bytes = edited.Raw.ToArray();
        var s = bytes.AsSpan();
        var g0 = BinaryPrimitives.ReadUInt16LittleEndian(s[G..]);
        var tera = edited.TeraType ?? (byte)(g0 >> 11);
        BinaryPrimitives.WriteUInt16LittleEndian(s[G..], (ushort)((edited.Species & 0x7FF) | (tera << 11)));
        Keep16(s[(G + 2)..], edited.HeldItem, 0x3FF);
        Keep32(s[(G + 4)..], edited.Exp, 0x1FFFFF);
        if (edited.PpUps is { } ups)
            s[G + 8] = (byte)((ups[0] & 3) | ((ups[1] & 3) << 2) | ((ups[2] & 3) << 4) | ((ups[3] & 3) << 6));
        if (edited.Friendship is { } friendship)
            s[G + 11] = friendship;
        for (var i = 0; i < 4; i++)
            Keep16(s[(A + (i * 2))..], edited.Moves[i], 0x7FF);
        if (edited.Pp is { } pp)
        {
            for (var i = 0; i < 4; i++)
                s[A + 8 + i] = (byte)((s[A + 8 + i] & 0x80) | (pp[i] & 0x7F));
        }
        if (edited.Nickname != before.Nickname)
            Gen3Record.EncodeText(edited.Nickname, edited.Language, NicknameLength).CopyTo(s[Nick..]);
        s[E] = (byte)edited.Evs.Hp;
        s[E + 1] = (byte)edited.Evs.Atk;
        s[E + 2] = (byte)edited.Evs.Def;
        s[E + 3] = (byte)edited.Evs.Spe;
        s[E + 4] = (byte)edited.Evs.SpA;
        s[E + 5] = (byte)edited.Evs.SpD;
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(s[(M + 4)..]);
        if (edited.Ivs is { } ivs)
            ivWord = (ivWord & 0xC000_0000) | (uint)ivs.Hp | ((uint)ivs.Atk << 5) | ((uint)ivs.Def << 10) | ((uint)ivs.Spe << 15) | ((uint)ivs.SpA << 20) | ((uint)ivs.SpD << 25);
        ivWord = edited.IsEgg ? ivWord | (1u << 30) : ivWord & ~(1u << 30);
        BinaryPrimitives.WriteUInt32LittleEndian(s[(M + 4)..], ivWord);
        var hidden = edited.Nature is { } nature && edited.PidNature is { } pidNature ? (byte)(nature ^ pidNature) : (byte)(s[0x14] >> 3);
        s[0x14] = (byte)((s[0x14] & 7) | (hidden << 3));
        s[0x15] = (byte)((s[0x15] & ~4) | (edited.IsEgg ? 4 : 0));
        if (edited.Party is { } party && before.Party is { } was)
        {
            if (party.Level != was.Level)
                s[Tail + 4] = party.Level;
            if (party.Stats != was.Stats)
                Pk3xEncoder.WriteStats(s[(Tail + 8)..], party.Stats);
            if (party.CurrentHp != was.CurrentHp || party.Stats.Hp != was.Stats.Hp)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(s[PartyCurrentHp..], (ushort)party.CurrentHp);
                var keep = BinaryPrimitives.ReadUInt16LittleEndian(s[HpLost..]) & ~HpLostMask;
                BinaryPrimitives.WriteUInt16LittleEndian(s[HpLost..], (ushort)(keep | ((party.Stats.Hp - party.CurrentHp) & HpLostMask)));
            }
        }
        if (codec.Decode(bytes, out why) is not { } after)
            throw new RecordEncodeException($"the edited record would not decode as pk3-soulgold96: {why}");
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
        if (before.OtName != after.OtName || before.Language != after.Language)
            yield return "OtName/Language";
        if (before.PidNature != after.PidNature)
            yield return "PidNature (it follows the PID)";
        if (after.Nature is { } n && after.PidNature is { } p && (n >= 25 || (n ^ p) >= 32))
            yield return "Nature (0-24)";
        if (before.HiddenNatureModifier != after.HiddenNatureModifier && !(after.Nature is { } an && after.PidNature is { } ap0 && after.HiddenNatureModifier == (an ^ ap0)))
            yield return "HiddenNatureModifier (set Nature; the modifier must equal Nature XOR the PID nature)";
        if (before.National != after.National)
            yield return "National (it follows Species)";
        foreach (var party in Pk3xEncoder.PartyProblems(before, after))
            yield return party;
        if (before.Party is { } hb && after.Party is { } ha && (hb.CurrentHp != ha.CurrentHp || hb.Stats.Hp != ha.Stats.Hp)
            && ha.CurrentHp >= 0 && ha.CurrentHp <= ha.Stats.Hp
            && (before.Raw.Length < HpLost + 2 || (BinaryPrimitives.ReadUInt16LittleEndian(before.Raw.Span[HpLost..]) & HpLostMask) != hb.Stats.Hp - hb.CurrentHp))
            yield return "current or max HP (this record's low 14 bits at 0x1E are not max HP minus current HP, the box-side HP copy measured in result 13n12, so it is not written)";
        if (before.IsShiny != after.IsShiny || before.ShinyXor != after.ShinyXor || before.ShinyModifier != after.ShinyModifier)
            yield return "shininess (no shiny modifier is measured in this record)";
        if (before.Form != after.Form || before.CatchRate != after.CatchRate || before.Ability != after.Ability || before.AbilitySlot != after.AbilitySlot || before.HiddenAbility != after.HiddenAbility || before.Ball != after.Ball)
            yield return "Form/CatchRate/Ability/Ball (not located in this record)";
        if (((before.HeaderFlags ?? 0) & ~4) != ((after.HeaderFlags ?? 0) & ~4) || before.Shape != after.Shape || before.Codec != after.Codec)
            yield return "header flags other than the egg bit, shape or codec";
        if (after.Species is 0 or > 0x7FF || after.HeldItem > 0x3FF || after.Exp > 0x1FFFFF || after.TeraType is > 31)
            yield return "a field above its bit width (species 11, item 10, EXP 21, tera 5 bits)";
        if (after.Moves.Count != 4 || after.Moves.Any(m => m > 0x7FF) || after.Pp is { Count: not 4 } || after.PpUps is { Count: not 4 } || after.PpUps?.Any(u => u > 3) == true)
            yield return "Moves/Pp/PpUps (4 entries; move 11 bits, PP Ups 0-3)";
        if (after.Pp?.Any(x => x > MaxPp) == true)
            yield return $"Pp above {MaxPp} (the decoder's plausibility cap)";
        if (Enumerable.Range(0, 6).Any(i => after.Evs[i] is < 0 or > 255) || (after.Ivs is { } ivs && Enumerable.Range(0, 6).Any(i => ivs[i] is < 0 or > 31)))
            yield return "Evs (0-255) / Ivs (0-31)";
        if ((before.Ivs is null) != (after.Ivs is null) || (before.Pp is null) != (after.Pp is null) || (before.PpUps is null) != (after.PpUps is null) || (before.Party is null) != (after.Party is null))
            yield return "a field the record has became empty, or the reverse";
    }

    private static void Keep16(Span<byte> at, int value, int mask) =>
        BinaryPrimitives.WriteUInt16LittleEndian(at, (ushort)((BinaryPrimitives.ReadUInt16LittleEndian(at) & ~mask) | (value & mask)));

    private static void Keep32(Span<byte> at, uint value, uint mask) =>
        BinaryPrimitives.WriteUInt32LittleEndian(at, (BinaryPrimitives.ReadUInt32LittleEndian(at) & ~mask) | (value & mask));
}
