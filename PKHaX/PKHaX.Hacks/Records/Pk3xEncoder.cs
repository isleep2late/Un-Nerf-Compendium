using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Editing;

namespace PKHaX.Hacks.Records;

public static class Pk3xEncoder
{
    private const int G = 0;
    private const int A = Gen3Record.SubstructSize;
    private const int E = 2 * Gen3Record.SubstructSize;
    private const int M = 3 * Gen3Record.SubstructSize;
    private const int Tail = Gen3Record.StoredSize;
    private const int P = Gen3Record.SecureOffset;

    public static IReadOnlyList<EncodedField> Fields { get; } =
    [
        EncodedField.Of("Species", EditFieldKind.SpeciesId, 1, 0x7FF, new FieldBits(P + G, 0, 11)),
        EncodedField.Of("HeldItem", EditFieldKind.ItemId, 0, 0x3FF, new FieldBits(P + G + 2, 0, 10)),
        EncodedField.Of("Exp", EditFieldKind.Number, 0, 0x1FFFFF, new FieldBits(P + G + 4, 0, 21)),
        EncodedField.Of("Friendship", EditFieldKind.Number, 0, 255, new FieldBits(P + G + 9, 0, 8)),
        EncodedField.Of("Ball", EditFieldKind.Number, 0, 0x3F, new FieldBits(P + G + 10, 0, 6)),
        EncodedField.Of("Moves", EditFieldKind.MoveId, 0, 0x7FF, new FieldBits(P + A, 0, 11), new FieldBits(P + A + 2, 0, 11), new FieldBits(P + A + 4, 0, 11), new FieldBits(P + A + 6, 0, 11)),
        EncodedField.Of("Evs", EditFieldKind.Number, 0, 255, new FieldBits(P + E, 0, 48)),
        EncodedField.Of("Ivs", EditFieldKind.Number, 0, 31, new FieldBits(P + M + 4, 0, 30)),
        EncodedField.Of("IsEgg", EditFieldKind.Flag, 0, 1, new FieldBits(P + M + 4, 30, 1), new FieldBits(0x13, 2, 1)),
        EncodedField.Of(EditFieldSpecs.AbilitySlot, EditFieldKind.AbilitySlot, 0, 2, new FieldBits(P + M + 8, 29, 2)),
        EncodedField.Of(EditFieldSpecs.Nature, EditFieldKind.Nature, 0, 24, new FieldBits(0x12, 3, 5)),
        EncodedField.Of(EditFieldSpecs.Nickname, EditFieldKind.Text, 1, NicknameLength, new FieldBits(Nick, 0, 80), new FieldBits(P + G + 4, 21, 8), new FieldBits(P + G + 10, 6, 8)),
    ];

    public static EncodedField PartyLevel { get; } = EncodedField.Of(EditableFields.Level, EditFieldKind.Number, 1, RecordPlausibility.MaxLevel, new FieldBits(Tail + 4, 0, 8));

    public const int Nick = 0x08;
    public const int NicknameLength = 12;
    public const int HpLost = 0x1E;
    public const int HpLostMask = 0x3FFF;

    public static IReadOnlyList<FieldBits> Derived { get; } = [new FieldBits(0x1C, 0, 16)];

    public static byte[] Plain(ReadOnlySpan<byte> record)
    {
        var plain = record.ToArray();
        if (record.Length < Gen3Record.StoredSize)
            return plain;
        Span<byte> s = stackalloc byte[Gen3Record.SecureSize];
        Gen3Record.Decrypt(record, s);
        s.CopyTo(plain.AsSpan(Gen3Record.SecureOffset));
        return plain;
    }

