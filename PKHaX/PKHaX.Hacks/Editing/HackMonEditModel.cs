using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;

namespace PKHaX.Hacks.Editing;

public static class EditableFields
{
    public const string Level = "Party.Level";

    public static IReadOnlyList<string> Modelled { get; } =
        ["Species", "HeldItem", "Exp", "Friendship", "Ball", "Moves", "Evs", "Ivs", "IsEgg", "HiddenAbility", Level, EditFieldSpecs.Nature, EditFieldSpecs.AbilitySlot, EditFieldSpecs.Nickname];

    public static IReadOnlyList<string> PartyLocked { get; } = ["Species", "Exp", Level];

    public static IReadOnlyList<EncodedField> Encoded(RecordCodecId codec) => codec switch
    {
        RecordCodecId.Pk3CfruCompact58 => Pk3CfruCompact58Encoder.Fields,
        RecordCodecId.Pk3CfruPlain => Pk3CfruPlainEncoder.Fields,
        RecordCodecId.Pk3x => Pk3xEncoder.Fields,
        RecordCodecId.Pk3SoulGold96 => Pk3SoulGold96Encoder.Fields,
        RecordCodecId.Pk3 => Pk3Encoder.Fields,
        _ => [],
    };

    public static EncodedField? PartyLevel(RecordCodecId codec) => codec switch
    {
        RecordCodecId.Pk3x => Pk3xEncoder.PartyLevel,
        RecordCodecId.Pk3SoulGold96 => Pk3SoulGold96Encoder.PartyLevel,
        _ => null,
    };

    public static IReadOnlyList<EncodedField> Encoded(RecordCodecId codec, RecordShape shape)
    {
        var fields = Encoded(codec);
        if (shape != RecordShape.Party)
            return [.. fields.Where(f => f.Field != Level)];
        return PartyLevel(codec) is { } level && !fields.Any(f => f.Field == Level) ? [.. fields, level] : fields;
    }

    public static IReadOnlyList<string> For(RecordCodecId codec) => [.. Encoded(codec).Select(f => f.Field)];

    public static IReadOnlyList<string> Writable(RecordCodecId codec, RecordShape shape) => [.. Encoded(codec, shape).Select(f => f.Field)];

    public static string? WhyNone(RecordCodecId codec) => For(codec).Count > 0 ? null : $"{codec} records open view-only: their editable fields are not listed yet";

    public static IReadOnlyList<EditFieldSpec> Specs(RecordCodecId codec, RecordShape shape)
    {
        var party = shape == RecordShape.Party;
        return [.. Encoded(codec, shape).Where(f => Modelled.Contains(f.Field)).Select(f => f.Spec with
        {
            Why = party && PartyLocked.Contains(f.Field) ? EditFieldSpecs.PartyLocked
                : f.Field == EditFieldSpecs.Nature && codec is not (RecordCodecId.Pk3x or RecordCodecId.Pk3SoulGold96) ? EditFieldSpecs.PidNature
                : null,
        })];
    }
}

public sealed record HackEditContext(HackData? Data, PartyStatsVerdict? Party)
{
    public static HackEditContext None { get; } = new(null, null);
}

public sealed class HackMonEditModel
{
    private readonly HashSet<string> editable;
    private readonly Dictionary<string, EditFieldSpec> specs;
    private readonly IReadOnlyDictionary<string, string> locks;
    private readonly HackEditContext context;
    private ushort[] moves = [];
    private byte[]? pp;
    private readonly List<string> changed = [];
    private readonly Dictionary<int, string> ppNotes = [];
    private ushort species;
    private ushort heldItem;
    private uint exp;
    private byte? friendship;
    private byte? ball;
    private StatSet evs;
    private StatSet? ivs;
    private bool isEgg;
    private bool? hiddenAbility;
    private byte? level;
    private byte? nature;
    private byte? abilitySlot;
    private string? nickname;

    public HackMonEditModel(HackSlotRef where, HackMon original) : this(where, original, null, null)
    {
    }

    public HackMonEditModel(HackSlotRef where, HackMon original, IReadOnlyDictionary<string, string>? locks) : this(where, original, locks, null)
    {
    }

