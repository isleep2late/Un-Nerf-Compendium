using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Expansion;
using PKHaX.Hacks.Saves.Gen3;

namespace PKHaX.Hacks.Saves;

public sealed record WriteGateInput
{
    public SaveShape? Shape { get; init; }
    public TrialResult? Trial { get; init; }
    public WriteEvidence ProfileClaim { get; init; } = WriteEvidence.Unproven;
    public bool MatchAllowsWrite { get; init; }
    public bool FromFile { get; init; }
    public bool OnDeviceCheck { get; init; }
    public bool PartyStatsAgree { get; init; }
    public string? PartyStatsSentence { get; init; }
}

public sealed record GateItem(string Number, string Name, bool Passed, string? Sentence)
{
    public override string ToString() => $"{Number} {Name}: {(Passed ? "pass" : Sentence ?? "fail")}";
}

public sealed class WriteGateVerdict
{
    private readonly bool partyStatsAgree;
    private readonly string partyStatsSentence;
    private readonly string? moneyRefusal;

    internal WriteGateVerdict(IReadOnlyList<GateItem> items, string? reason, WriteEvidence effective, AcceptanceKey? key, AcceptanceRow? row, string? partyRefusal,
        IReadOnlyDictionary<(int Box, int Slot), string> boxRefusals, bool partyStatsAgree, string partyStatsSentence, string? moneyRefusal)
    {
        Items = items;
        Reason = reason;
        Effective = effective;
        Key = key;
        Row = row;
        PartyRefusal = partyRefusal;
        BoxRefusals = boxRefusals;
        Fields = new HashSet<string>(row?.Fields ?? Array.Empty<string>(), StringComparer.Ordinal);
        this.partyStatsAgree = partyStatsAgree;
        this.partyStatsSentence = partyStatsSentence;
        this.moneyRefusal = moneyRefusal;
    }

    public IReadOnlyList<GateItem> Items { get; }
    public string? Reason { get; }
    public bool Passed => Reason is null;
    public WriteEvidence Effective { get; }
    public AcceptanceKey? Key { get; }
    public AcceptanceRow? Row { get; }
    public string? PartyRefusal { get; }
    public IReadOnlyDictionary<(int Box, int Slot), string> BoxRefusals { get; }
    public IReadOnlySet<string> Fields { get; }

    public string? FieldRefusal(string field, bool party)
    {
        if (Reason is { } closed)
            return closed;
        if (party && !partyStatsAgree && EditableFields.PartyLocked.Contains(field))
            return partyStatsSentence;
        if (field == EngineAcceptance.Money && moneyRefusal is { } money)
            return money;
        return Fields.Contains(field) ? null : WriteGate.FieldNotAccepted;
    }

    public string? SlotRefusal(bool party, int box, int slot) => party
        ? PartyRefusal
        : BoxRefusals.TryGetValue((box, slot), out var why) ? why : null;

    public IReadOnlyDictionary<string, string> Locks(bool party, IEnumerable<string> fields)
    {
        var locks = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (FieldRefusal(field, party) is { } why)
                locks[field] = why;
        }
        return locks;
    }
}

public static class WriteGate
{
    public const string FieldNotAccepted = "the game has not been shown to keep this field yet";
    public const string ConfinedSentence = "This hack's engine family is accepted. Run 'Check this hack's saves' once to edit.";
    public const string LiveSentence = "Close the game, then open its save file to edit it.";
    public const string MatchSentence = "this save's match to the hack does not allow writing: only a match by signature, sibling ROM, profile file or your own answer does";
    public const string ListedMapSentence = "this save was read through a listed layout, not one derived from the hack's ROM; open or import the hack's ROM once so the editor can read its save layout";
    public const string NoMoney = "where this hack keeps money is not known";

    public static WriteGateVerdict Admit(HackSave save, WriteGateInput input) => Admit(save, input, EngineAcceptance.Rows);

    public static WriteGateVerdict Admit(HackSave save, WriteGateInput input, IReadOnlyList<AcceptanceRow> rows)
    {
        var verdict = Evaluate(save, input, rows);
        save.Admit(verdict);
        if (save is TiedSave tied)
        {
            foreach (var layout in tied.Layouts)
                layout.Admit(verdict);
        }
        return verdict;
    }

