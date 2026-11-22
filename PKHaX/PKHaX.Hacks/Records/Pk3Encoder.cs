using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Editing;

namespace PKHaX.Hacks.Records;

public static class Pk3Encoder
{
    private const int G = Gen3Record.SecureOffset;
    private const int A = G + Gen3Record.SubstructSize;
    private const int E = A + Gen3Record.SubstructSize;
    private const int M = E + Gen3Record.SubstructSize;
    private const int Tail = Gen3Record.StoredSize;
    private const int ChecksumAt = 0x1C;
    private const int FlagsAt = 0x13;
    private const int EggFlag = 4;

    public const int MaxBall = 15;
    public const int MaxAbilitySlot = 1;

    public const string Source = "pokefirered include/pokemon.h:8-16 (PokemonSubstruct0: species, heldItem, experience, ppBonuses, friendship), :18-22 (PokemonSubstruct1: moves, pp), :24-31 (PokemonSubstruct2: EVs), :40-58 (PokemonSubstruct3: pokeball bits 11-14 of the origins word, IV word with isEgg bit 30 and abilityNum bit 31), :105-126 (struct BoxPokemon: checksum at 0x1C over the 48 decrypted bytes, isEgg flag at 0x13 bit 2), :128-141 (struct Pokemon party tail); src/pokemon.c:2069 CalculateBoxMonChecksum, :2797 EncryptBoxMon (key = personality ^ otId), :2863 GetSubstruct (order by personality % 24)";

    public static IReadOnlyList<EncodedField> Fields { get; } =
    [
        EncodedField.Of("Species", EditFieldKind.SpeciesId, 1, 0xFFFF, new FieldBits(G, 0, 16)),
        EncodedField.Of("HeldItem", EditFieldKind.ItemId, 0, 0xFFFF, new FieldBits(G + 2, 0, 16)),
        EncodedField.Of("Exp", EditFieldKind.Number, 0, int.MaxValue, new FieldBits(G + 4, 0, 32)),
        EncodedField.Of("Friendship", EditFieldKind.Number, 0, 255, new FieldBits(G + 9, 0, 8)),
        EncodedField.Of("Ball", EditFieldKind.Number, 0, MaxBall, new FieldBits(M + 2, 11, 4)),
        EncodedField.Of("Moves", EditFieldKind.MoveId, 0, 0xFFFF, new FieldBits(A, 0, 64)),
        EncodedField.Of("Evs", EditFieldKind.Number, 0, 255, new FieldBits(E, 0, 48)),
        EncodedField.Of("Ivs", EditFieldKind.Number, 0, 31, new FieldBits(M + 4, 0, 30)),
        EncodedField.Of("IsEgg", EditFieldKind.Flag, 0, 1, new FieldBits(M + 4, 30, 1), new FieldBits(FlagsAt, 2, 1)),
        EncodedField.Of(EditFieldSpecs.AbilitySlot, EditFieldKind.AbilitySlot, 0, MaxAbilitySlot, new FieldBits(M + 4, 31, 1)),
        EncodedField.Of(EditableFields.Level, EditFieldKind.Number, 1, RecordPlausibility.MaxLevel, new FieldBits(Tail + 4, 0, 8)),
    ];

