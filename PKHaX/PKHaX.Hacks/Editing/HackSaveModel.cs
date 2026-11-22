using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Gen3;

namespace PKHaX.Hacks.Editing;

public enum HackSlotKind
{
    Party,
    Box,
}

public readonly record struct HackSlotRef(HackSlotKind Kind, int Box, int Slot)
{
    public static HackSlotRef InParty(int slot) => new(HackSlotKind.Party, 0, slot);

    public static HackSlotRef InBox(int box, int slot) => new(HackSlotKind.Box, box, slot);

    public override string ToString() => Kind == HackSlotKind.Party ? $"party slot {Slot + 1}" : $"box {Box} slot {Slot + 1}";
}

public sealed record HackSlotModel(HackSlotRef Where, HackMon? Mon, string? Refused)
{
    public bool IsEmpty => Mon is null && Refused is null;

    public HackNames? Names { get; init; }

    public string Title => Mon is { } m
        ? Names is { } names && names.Has(HackNameKind.Species)
            ? $"{(m.IsEgg ? "Egg" : m.Nickname is { Length: > 0 } nick ? nick : "?")} - {names.Name(HackNameKind.Species, m.Species)}{(LevelOf(m) is { } level ? $" Lv {level}" : "")}"
            : $"{(m.IsEgg ? "Egg" : m.Nickname is { Length: > 0 } n ? n : "?")} (#{m.Species}){(LevelOf(m) is { } l ? $" Lv {l}" : "")}"
        : Refused is { } why ? $"unreadable record ({why})" : "empty";

    public IReadOnlyList<string> Detail => Mon is not { } mon || Names is not { } catalog ? [] :
    [
        $"Species: {catalog.Shown(HackNameKind.Species, mon.Species)}",
        $"Held item: {(mon.HeldItem == 0 ? "none" : catalog.Shown(HackNameKind.Item, mon.HeldItem))}",
        .. mon.Moves.Select((move, i) => $"Move {i + 1}: {(move == 0 ? "none" : catalog.Shown(HackNameKind.Move, move))}"),
    ];

    private static byte? LevelOf(HackMon m) => m.Party?.Level ?? m.Level;
}

public sealed record HackBoxModel(int Number, string Source, IReadOnlyList<HackSlotModel> Slots)
{
    public int Count => Slots.Count(s => s.Mon is not null);
}

public sealed class HackSaveModel(HackSave save, HackNames? names = null, HackEditContext? context = null)
{
    private HackSave Save { get; } = save;

    private readonly HackEditContext editContext = context ?? HackEditContext.None;

    public HackNames Names { get; } = names ?? HackNames.Empty;

    public string? NamesBanner => Names.NumberBanner;

    public IReadOnlyList<EditFieldSpec> Specs(HackSlotRef where) => Edit(where)?.Specs ?? [];

    private IReadOnlyList<HackSlotModel>? party;
    private IReadOnlyList<HackBoxModel>? boxes;

    public string Variant => Save.Variant;

    public string? Banner => Save.ReadOnlyReason;

    public bool CanWrite => !Save.ReadOnly;

    public string TrainerLine => $"{Save.Trainer.Name}, ID {Save.Trainer.Tid16:D5}, SID {Save.Trainer.Sid16:D5}";

    public string MoneyLine => Save.Money is { } money ? $"{money:N0}" : "where this hack keeps money is not known yet";

    public string? BoxesReason => Save.BoxesReason;

    public IReadOnlyList<HackSlotModel> Party => party ??= [.. Save.Party.Select((m, i) => new HackSlotModel(HackSlotRef.InParty(i), m, Save.PartyRefused.TryGetValue(i, out var why) ? why : null) { Names = Names })];

    public IReadOnlyList<HackBoxModel> Boxes => boxes ??= [.. Save.Boxes.Select(b => new HackBoxModel(b.Number, b.Source,
        [.. b.Slots.Select((m, s) => new HackSlotModel(HackSlotRef.InBox(b.Number, s), m, b.Refused.TryGetValue(s, out var why) ? why : null) { Names = Names })]))];

    public HackSlotModel? Slot(HackSlotRef where) => where.Kind == HackSlotKind.Party
        ? where.Slot >= 0 && where.Slot < Party.Count ? Party[where.Slot] : null
        : Boxes.FirstOrDefault(b => b.Number == where.Box) is { } box && where.Slot >= 0 && where.Slot < box.Slots.Count ? box.Slots[where.Slot] : null;

    public HackMonEditModel? Edit(HackSlotRef where) => Slot(where)?.Mon is { } mon
        ? new HackMonEditModel(where, mon, Save.Gate?.Locks(where.Kind == HackSlotKind.Party, EditableFields.Writable(mon.Codec.Id, mon.Shape)), editContext)
        : null;

    public IReadOnlyList<GateItem> GateItems => Save.Gate?.Items ?? [];