    public HackMonEditModel(HackSlotRef where, HackMon original, IReadOnlyDictionary<string, string>? locks, HackEditContext? context)
    {
        ArgumentNullException.ThrowIfNull(original);
        Where = where;
        Original = original;
        this.locks = locks ?? new Dictionary<string, string>();
        this.context = context ?? HackEditContext.None;
        specs = EditableFields.Specs(original.Codec.Id, original.Shape).ToDictionary(s => s.Field);
        editable = [.. specs.Values.Where(s => s.Editable && !this.locks.ContainsKey(s.Field)).Select(s => s.Field)];
        if (original.Party is not null)
        {
            editable.ExceptWith(EditableFields.PartyLocked);
            if (PartyStatsRefusal() is null && EditableFields.PartyLocked.All(f => specs.ContainsKey(f) && !this.locks.ContainsKey(f)))
                editable.UnionWith(EditableFields.PartyLocked);
        }
        else
        {
            editable.Remove(EditableFields.Level);
        }
        if (original.Nature is null || original.PidNature is null)
            editable.Remove(EditFieldSpecs.Nature);
        if (original.AbilitySlot is null)
            editable.Remove(EditFieldSpecs.AbilitySlot);
        if (original.Nickname is null)
            editable.Remove(EditFieldSpecs.Nickname);
        Load(original);
    }

    internal void Rebase(HackMon now)
    {
        Original = now;
        changed.Clear();
        ppNotes.Clear();
        Load(now);
    }

    private void Load(HackMon original)
    {
        moves = [.. original.Moves];
        pp = original.Pp is { } p ? [.. p] : null;
        species = original.Species;
        heldItem = original.HeldItem;
        exp = original.Exp;
        friendship = original.Friendship;
        ball = original.Ball;
        evs = original.Evs;
        ivs = original.Ivs;
        isEgg = original.IsEgg;
        hiddenAbility = original.HiddenAbility;
        level = original.Party?.Level;
        nature = original.Nature;
        abilitySlot = original.AbilitySlot;
        nickname = original.Nickname;
    }

    public HackSlotRef Where { get; }
    public HackMon Original { get; private set; }

    public IReadOnlyCollection<string> Editable => editable;

    public IReadOnlyList<EditFieldSpec> Specs => [.. specs.Values.Select(s => s with { Why = LockedReason(s.Field) })];

    public IReadOnlyList<string> Changed => changed;

    public IReadOnlyList<string> Notes => [.. ppNotes.OrderBy(n => n.Key).Select(n => n.Value)];

    public bool CanEdit(string field) => editable.Contains(field);

    public string? PartyStatsRefusal()
    {
        if (Original.Party is null)
            return null;
        if (context.Party is not { } verdict)
            return EditFieldSpecs.PartyLocked;
        if (!verdict.Agree)
            return verdict.Sentence ?? EditFieldSpecs.PartyLocked;
        if (Original.Ivs is null || Original.Nature is not < 25)
            return PartyStats.NoIvs;
        return PartyStats.Row(context.Data, Original.Species, out var why) is null ? why : null;
    }

    public string? LockedReason(string field)
    {
        if (CanEdit(field))
            return null;
        if (Original.Party is not null && EditableFields.PartyLocked.Contains(field) && EditableFields.WhyNone(Original.Codec.Id) is null)
        {
            var own = locks.TryGetValue(field, out var mine) ? mine : null;
            if ((own is null || own == EditFieldSpecs.PartyLocked) && PartyStatsRefusal() is { } stats)
                return stats;
            if (own is not null)
                return own;
            if (EditableFields.PartyLocked.Where(locks.ContainsKey).Select(f => locks[f]).FirstOrDefault() is { } unit)
                return unit;
            if (EditableFields.PartyLocked.FirstOrDefault(f => !specs.ContainsKey(f)) is { } missing)
                return $"{missing} is not written into {Original.Codec} records, so a party Pokemon's species, EXP and level stay locked";
        }
        return locks.TryGetValue(field, out var locked) ? locked
            : EditableFields.WhyNone(Original.Codec.Id)
            ?? (field == EditableFields.Level && Original.Party is null ? EditFieldSpecs.StoredLevel
            : specs.TryGetValue(field, out var spec) && spec.Why is { } why ? why
            : $"{field} is not written into {Original.Codec} records");
    }

    private void GuardRange(PartyStatsRow row, byte lv)
    {
        if (ivs is { } i && nature is { } n && PartyStats.RangeProblem(row, lv, i, evs, n) is { } range)
            throw new InvalidOperationException(range);
    }

    public byte? LevelFromExp => Row(species) is { } row ? PartyStats.LevelOf(exp, row.Growth) : null;

    private PartyStatsRow? Row(ushort id) => PartyStats.Row(context.Data, id, out _);

