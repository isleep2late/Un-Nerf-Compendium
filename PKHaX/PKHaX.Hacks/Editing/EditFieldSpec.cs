using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Records;

namespace PKHaX.Hacks.Editing;

public enum EditFieldKind
{
    SpeciesId,
    MoveId,
    ItemId,
    AbilitySlot,
    Nature,
    Number,
    Text,
    Flag,
}

public sealed record EditFieldSpec(string Field, EditFieldKind Kind, int Min, int Max, string? Why)
{
    public bool Editable => Why is null;

    public string Range => Kind switch
    {
        EditFieldKind.Flag => "yes or no",
        EditFieldKind.Text => $"up to {Max} characters",
        _ => $"{Min} to {Max}",
    };
}

public static class EditFieldSpecs
{
    public const string Nickname = "Nickname";
    public const string Nature = "Nature";
    public const string AbilitySlot = "AbilitySlot";
    public const string PidNature = "this game takes nature from the personality value";
    public const string PartyLocked = "a party Pokemon's species, EXP and level stay locked: the hack's ROM tables were not given to the editor, so its stats and HP would not be recalculated";
    public const string StoredLevel = "a stored record has no level of its own; it follows EXP";
    public const string NicknameNotWritten = "nicknames are not written in this build";

    private sealed record Limits(int Species, int Moves, int Item, int Iv, int Ev, int Exp, int Ball, int Nickname, bool HiddenFlag, bool Slot, bool Nature, bool Friendship);

    private static Limits? Of(RecordCodecId codec) => codec switch
    {
        RecordCodecId.Pk1 => new(0xFF, 0xFF, 0, 15, 0xFFFF, 0xFFFFFF, 0, 10, false, false, false, false),
        RecordCodecId.Pk2 => new(0xFF, 0xFF, 0xFF, 15, 0xFFFF, 0xFFFFFF, 0, 10, false, false, false, true),
        RecordCodecId.Pk3 => new(0xFFFF, 0xFFFF, 0xFFFF, 31, 255, int.MaxValue, 15, 10, false, true, true, true),
        RecordCodecId.Pk3CfruPlain => new(RecordPlausibility.CfruSpeciesCap, 0xFFFF, 0xFFFF, 31, 255, int.MaxValue, 255, 10, true, false, true, true),
        RecordCodecId.Pk3CfruCompact58 => new(RecordPlausibility.CfruSpeciesCap, (1 << Pk3CfruCompact58Encoder.MoveBits) - 1, 0xFFFF, 31, 255, int.MaxValue, 255, 10, true, false, true, true),
        RecordCodecId.Pk3x => new(0x7FF, 0x7FF, 0x3FF, 31, 255, 0x1FFFFF, 0x3F, 12, false, true, true, true),
        RecordCodecId.Pk3SoulGold96 => new(0x7FF, 0x7FF, 0x3FF, 31, 255, 0x1FFFFF, 0, 12, false, false, true, true),
        _ => null,
    };

    public static IReadOnlyList<EditFieldSpec> For(RecordCodecId codec, RecordShape shape)
    {
        if (Of(codec) is not { } l)
            return [];
        var writable = EditableFields.For(codec);
        var none = EditableFields.WhyNone(codec);
        var party = shape == RecordShape.Party;
        string? Why(string field, string? special = null) =>
            none ?? (party && EditableFields.PartyLocked.Contains(field) ? PartyLocked
                : writable.Contains(field) && EditableFields.Modelled.Contains(field) ? null
                : special ?? $"{field} is not written into {codec.Name()} records");
        var specs = new List<EditFieldSpec>
        {
            new("Species", EditFieldKind.SpeciesId, 1, l.Species, Why("Species")),
            new(Nickname, EditFieldKind.Text, 0, l.Nickname, Why(Nickname, NicknameNotWritten)),
            new("Moves", EditFieldKind.MoveId, 0, l.Moves, Why("Moves")),
        };
        if (l.Item > 0)
            specs.Add(new("HeldItem", EditFieldKind.ItemId, 0, l.Item, Why("HeldItem")));
        specs.Add(new("Exp", EditFieldKind.Number, 0, l.Exp, Why("Exp")));
        specs.Add(new(EditableFields.Level, EditFieldKind.Number, 1, RecordPlausibility.MaxLevel, party ? Why(EditableFields.Level) : none ?? StoredLevel));
        specs.Add(new("Ivs", EditFieldKind.Number, 0, l.Iv, Why("Ivs")));
        specs.Add(new("Evs", EditFieldKind.Number, 0, l.Ev, Why("Evs")));
        if (l.Nature)
            specs.Add(new(Nature, EditFieldKind.Nature, 0, 24, Why(Nature, codec is RecordCodecId.Pk3x or RecordCodecId.Pk3SoulGold96 ? null : PidNature)));
        if (l.Slot)
            specs.Add(new(AbilitySlot, EditFieldKind.AbilitySlot, 0, codec == RecordCodecId.Pk3x ? 2 : 1, Why(AbilitySlot)));
        if (l.HiddenFlag)
            specs.Add(new("HiddenAbility", EditFieldKind.Flag, 0, 1, Why("HiddenAbility")));
        if (l.Friendship)
            specs.Add(new("Friendship", EditFieldKind.Number, 0, 255, Why("Friendship")));
        if (l.Ball > 0)
            specs.Add(new("Ball", EditFieldKind.Number, 0, l.Ball, Why("Ball")));
        specs.Add(new("IsEgg", EditFieldKind.Flag, 0, 1, Why("IsEgg")));
        return specs;
    }

    public static EditFieldSpec? Find(RecordCodecId codec, RecordShape shape, string field) => For(codec, shape).FirstOrDefault(s => s.Field == field);

    public static int NumberCap(RecordCodecId codec, EditFieldKind kind) => Of(codec) is not { } l ? 0 : kind switch
    {
        EditFieldKind.SpeciesId => l.Species,
        EditFieldKind.MoveId => l.Moves,
        EditFieldKind.ItemId => l.Item,
        _ => 0,
    };
}