    public string? WhyNotEditable(HackSlotRef where) =>
        !CanWrite ? Banner ?? "this save is read-only"
        : Slot(where) is not { } slot ? $"{where}: no such slot in this save"
        : slot.Mon is not { } mon ? slot.Refused is { } why ? $"{where}: the record does not read ({why})" : $"{where}: empty; filling an empty slot is not supported yet"
        : EditableFields.WhyNone(mon.Codec.Id)
            ?? Save.SlotRefusal(where.Kind == HackSlotKind.Party, where.Box, where.Slot)
            ?? (where.Kind == HackSlotKind.Box ? Save.BoxWriteRefusal(where.Box) : null);

    private readonly Dictionary<HackSlotRef, SelfCheckEdit> applied = [];

    public IReadOnlyList<SelfCheckEdit> Edits => [.. applied.Values];

    public uint? MoneyEdit { get; private set; }

    private string? NameRefusal(HackMonEditModel edit, HackMon mon)
    {
        if (edit.Changed.Contains("Species") && Names.WhyRefused(HackNameKind.Species, mon.Species) is { } species)
            return $"Species: {species}";
        if (edit.Changed.Contains("HeldItem") && mon.HeldItem != 0 && Names.WhyRefused(HackNameKind.Item, mon.HeldItem) is { } item)
            return $"HeldItem: {item}";
        if (edit.Changed.Contains("Moves"))
        {
            foreach (var move in mon.Moves.Where(m => m != 0))
            {
                if (Names.WhyRefused(HackNameKind.Move, move) is { } why)
                    return $"Moves: {why}";
            }
        }
        return null;
    }

    public string? Apply(HackMonEditModel edit)
    {
        if (edit.Changed.Count == 0)
            return null;
        if (WhyNotEditable(edit.Where) is { } refusal)
            return $"{edit.Where} is not written: {refusal}";
        var inParty = edit.Where.Kind == HackSlotKind.Party;
        foreach (var field in edit.Changed)
        {
            if (Save.FieldRefusal(field, inParty) is { } locked)
                return $"{edit.Where} is not written: {field}: {locked}";
        }
        if (Slot(edit.Where)?.Mon is not { } now || !now.Raw.Span.SequenceEqual(edit.Original.Raw.Span))
            return $"{edit.Where} changed since this edit was opened; open it again";
        try
        {
            var mon = edit.ToMon();
            if (NameRefusal(edit, mon) is { } named)
                return $"{edit.Where} is not written: {named}";
            var fields = edit.Changed.ToList();
            if (inParty)
                Save.SetPartySlot(edit.Where.Slot, mon);
            else
                Save.SetBoxSlot(edit.Where.Box, edit.Where.Slot, mon);
            party = null;
            boxes = null;
            IReadOnlyList<string> before = applied.TryGetValue(edit.Where, out var earlier) ? earlier.Fields : [];
            applied[edit.Where] = new SelfCheckEdit(inParty, edit.Where.Box, edit.Where.Slot, mon, [.. before.Union(fields)]);
            edit.Rebase(Slot(edit.Where)!.Mon!);
            return null;
        }
        catch (Exception e) when (e is FaithfulWriteException or RecordEncodeException or ArgumentOutOfRangeException)
        {
            return $"{edit.Where} is not written: {e.Message}";
        }
    }

    public string? SetMoney(uint money)
    {
        if (!CanWrite)
            return $"money is not written: {Banner ?? "this save is read-only"}";
        if (Save.FieldRefusal(EngineAcceptance.Money, false) is { } locked)
            return $"money is not written: {locked}";
        try
        {
            Save.SetMoney(money);
            MoneyEdit = money;
            return null;
        }
        catch (FaithfulWriteException e)
        {
            return $"money is not written: {e.Message}";
        }
    }

    public WriteResult Preview() => Save.Plan();

    public WriteResult Write() => Save.Write();

    public const string NotAdmitted = "write: this save was not admitted through the write gate";

    public WriteCheckResult CheckedWrite(SaveShape? shape, ReadOnlyMemory<byte> fileNow, out byte[]? bytes)
    {
        bytes = null;
        if (Save.Gate is not { Passed: true })
            return new WriteCheckResult([NotAdmitted]);
        WriteResult plan;
        try
        {
            plan = Save.Write();
        }
        catch (FaithfulWriteException e)
        {
            return new WriteCheckResult([$"write: {e.Message}"]);
        }
        var output = WriteSelfCheck.Output(Save, plan);
        var check = WriteSelfCheck.Before(Save, shape, fileNow, plan, output, Edits, MoneyEdit);
        if (check.Passed)
            bytes = output;
        return check;
    }

    public WriteCheckResult Commit(ReadOnlyMemory<byte> toWrite, ReadOnlyMemory<byte> backup, ReadOnlyMemory<byte> fileNow, Action<byte[]> write, Func<byte[]> readBack) =>
        WriteSelfCheck.Commit(toWrite, backup, fileNow, write, readBack);
}