    public ushort Species
    {
        get => species;
        set
        {
            if (Original.Party is null)
            {
                Set("Species", ref species, value);
                return;
            }
            if (value == species)
                return;
            Guard("Species", value);
            var row = PartyStats.Row(context.Data, value, out var why) ?? throw new InvalidOperationException(why);
            var lv = level ?? Original.Party.Level;
            GuardRange(row, lv);
            species = value;
            exp = Unchanged(lv) ? Original.Exp : PartyStats.MinExp(lv, row.Growth);
            MarkParty();
        }
    }

    public uint Exp
    {
        get => exp;
        set
        {
            if (Original.Party is null)
            {
                Set("Exp", ref exp, value);
                return;
            }
            if (value == exp)
                return;
            Guard("Exp", value);
            var row = PartyStats.Row(context.Data, species, out var why) ?? throw new InvalidOperationException(why);
            var lv = PartyStats.LevelOf(value, row.Growth);
            GuardRange(row, lv);
            exp = value;
            level = lv;
            MarkParty();
        }
    }

    public byte? Level
    {
        get => level;
        set
        {
            if (Original.Party is null || value == level)
            {
                Set(EditableFields.Level, ref level, value);
                return;
            }
            Guard(EditableFields.Level, value);
            if (value is not { } lv)
                throw new InvalidOperationException($"{EditableFields.Level} cannot be cleared; give it a value");
            var row = PartyStats.Row(context.Data, species, out var why) ?? throw new InvalidOperationException(why);
            GuardRange(row, lv);
            level = lv;
            exp = Unchanged(lv) ? Original.Exp : PartyStats.MinExp(lv, row.Growth);
            MarkParty();
        }
    }

    public ushort HeldItem { get => heldItem; set => Set("HeldItem", ref heldItem, value); }
    public byte? Friendship { get => friendship; set => Set("Friendship", ref friendship, value); }
    public byte? Ball { get => ball; set => Set("Ball", ref ball, value); }
    public StatSet Evs { get => evs; set => Set("Evs", ref evs, value); }
    public StatSet? Ivs { get => ivs; set => Set("Ivs", ref ivs, value); }
    public bool IsEgg { get => isEgg; set => Set("IsEgg", ref isEgg, value); }
    public bool? HiddenAbility { get => hiddenAbility; set => Set("HiddenAbility", ref hiddenAbility, value); }
    public byte? Nature { get => nature; set => Set(EditFieldSpecs.Nature, ref nature, value); }
    public byte? AbilitySlot { get => abilitySlot; set => Set(EditFieldSpecs.AbilitySlot, ref abilitySlot, value); }

    public string? Nickname
    {
        get => nickname;
        set
        {
            if (value == nickname)
                return;
            if (!CanEdit(EditFieldSpecs.Nickname))
                throw new InvalidOperationException(LockedReason(EditFieldSpecs.Nickname));
            var max = specs.TryGetValue(EditFieldSpecs.Nickname, out var spec) ? spec.Max : 0;
            if (Gen3Record.TextProblem(value, Original.Language, max) is { } why)
                throw new InvalidOperationException($"{EditFieldSpecs.Nickname}: {why}");
            nickname = value;
            Mark(EditFieldSpecs.Nickname, value != Original.Nickname);
        }
    }

    public IReadOnlyList<ushort> Moves => moves;

    public IReadOnlyList<byte>? Pp => pp;

    public void SetMove(int index, ushort move)
    {
        if (index is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(index), index, "a Pokemon has four move slots");
        if (moves[index] == move)
            return;
        if (!CanEdit("Moves"))
            throw new InvalidOperationException(LockedReason("Moves"));
        if (OutOfRange("Moves", move) is { } range)
            throw new InvalidOperationException(range);
        moves[index] = move;
        if (pp is not null && Original.Pp is { } was)
        {
            ppNotes.Remove(index);
            if (move == Original.Moves[index])
            {
                pp[index] = was[index];
            }
            else
            {
                var ups = Original.PpUps is { Count: 4 } u ? u[index] : 0;
                pp[index] = MovePp.For(context.Data, move, ups, out var read);
                if (!read)
                    ppNotes[index] = MovePp.UnreadNote(index, move);
            }
        }
        Mark("Moves", !moves.SequenceEqual(Original.Moves));
    }

    private bool Unchanged(byte lv) => species == Original.Species && lv == Original.Party?.Level;

    private void Guard<T>(string field, T value)
    {
        if (!CanEdit(field))
            throw new InvalidOperationException(LockedReason(field));
        if (OutOfRange(field, value) is { } range)
            throw new InvalidOperationException(range);
    }