    public static IReadOnlyList<FieldBits> Derived { get; } = [new FieldBits(ChecksumAt, 0, 16)];

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
        ArgumentNullException.ThrowIfNull(edited);
        if (edited.Codec.Id != RecordCodecId.Pk3 || edited.Raw.Length is not (Gen3Record.PartySize or Gen3Record.StoredSize))
            throw new RecordEncodeException($"the retail encoder takes a pk3 record of {Gen3Record.StoredSize} or {Gen3Record.PartySize} bytes, got {edited.Codec} of {edited.Raw.Length}");
        var codec = RecordCodecs.For(RecordCodecId.Pk3);
        var before = codec.Decode(edited.Raw.Span, out var why) ?? throw new RecordEncodeException($"the original record does not decode: {why}");
        var refused = Unsupported(before, edited).ToList();
        if (refused.Count > 0)
            throw new RecordEncodeException($"these fields cannot be written into a retail pk3 record yet: {string.Join(", ", refused)}");
        var bytes = edited.Raw.ToArray();
        var span = bytes.AsSpan();
        Span<byte> s = stackalloc byte[Gen3Record.SecureSize];
        Gen3Record.Decrypt(span, s);
        const int g = G - Gen3Record.SecureOffset;
        const int a = A - Gen3Record.SecureOffset;
        const int e = E - Gen3Record.SecureOffset;
        const int m = M - Gen3Record.SecureOffset;
        BinaryPrimitives.WriteUInt16LittleEndian(s[g..], edited.Species);
        BinaryPrimitives.WriteUInt16LittleEndian(s[(g + 2)..], edited.HeldItem);
        BinaryPrimitives.WriteUInt32LittleEndian(s[(g + 4)..], edited.Exp);
        if (edited.PpUps is { } ups)
            s[g + 8] = (byte)((ups[0] & 3) | ((ups[1] & 3) << 2) | ((ups[2] & 3) << 4) | ((ups[3] & 3) << 6));
        if (edited.Friendship is { } friendship)
            s[g + 9] = friendship;
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(s[(a + (i * 2))..], edited.Moves[i]);
        if (edited.Pp is { } pp)
        {
            for (var i = 0; i < 4; i++)
                s[a + 8 + i] = pp[i];
        }
        s[e] = (byte)edited.Evs.Hp;
        s[e + 1] = (byte)edited.Evs.Atk;
        s[e + 2] = (byte)edited.Evs.Def;
        s[e + 3] = (byte)edited.Evs.Spe;
        s[e + 4] = (byte)edited.Evs.SpA;
        s[e + 5] = (byte)edited.Evs.SpD;
        if (edited.Ball is { } ball)
        {
            var origins = BinaryPrimitives.ReadUInt16LittleEndian(s[(m + 2)..]);
            BinaryPrimitives.WriteUInt16LittleEndian(s[(m + 2)..], (ushort)((origins & ~(0xF << 11)) | ((ball & 0xF) << 11)));
        }
        var ivWord = BinaryPrimitives.ReadUInt32LittleEndian(s[(m + 4)..]);
        if (edited.Ivs is { } ivs)
            ivWord = (ivWord & 0xC000_0000) | (uint)ivs.Hp | ((uint)ivs.Atk << 5) | ((uint)ivs.Def << 10) | ((uint)ivs.Spe << 15) | ((uint)ivs.SpA << 20) | ((uint)ivs.SpD << 25);
        ivWord = edited.IsEgg ? ivWord | (1u << 30) : ivWord & ~(1u << 30);
        if (edited.AbilitySlot is { } slot)
            ivWord = slot != 0 ? ivWord | (1u << 31) : ivWord & ~(1u << 31);
        BinaryPrimitives.WriteUInt32LittleEndian(s[(m + 4)..], ivWord);
        if (edited.IsEgg != before.IsEgg)
            span[FlagsAt] = (byte)((span[FlagsAt] & ~EggFlag) | (edited.IsEgg ? EggFlag : 0));
        BinaryPrimitives.WriteUInt16LittleEndian(span[ChecksumAt..], Gen3Record.Sum(s));
        Gen3Record.Encrypt(s, span);
        if (edited.Party is { } party && span.Length == Gen3Record.PartySize)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(span[Tail..], party.Status);
            span[Tail + 4] = party.Level;
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 6)..], (ushort)party.CurrentHp);
            var stats = party.Stats;
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 8)..], (ushort)stats.Hp);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 10)..], (ushort)stats.Atk);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 12)..], (ushort)stats.Def);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 14)..], (ushort)stats.Spe);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 16)..], (ushort)stats.SpA);
            BinaryPrimitives.WriteUInt16LittleEndian(span[(Tail + 18)..], (ushort)stats.SpD);
        }
        var after = codec.Decode(bytes, out why) ?? throw new RecordEncodeException($"the edited record would not decode as a retail pk3 record: {why}");
        if (after.Pid != before.Pid || after.Tid16 != before.Tid16 || after.Sid16 != before.Sid16 || after.Species != edited.Species || after.HeldItem != edited.HeldItem || !after.Moves.SequenceEqual(edited.Moves))
            throw new RecordEncodeException("the edited record does not read back as the edit");
        return bytes;
    }

    public static IEnumerable<string> Unsupported(HackMon before, HackMon after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (before.Pid != after.Pid)
            yield return "Pid (the substructure order and the key follow it)";
        if (before.Tid16 != after.Tid16 || before.Sid16 != after.Sid16)
            yield return "Tid/Sid (the key follows the OT id)";
        if (before.Nickname != after.Nickname)
            yield return "Nickname";
        if (before.OtName != after.OtName)
            yield return "OtName";
        if (before.Language != after.Language)
            yield return "Language";
        if (before.Nature != after.Nature || before.PidNature != after.PidNature)
            yield return "Nature (it follows the PID)";
        if (before.Form != after.Form || before.CatchRate != after.CatchRate || before.Ability != after.Ability || before.HiddenAbility != after.HiddenAbility)
            yield return "Form/CatchRate/Ability/HiddenAbility (set AbilitySlot)";
        if (before.IsShiny != after.IsShiny || before.ShinyXor != after.ShinyXor)
            yield return "shininess (it follows the PID)";
        if (before.ShinyModifier != after.ShinyModifier || before.TeraType != after.TeraType || before.HiddenNatureModifier != after.HiddenNatureModifier)
            yield return "expansion-only fields";
        if (((before.HeaderFlags ?? 0) & ~EggFlag) != ((after.HeaderFlags ?? 0) & ~EggFlag))
            yield return "header flags other than the egg bit";
        if (before.Shape != after.Shape || before.Codec != after.Codec)
            yield return "record shape or codec";
        if (before.Level != after.Level && before.Party?.Level == after.Party?.Level)
            yield return "Level (set Party.Level)";
        if ((before.Pp is null) != (after.Pp is null) || (before.PpUps is null) != (after.PpUps is null) || (before.Ivs is null) != (after.Ivs is null) || (before.Party is null) != (after.Party is null)
            || (before.Ball is null) != (after.Ball is null) || (before.AbilitySlot is null) != (after.AbilitySlot is null) || (before.Friendship is null) != (after.Friendship is null))
            yield return "a field the record has became empty, or the reverse";
        if (after.Species == 0)
            yield return "Species 0 (an empty slot is not written)";
        if (after.Ball is > MaxBall || after.AbilitySlot is > MaxAbilitySlot)
            yield return $"a field above its bit width (ball 0-{MaxBall}, ability slot 0-{MaxAbilitySlot})";
        if (after.Moves.Count != 4 || after.Pp is { Count: not 4 } || after.PpUps is { Count: not 4 } || after.PpUps?.Any(u => u > 3) == true)
            yield return "Moves/Pp/PpUps need 4 entries (PP Ups 0-3)";
        if (Enumerable.Range(0, 6).Any(i => after.Evs[i] is < 0 or > 255) || (after.Ivs is { } ivs && Enumerable.Range(0, 6).Any(i => ivs[i] is < 0 or > 31)))
            yield return "Evs (0-255) / Ivs (0-31)";
        if (before.Party is { } bp && after.Party is { } ap && bp.Status != ap.Status)
            yield return "Party status";
        if (after.Party is { } party && (party.Level is < 1 or > RecordPlausibility.MaxLevel || party.CurrentHp < 0 || party.CurrentHp > party.Stats.Hp))
            yield return $"Party level (1-{RecordPlausibility.MaxLevel}) or current HP (0 to max HP)";
        if (after.Party is { } edited && edited.Stats != before.Party?.Stats && (edited.Stats.Hp < 1 || Enumerable.Range(0, 6).Any(i => edited.Stats[i] is < 0 or > RecordPlausibility.MaxStat)))
            yield return $"Party stats {edited.Stats} (max HP at least 1, each stat 0 to {RecordPlausibility.MaxStat})";
    }
}