    public static WriteGateVerdict Evaluate(HackSave save, WriteGateInput input, IReadOnlyList<AcceptanceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(rows);
        var core = Core(save);
        var items = new List<GateItem>();
        var structure = StructureOf(save);
        items.Add(new GateItem("5", "structure", structure is null, structure));
        items.Add(new GateItem("7", "a file, not a running game", input.FromFile, input.FromFile ? null : LiveSentence));
        items.Add(new GateItem("6", "match strength", input.MatchAllowsWrite, input.MatchAllowsWrite ? null : MatchSentence));
        var shapeWhy = ShapeProblem(core, input);
        items.Add(new GateItem("4", "settled shape", shapeWhy is null, shapeWhy));
        var roundTrip = RoundTrip(save);
        items.Add(new GateItem("2", "live round trip", roundTrip is null, roundTrip));
        var key = KeyOf(save, input.Trial);
        var row = key is null ? null : EngineAcceptance.Find(key, rows);
        var familyWhy = key is null ? "this save's engine family is not one the write gate knows"
            : row is null ? $"no game has been shown to load an edited save of this engine family yet ({key}); the save stays read-only until an acceptance run for it is recorded"
            : null;
        items.Add(new GateItem("1a", "engine family accepted", familyWhy is null, familyWhy));
        var image = input.Shape?.RomSha1.ToLowerInvariant();
        var imageOk = row is not null && ((image is not null && row.Images.Contains(image, StringComparer.Ordinal)) || input.OnDeviceCheck);
        items.Add(new GateItem("1b", "this image", imageOk, imageOk ? null : row is null ? familyWhy : ConfinedSentence));
        var (partyRefusal, boxRefusals) = Identity(core);
        var identityOk = partyRefusal is null && boxRefusals.Count == 0;
        items.Add(new GateItem("3", "live identity encode", identityOk, identityOk ? null : partyRefusal ?? $"{boxRefusals.Count} box slot(s) do not re-encode to their own bytes and stay read-only"));
        var partyStatsSentence = input.PartyStatsSentence ?? EditFieldSpecs.PartyLocked;
        items.Add(new GateItem("8", "party stats agree", input.PartyStatsAgree, input.PartyStatsAgree ? null : partyStatsSentence));
        var blocking = items.Where(i => i.Number is not ("3" or "8") && !i.Passed).ToList();
        var reason = blocking.FirstOrDefault()?.Sentence;
        var effective = blocking.Count == 0 ? WriteEvidence.GameAccepted
            : blocking.All(i => i.Number == "1b") ? WriteEvidence.Confined
            : (WriteEvidence)Math.Min((int)input.ProfileClaim, (int)WriteEvidence.Confined);
        var moneyRefusal = input.Shape?.Money is not { } money || money.Tier == ShapeTier.Unknown || core.Money is null ? NoMoney : null;
        return new WriteGateVerdict(items, reason, effective, key, row, partyRefusal, boxRefusals, input.PartyStatsAgree, partyStatsSentence, moneyRefusal);
    }

    public static HackSave Core(HackSave save) => save is TiedSave tied ? Core(tied.Primary) : save;

    public static string? StructureOf(HackSave save) => Core(save) switch
    {
        CfruSave c => c.StructureProblem,
        ExpansionSave e => e.StructureProblem,
        RetailGen3Save r => r.StructureProblem,
        _ => null,
    };

    public static GbaSectorContainer? ContainerOf(HackSave save) => Core(save) switch
    {
        CfruSave c => c.Container,
        ExpansionSave e => e.Container,
        RetailGen3Save r => r.Container,
        _ => null,
    };