    private void MarkParty()
    {
        Mark("Species", species != Original.Species);
        Mark("Exp", exp != Original.Exp);
        Mark(EditableFields.Level, level != Original.Party?.Level);
    }

    private string? OutOfRange(string field, long value) =>
        specs.TryGetValue(field, out var spec) && (value < spec.Min || value > spec.Max) ? $"{field}: {value} is outside {spec.Range} for {Original.Codec} records" : null;

    private string? OutOfRange<T>(string field, T value) => value switch
    {
        null => null,
        bool => null,
        StatSet s => Enumerable.Range(0, 6).Select(k => OutOfRange(field, (long)s[k])).FirstOrDefault(r => r is not null),
        byte b => OutOfRange(field, (long)b),
        ushort w => OutOfRange(field, (long)w),
        uint d => OutOfRange(field, (long)d),
        int n => OutOfRange(field, (long)n),
        _ => null,
    };

    private void Set<T>(string field, ref T backing, T value)
    {
        if (EqualityComparer<T>.Default.Equals(backing, value))
            return;
        if (!CanEdit(field))
            throw new InvalidOperationException(LockedReason(field));
        if (value is null && OriginalValue<T>(field) is not null)
            throw new InvalidOperationException($"{field} cannot be cleared; give it a value");
        if (OutOfRange(field, value) is { } range)
            throw new InvalidOperationException(range);
        backing = value;
        Mark(field, !EqualityComparer<T>.Default.Equals(value, OriginalValue<T>(field)));
    }

    private T OriginalValue<T>(string field) => (T)(field switch
    {
        "Species" => Original.Species,
        "HeldItem" => Original.HeldItem,
        "Exp" => Original.Exp,
        "Friendship" => Original.Friendship,
        "Ball" => Original.Ball,
        "Evs" => Original.Evs,
        "Ivs" => Original.Ivs,
        "IsEgg" => Original.IsEgg,
        "HiddenAbility" => Original.HiddenAbility,
        EditableFields.Level => (object?)Original.Party?.Level,
        EditFieldSpecs.Nature => Original.Nature,
        EditFieldSpecs.AbilitySlot => Original.AbilitySlot,
        EditFieldSpecs.Nickname => Original.Nickname,
        _ => throw new ArgumentException(field),
    })!;

    private void Mark(string field, bool differs)
    {
        if (differs && !changed.Contains(field))
            changed.Add(field);
        else if (!differs)
            changed.Remove(field);
    }

    public bool Recomputes => Original.Party is { } was && PartyStatsRefusal() is null && level is { } lv
        && (species != Original.Species || lv != was.Level || ivs != Original.Ivs || evs != Original.Evs || nature != Original.Nature);

    public HackMon ToMon()
    {
        var mon = Original;
        foreach (var field in changed)
        {
            mon = field switch
            {
                "Species" => mon with { Species = species },
                "HeldItem" => mon with { HeldItem = heldItem },
                "Exp" => mon with { Exp = exp },
                "Friendship" => mon with { Friendship = friendship },
                "Ball" => mon with { Ball = ball },
                "Evs" => mon with { Evs = evs },
                "Ivs" => mon with { Ivs = ivs },
                "IsEgg" => mon with { IsEgg = isEgg },
                "HiddenAbility" => mon with { HiddenAbility = hiddenAbility },
                "Moves" => mon with { Moves = [.. moves], Pp = pp is null ? mon.Pp : [.. pp] },
                EditableFields.Level => mon with { Level = level, Party = mon.Party! with { Level = level ?? mon.Party.Level } },
                EditFieldSpecs.Nature => mon with { Nature = nature },
                EditFieldSpecs.AbilitySlot => mon with { AbilitySlot = abilitySlot },
                EditFieldSpecs.Nickname => mon with { Nickname = nickname },
                _ => mon,
            };
        }
        if (Recomputes && Original.Party is { } was && level is { } lv)
        {
            var row = PartyStats.Row(context.Data, species, out var why) ?? throw new InvalidOperationException(why);
            var withIvs = ivs ?? throw new InvalidOperationException(PartyStats.NoIvs);
            var withNature = nature ?? throw new InvalidOperationException(PartyStats.NoIvs);
            if (PartyStats.RangeProblem(row, lv, withIvs, evs, withNature) is { } range)
                throw new RecordEncodeException(range);
            var stats = PartyStats.Compute(row, lv, withIvs, evs, withNature);
            mon = mon with { Level = lv, Party = was with { Level = lv, Stats = stats, CurrentHp = PartyStats.CurrentHp(was.Stats.Hp, was.CurrentHp, stats.Hp) } };
        }
        return mon;
    }
}