    public static byte[] Encode(HackMon edited)
    {
        if (edited.Codec.Id != RecordCodecId.Pk3x || edited.Raw.Length is not (Gen3Record.PartySize or Gen3Record.StoredSize))
            throw new RecordEncodeException($"the expansion encoder takes a pk3x record of {Gen3Record.StoredSize} or {Gen3Record.PartySize} bytes, got {edited.Codec} of {edited.Raw.Length}");
        var codec = RecordCodecs.For(RecordCodecId.Pk3x, edited.Codec.Version);
        var before = codec.Decode(edited.Raw.Span, out var why) ?? throw new RecordEncodeException($"the original record does not decode: {why}");
        var refused = Unsupported(before, edited).ToList();
        if (refused.Count > 0)
            throw new RecordEncodeException($"these fields cannot be written into an expansion record yet: {string.Join(", ", refused)}");
        var bytes = edited.Raw.ToArray();
        var span = bytes.AsSpan();
        Span<byte> s = stackalloc byte[Gen3Record.SecureSize];
        Gen3Record.Decrypt(span, s);
        var g0 = (ushort)((edited.Species & 0x7FF) | ((edited.TeraType ?? (byte)(BinaryPrimitives.ReadUInt16LittleEndian(s[G..]) >> 11)) << 11));
        BinaryPrimitives.WriteUInt16LittleEndian(s[G..], g0);
        Keep16(s[(G + 2)..], edited.HeldItem, 0x3FF);
        Keep32(s[(G + 4)..], edited.Exp, 0x1FFFFF);
        if (edited.PpUps is { } ups)
            s[G + 8] = (byte)((ups[0] & 3) | ((ups[1] & 3) << 2) | ((ups[2] & 3) << 4) | ((ups[3] & 3) << 6));
        if (edited.Friendship is { } friendship)
            s[G + 9] = friendship;
        if (edited.Ball is { } ball)
            Keep16(s[(G + 10)..], ball, 0x3F);
        for (var i = 0; i < 4; i++)
            Keep16(s[(A + (i * 2))..], edited.Moves[i], 0x7FF);
        if (edited.Pp is { } pp)
        {
            for (var i = 0; i < 4; i++)
                s[A + 8 + i] = (byte)((s[A + 8 + i] & 0x80) | (pp[i] & 0x7F));
        }
        if (edited.Nickname != before.Nickname)
            WriteNickname(span, s, edited.Nickname, edited.Language);
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
        if (edited.AbilitySlot is { } slot)
        {
            var ribbons = BinaryPrimitives.ReadUInt32LittleEndian(s[(M + 8)..]);
            BinaryPrimitives.WriteUInt32LittleEndian(s[(M + 8)..], (ribbons & ~(3u << 29)) | ((uint)(slot & 3) << 29));
        }
        var hidden = edited.Nature is { } nature && edited.PidNature is { } pidNature ? (byte)(nature ^ pidNature) : (byte)(span[0x12] >> 3);
        span[0x12] = (byte)((span[0x12] & 7) | (hidden << 3));
        span[0x13] = (byte)((span[0x13] & ~4) | (edited.IsEgg ? 4 : 0));
        var tail = BinaryPrimitives.ReadUInt16LittleEndian(span[0x1E..]);
        if (edited.ShinyModifier is { } shiny)
            tail = (ushort)(shiny ? tail | (1 << 14) : tail & ~(1 << 14));
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x1E..], tail);
        BinaryPrimitives.WriteUInt16LittleEndian(span[0x1C..], Gen3Record.Sum(s));
        Gen3Record.Encrypt(s, span);
        if (edited.Party is { } party && before.Party is { } was && span.Length == Gen3Record.PartySize)
        {
            if (party.Level != was.Level)
                span[Tail + 4] = party.Level;
            if (party.Stats != was.Stats)
                WriteStats(span[(Tail + 8)..], party.Stats);
            if (party.CurrentHp != was.CurrentHp || party.Stats.Hp != was.Stats.Hp)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 6)..], (ushort)party.CurrentHp);
                var lost = (ushort)Math.Max(0, party.Stats.Hp - party.CurrentHp);
                var hp = BinaryPrimitives.ReadUInt16LittleEndian(span[HpLost..]);
                BinaryPrimitives.WriteUInt16LittleEndian(span[HpLost..], (ushort)((hp & ~HpLostMask) | (lost & HpLostMask)));
            }
        }
        if (codec.Decode(bytes, out why) is not { } after)
            throw new RecordEncodeException($"the edited record would not decode as {edited.Codec}: {why}");
        if (after.Nickname != edited.Nickname)
            throw new RecordEncodeException($"the edited record would read the nickname \"{after.Nickname}\", not \"{edited.Nickname}\"");
        return bytes;
    }

    public static HackMon SetShiny(HackMon mon, bool shiny) => mon with { ShinyModifier = shiny };

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
        if (before.PidNature != after.PidNature)
            yield return "PidNature (it follows the PID)";
        if (after.Nature is { } n && after.PidNature is { } p && (n >= 25 || (n ^ p) >= 32))
            yield return "Nature (0-24)";
        if (before.HiddenNatureModifier != after.HiddenNatureModifier && before.Nature == after.Nature)
            yield return "HiddenNatureModifier (set Nature)";
        foreach (var party in PartyProblems(before, after))
            yield return party;
        if (before.Shape != after.Shape || before.Codec != after.Codec)
            yield return "record shape or codec";
        if (before.HiddenAbility != after.HiddenAbility)
            yield return "HiddenAbility (set AbilitySlot)";
        if (before.IsShiny != after.IsShiny || before.ShinyXor != after.ShinyXor)
            yield return "IsShiny (set ShinyModifier)";
        if (before.Form != after.Form || before.CatchRate != after.CatchRate || before.Ability != after.Ability)
            yield return "Form/CatchRate/Ability";
        if (((before.HeaderFlags ?? 0) & ~4) != ((after.HeaderFlags ?? 0) & ~4))
            yield return "header flags other than the egg bit";
        if (after.Species is 0 or > 0x7FF || after.HeldItem > 0x3FF || after.Exp > 0x1FFFFF || after.Ball is > 0x3F || after.TeraType is > 31 || after.AbilitySlot is > 2)
            yield return "a field above its bit width (species 11, item 10, EXP 21, ball 6, tera 5 bits; ability slot 0-2)";
        if (after.Moves.Count != 4 || after.Moves.Any(m => m > 0x7FF) || after.Pp is { Count: not 4 } || after.Pp?.Any(x => x > 0x7F) == true || after.PpUps is { Count: not 4 } || after.PpUps?.Any(u => u > 3) == true)
            yield return "Moves/Pp/PpUps (4 entries; move 11 bits, PP 7 bits, PP Ups 0-3)";
        if (Enumerable.Range(0, 6).Any(i => after.Evs[i] is < 0 or > 255) || (after.Ivs is { } ivs && Enumerable.Range(0, 6).Any(i => ivs[i] is < 0 or > 31)))
            yield return "Evs (0-255) / Ivs (0-31)";
        if ((before.Ivs is null) != (after.Ivs is null) || (before.Pp is null) != (after.Pp is null) || (before.PpUps is null) != (after.PpUps is null) || (before.Party is null) != (after.Party is null) || (before.ShinyModifier is null) != (after.ShinyModifier is null))
            yield return "a field the record has became empty, or the reverse";
    }

    internal static IEnumerable<string> PartyProblems(HackMon before, HackMon after)
    {
        if (after.Party is { } level ? after.Level != level.Level : before.Level != after.Level)
            yield return "Level (set it with the party level; a stored record follows EXP)";
        if (before.Party is not { } bp || after.Party is not { } ap)
            yield break;
        if (bp.Status != ap.Status)
            yield return "Party status";
        if (ap.Level is < 1 or > RecordPlausibility.MaxLevel)
            yield return $"Party level {ap.Level} (1 to {RecordPlausibility.MaxLevel})";
        if (ap.Stats.Hp < 1 || Enumerable.Range(0, 6).Any(i => ap.Stats[i] is < 0 or > RecordPlausibility.MaxStat))
            yield return $"Party stats {ap.Stats} (max HP at least 1, each stat 0 to {RecordPlausibility.MaxStat})";
        if (ap.CurrentHp < 0 || ap.CurrentHp > ap.Stats.Hp)
            yield return $"current HP {ap.CurrentHp} (0 to max HP {ap.Stats.Hp})";
    }

    internal static void WriteStats(Span<byte> at, StatSet stats)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(at, (ushort)stats.Hp);
        BinaryPrimitives.WriteUInt16LittleEndian(at[2..], (ushort)stats.Atk);
        BinaryPrimitives.WriteUInt16LittleEndian(at[4..], (ushort)stats.Def);
        BinaryPrimitives.WriteUInt16LittleEndian(at[6..], (ushort)stats.Spe);
        BinaryPrimitives.WriteUInt16LittleEndian(at[8..], (ushort)stats.SpA);
        BinaryPrimitives.WriteUInt16LittleEndian(at[10..], (ushort)stats.SpD);
    }

    private static void WriteNickname(Span<byte> record, Span<byte> s, string? nickname, int? language)
    {
        var text = Gen3Record.EncodeText(nickname, language, NicknameLength);
        text.AsSpan(0, 10).CopyTo(record[Nick..]);
        var expWord = BinaryPrimitives.ReadUInt32LittleEndian(s[(G + 4)..]);
        var ballWord = BinaryPrimitives.ReadUInt16LittleEndian(s[(G + 10)..]);
        var n11 = (byte)((expWord >> 21) & 0xFF);
        var n12 = (byte)((ballWord >> 6) & 0xFF);
        var length = nickname!.Length;
        if (length > 10)
        {
            n11 = text[10];
            n12 = text[11];
        }
        else if (length == 10 && (n11 != 0 || n12 != 0))
            n11 = 0xFF;
        else
            return;
        BinaryPrimitives.WriteUInt32LittleEndian(s[(G + 4)..], (expWord & ~(0xFFu << 21)) | ((uint)n11 << 21));
        BinaryPrimitives.WriteUInt16LittleEndian(s[(G + 10)..], (ushort)((ballWord & ~(0xFF << 6)) | (n12 << 6)));
    }

    private static void Keep16(Span<byte> at, int value, int mask) =>
        BinaryPrimitives.WriteUInt16LittleEndian(at, (ushort)((BinaryPrimitives.ReadUInt16LittleEndian(at) & ~mask) | (value & mask)));

    private static void Keep32(Span<byte> at, uint value, uint mask) =>
        BinaryPrimitives.WriteUInt32LittleEndian(at, (BinaryPrimitives.ReadUInt32LittleEndian(at) & ~mask) | (value & mask));
}