    public static AcceptanceKey? KeyOf(HackSave save, TrialResult? trial)
    {
        var core = Core(save);
        var rules = trial is { Survivors.Count: > 0 } t
            ? string.Join("+", t.Survivors.Select(c => c.Rule.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            : "listed";
        var stride = trial?.Primary?.Stride;
        switch (core)
        {
            case CfruSave c:
            {
                var areas = c.VariantInfo.BoxSources.Select(s => s.Area.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
                return new AcceptanceKey(SaveShape.Cfru, rules, $"0x{stride ?? SectionLayout.CfruStride:X}", RecordCodecId.Pk3CfruPlain.Name(), RecordCodecId.Pk3CfruCompact58.Name(),
                    areas.Count == 0 ? EngineAcceptance.NoAreas : string.Join("+", areas));
            }
            case ExpansionSave e:
                return new AcceptanceKey(SaveShape.Expansion, rules, $"0x{stride ?? e.Layout.Stride:X}", e.Layout.Codec.Id.Name(), e.Layout.Codec.Id.Name(),
                    e.Boxes.Count == 0 ? EngineAcceptance.NoAreas : CfruBoxArea.Storage.ToString());
            case RetailGen3Save r:
                return new AcceptanceKey(SaveShape.Retail, rules, $"0x{stride ?? SectionLayout.VanillaStride:X}", RecordCodecId.Pk3.Name(), RecordCodecId.Pk3.Name(),
                    r.Boxes.Count == 0 ? EngineAcceptance.NoAreas : CfruBoxArea.Storage.ToString());
            default:
                return null;
        }
    }

    public static byte[] Encode(HackMon mon) => mon.Codec.Id switch
    {
        RecordCodecId.Pk3CfruPlain => Pk3CfruPlainEncoder.Encode(mon),
        RecordCodecId.Pk3CfruCompact58 => Pk3CfruCompact58Encoder.Encode(mon),
        RecordCodecId.Pk3x => Pk3xEncoder.Encode(mon),
        RecordCodecId.Pk3SoulGold96 => Pk3SoulGold96Encoder.Encode(mon),
        RecordCodecId.Pk3 => Pk3Encoder.Encode(mon),
        _ => throw new RecordEncodeException($"no hack record encoder writes {mon.Codec} records"),
    };

    private static string? ShapeProblem(HackSave core, WriteGateInput input)
    {
        if (input.Shape is not { } shape)
            return ListedMapSentence;
        if (!shape.Known)
            return $"this hack's save layout is not known ({shape.Reason ?? "no family was derived"})";
        if (shape.FamilyTier == ShapeTier.Unknown || shape.LayoutTier == ShapeTier.Unknown)
            return "this hack's save layout was not derived from its ROM";
        if (input.Trial is not { Refusal: null, Survivors.Count: > 0 })
            return "this save was not settled against the hack's layout";
        return core switch
        {
            CfruSave { FromShape: false } or ExpansionSave { FromShape: false } or RetailGen3Save { FromShape: false } => ListedMapSentence,
            CfruSave when shape.Family != SaveShape.Cfru => $"the save was read as CFRU but the hack's layout says {shape.Family}",
            ExpansionSave when shape.Family == SaveShape.Cfru => "the save was read as an expansion save but the hack's layout says CFRU",
            RetailGen3Save when shape.Family != SaveShape.Retail => $"the save was read as a retail Gen 3 save but the hack's layout says {shape.Family}",
            CfruSave or ExpansionSave or RetailGen3Save => null,
            _ => $"no hack save class writes {core.Variant} saves",
        };
    }

    private static string? RoundTrip(HackSave save)
    {
        try
        {
            var plan = save.Plan();
            return plan.Bytes.AsSpan().SequenceEqual(save.Original.Span)
                ? null
                : $"a plan with no edits does not give back the file's own bytes ({plan.DiffRanges.Count} changed ranges), so nothing is written; open the save again before editing";
        }
        catch (FaithfulWriteException e)
        {
            return $"a plan with no edits is refused ({e.Message}), so nothing is written";
        }
    }

    private static (string? Party, IReadOnlyDictionary<(int Box, int Slot), string> Boxes) Identity(HackSave core)
    {
        string? party = null;
        foreach (var (slot, why) in core.PartyRefused.OrderBy(p => p.Key))
        {
            party = $"party slot {slot + 1} does not read ({why}), so the party is not written";
            break;
        }
        for (var i = 0; party is null && i < core.Party.Count; i++)
        {
            if (core.Party[i] is not { } mon)
                continue;
            if (Reencodes(mon) is { } why)
                party = $"party slot {i + 1} {why}, so the party is not written";
        }
        var boxes = new Dictionary<(int Box, int Slot), string>();
        foreach (var box in core.Boxes)
        {
            for (var s = 0; s < box.Slots.Count; s++)
            {
                if (box.Slots[s] is { } mon && Reencodes(mon) is { } why)
                    boxes[(box.Number, s)] = $"box {box.Number} slot {s + 1} {why}, so it is not written";
            }
        }
        return (party, boxes);
    }

    private static string? Reencodes(HackMon mon)
    {
        try
        {
            return Encode(mon).AsSpan().SequenceEqual(mon.Raw.Span) ? null : "does not re-encode to its own bytes";
        }
        catch (RecordEncodeException e)
        {
            return $"does not re-encode ({e.Message})";
        }
    }
}
