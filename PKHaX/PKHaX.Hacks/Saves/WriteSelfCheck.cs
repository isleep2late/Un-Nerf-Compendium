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

public sealed record SelfCheckEdit(bool Party, int Box, int Slot, HackMon Expected, IReadOnlyList<string> Fields)
{
    public override string ToString() => Party ? $"party slot {Slot + 1}" : $"box {Box} slot {Slot + 1}";
}

public sealed record WriteCheckResult(IReadOnlyList<string> Failures)
{
    public bool Passed => Failures.Count == 0;
    public bool Undone { get; init; }
    public bool StateUnknown { get; init; }

    public override string ToString() => Passed ? "self-check: 0 failures" : $"self-check: {Failures.Count} failure(s): {string.Join("; ", Failures)}";
}

public static class WriteSelfCheck
{
    public const int HallOfFameSector = 28;
    public const int HallOfFameSectors = 2;

    public static byte[] Output(HackSave save, WriteResult plan)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(plan);
        var bytes = new byte[plan.Bytes.Length + save.Footer.Length];
        plan.Bytes.CopyTo(bytes, 0);
        save.Footer.Span.CopyTo(bytes.AsSpan(plan.Bytes.Length));
        return bytes;
    }

    public static string FieldValue(HackMon mon, string field) => field switch
    {
        "Species" => $"{mon.Species}",
        "HeldItem" => $"{mon.HeldItem}",
        "Exp" => $"{mon.Exp}",
        "Friendship" => $"{mon.Friendship}",
        "Ball" => $"{mon.Ball}",
        "Moves" => string.Join(",", mon.Moves),
        "Evs" => mon.Evs.ToString(),
        "Ivs" => $"{mon.Ivs}",
        "IsEgg" => $"{mon.IsEgg}",
        "HiddenAbility" => $"{mon.HiddenAbility}",
        EditableFields.Level => $"{mon.Party?.Level ?? mon.Level}",
        EditFieldSpecs.Nature => $"{mon.Nature}",
        EditFieldSpecs.AbilitySlot => $"{mon.AbilitySlot}",
        EditFieldSpecs.Nickname => mon.Nickname ?? "",
        _ => throw new ArgumentException($"no record field is named {field}", nameof(field)),
    };

    public static HackSave? Reopen(HackSave save, SaveShape? shape, ReadOnlyMemory<byte> bytes, out string? why)
    {
        var core = WriteGate.Core(save);
        if (shape is not null && (core is CfruSave { FromShape: true } || core is ExpansionSave { FromShape: true } || core is RetailGen3Save { FromShape: true }))
            return HackSaves.TryOpen(bytes, shape, WriteEvidence.Unproven, out why);
        var file = GbaSectorContainer.TrimFooter(bytes);
        switch (core)
        {
            case ExpansionSave e:
                return ExpansionSave.TryOpen(file, e.Layout, WriteEvidence.Unproven, out why);
            case CfruSave c:
                return CfruSave.TryOpen(file, WriteEvidence.Unproven, out why, c.Variant);
            case RetailGen3Save r:
                return RetailGen3Save.TryOpen(file, r.Layout, WriteEvidence.Unproven, out why);
            default:
                why = $"no hack save class reopens {save.Variant} saves";
                return null;
        }
    }

    public static WriteCheckResult Before(HackSave save, SaveShape? shape, ReadOnlyMemory<byte> fileNow, WriteResult plan, ReadOnlyMemory<byte> toWrite, IReadOnlyList<SelfCheckEdit> edits, uint? money)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(edits);
        var failures = new List<string>();
        CheckLayout(save, fileNow, plan, toWrite, failures);
        if (failures.Count > 0 && toWrite.Length < GbaSectorContainer.FileSize)
            return new WriteCheckResult(failures);
        var reopened = Reopen(save, shape, toWrite, out var why);
        var container = WriteGate.ContainerOf(save);
        if (reopened is null)
        {
            failures.Add($"2: the planned bytes do not open again ({why})");
            return new WriteCheckResult(failures);
        }
        var again = WriteGate.ContainerOf(reopened);
        if (container is null || again is null)
            failures.Add("2: the planned bytes open, but not as a sector save");
        else if (again.ActiveSlot != container.ActiveSlot || again.Counter != container.Counter)
            failures.Add($"2: the planned bytes open on slot {again.ActiveSlot} counter {again.Counter}, the file was slot {container.ActiveSlot} counter {container.Counter}");
        else if (!again.Layout.Slots.SequenceEqual(container.Layout.Slots) || !again.Policy.Windows.SequenceEqual(container.Policy.Windows))
            failures.Add($"2: the planned bytes open under another layout ({again.Layout.Name}, {again.Policy.Name}), the file was {container.Layout.Name}, {container.Policy.Name}");
        var baseline = Reopen(save, shape, save.Original, out var baseWhy);
        if (baseline is null)
        {
            failures.Add($"3: the file's own bytes do not open again ({baseWhy})");
            return new WriteCheckResult(failures);
        }
        CheckRecords(baseline, reopened, edits, failures);
        CheckGate(save, edits, money, failures);
        if (reopened.Trainer != baseline.Trainer)
            failures.Add("3: the trainer would change");
        if (reopened.Party.Count(p => p is not null) != baseline.Party.Count(p => p is not null))
            failures.Add("3: the party count would change");
        if (money is { } chosen ? reopened.Money != chosen : reopened.Money != baseline.Money)
            failures.Add(money is { } m ? $"3: money reads {reopened.Money?.ToString() ?? "nothing"}, the edit chose {m}" : $"3: money would change from {baseline.Money} to {reopened.Money}");
        return new WriteCheckResult(failures);
    }

    private static void CheckLayout(HackSave save, ReadOnlyMemory<byte> fileNow, WriteResult plan, ReadOnlyMemory<byte> toWrite, List<string> failures)
    {
        if (toWrite.Length != fileNow.Length)
            failures.Add($"1: the bytes to write are {toWrite.Length} long, the file is {fileNow.Length}");
        if (fileNow.Length > GbaSectorContainer.FileSize && toWrite.Length == fileNow.Length && !toWrite.Span[GbaSectorContainer.FileSize..].SequenceEqual(fileNow.Span[GbaSectorContainer.FileSize..]))
            failures.Add($"1: the {fileNow.Length - GbaSectorContainer.FileSize}-byte footer after the save would change");
        if (toWrite.Length < GbaSectorContainer.FileSize || fileNow.Length < GbaSectorContainer.FileSize)
        {
            failures.Add($"1: a Gen 3 save is 0x{GbaSectorContainer.FileSize:X} bytes");
            return;
        }
        var original = fileNow.Span[..GbaSectorContainer.FileSize];
        var body = toWrite.Span[..GbaSectorContainer.FileSize];
        if (!original.SequenceEqual(save.Original.Span))
            failures.Add("1: the file changed since it was opened");
        if (!plan.Confined)
            failures.Add($"1: the plan is not confined (outside: {string.Join(", ", plan.Outside)})");
        var actual = new WriteResult(body.ToArray(), plan.TouchedSections, FaithfulWriter.Diff(original, body), plan.Allowed, plan.TouchedRawSectors);
        if (!actual.Confined)
            failures.Add($"1: {actual.Outside.Count} byte range(s) would change outside the edited records and their checksums ({string.Join(", ", actual.Outside.Take(4))})");
        if (WriteGate.ContainerOf(save) is not { } container)
        {
            failures.Add("1: this save has no sector container to check");
            return;
        }
        var slotBytes = SectionLayout.SectionCount * SectionLayout.SectorSize;
        var inactive = (1 - container.ActiveSlot) * slotBytes;
        if (!body.Slice(inactive, slotBytes).SequenceEqual(original.Slice(inactive, slotBytes)))
            failures.Add($"1: the inactive save slot ({(container.ActiveSlot == 0 ? "B" : "A")}) would change");
        var fame = HallOfFameSector * SectionLayout.SectorSize;
        var fameBytes = HallOfFameSectors * SectionLayout.SectorSize;
        if (!body.Slice(fame, fameBytes).SequenceEqual(original.Slice(fame, fameBytes)))
            failures.Add("1: the Hall of Fame sectors 28-29 would change");
    }

    private static void CheckGate(HackSave save, IReadOnlyList<SelfCheckEdit> edits, uint? money, List<string> failures)
    {
        if (save.Gate is not { } gate)
            return;
        foreach (var edit in edits)
        {
            if (gate.SlotRefusal(edit.Party, edit.Box, edit.Slot) is { } slot)
                failures.Add($"3: {edit} is read-only by itself ({slot})");
            foreach (var field in edit.Fields)
            {
                if (gate.FieldRefusal(field, edit.Party) is { } locked)
                    failures.Add($"3: {edit} {field}: {locked}");
            }
        }
        if (money is not null && gate.FieldRefusal(EngineAcceptance.Money, false) is { } moneyLocked)
            failures.Add($"3: money: {moneyLocked}");
    }

    private static void CheckRecords(HackSave baseline, HackSave reopened, IReadOnlyList<SelfCheckEdit> edits, List<string> failures)
    {
        for (var i = 0; i < Math.Max(baseline.Party.Count, reopened.Party.Count); i++)
        {
            var before = i < baseline.Party.Count ? baseline.Party[i] : null;
            var after = i < reopened.Party.Count ? reopened.Party[i] : null;
            Compare($"party slot {i + 1}", before, after, edits.FirstOrDefault(e => e.Party && e.Slot == i), failures);
        }
        var numbers = baseline.Boxes.Select(b => b.Number).Union(reopened.Boxes.Select(b => b.Number)).Order().ToList();
        foreach (var number in numbers)
        {
            var before = baseline.Boxes.FirstOrDefault(b => b.Number == number);
            var after = reopened.Boxes.FirstOrDefault(b => b.Number == number);
            if (before is null || after is null)
            {
                failures.Add($"3: box {number} is read from {(before is null ? "the planned bytes only" : "the file only")}");
                continue;
            }
            for (var s = 0; s < Math.Max(before.Slots.Count, after.Slots.Count); s++)
            {
                var was = s < before.Slots.Count ? before.Slots[s] : null;
                var now = s < after.Slots.Count ? after.Slots[s] : null;
                Compare($"box {number} slot {s + 1}", was, now, edits.FirstOrDefault(e => !e.Party && e.Box == number && e.Slot == s), failures);
            }
        }
        foreach (var edit in edits.Where(e => !e.Party && !numbers.Contains(e.Box)))
            failures.Add($"3: {edit} is not in the planned bytes");
    }

    private static void Compare(string where, HackMon? before, HackMon? after, SelfCheckEdit? edit, List<string> failures)
    {
        if (edit is null)
        {
            var same = (before, after) switch
            {
                (null, null) => true,
                ({ } b, { } a) => b.Raw.Span.SequenceEqual(a.Raw.Span),
                _ => false,
            };
            if (!same)
                failures.Add($"3: {where} would change though it was not edited");
            return;
        }
        if (after is null)
        {
            failures.Add($"3: the edited {where} does not decode in the planned bytes");
            return;
        }
        foreach (var field in edit.Fields)
        {
            var got = FieldValue(after, field);
            var want = FieldValue(edit.Expected, field);
            if (got != want)
                failures.Add($"3: the edited {where} reads {field} {got}, the edit chose {want}");
        }
    }

    public const string BackupUnusable = "backup: the copy taken before this write is empty or does not match the file on disk, so nothing is written";

    public static WriteCheckResult Commit(ReadOnlyMemory<byte> toWrite, ReadOnlyMemory<byte> backup, ReadOnlyMemory<byte> fileNow, Action<byte[]> write, Func<byte[]> readBack)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(readBack);
        if (backup.IsEmpty || !backup.Span.SequenceEqual(fileNow.Span))
            return new WriteCheckResult([BackupUnusable]);
        string problem;
        try
        {
            write(toWrite.ToArray());
            var disk = readBack();
            if (disk.AsSpan().SequenceEqual(toWrite.Span))
                return new WriteCheckResult([]);
            problem = disk.Length != toWrite.Length
                ? $"the file on disk is {disk.Length} bytes, the planned bytes are {toWrite.Length}"
                : $"the file on disk differs from the planned bytes in {FaithfulWriter.Diff(toWrite.Span, disk).Count} range(s)";
        }
        catch (Exception e)
        {
            problem = $"the write failed ({e.Message})";
        }
        try
        {
            write(backup.ToArray());
            if (readBack().AsSpan().SequenceEqual(backup.Span))
                return new WriteCheckResult([$"read-back: {problem}; the backup was written back and the edit is undone"]) { Undone = true };
        }
        catch (Exception e)
        {
            problem = $"{problem}; writing the backup back failed ({e.Message})";
        }
        return new WriteCheckResult([$"read-back: {problem}; the save file is in an unknown state, restore it from the backup copy"]) { StateUnknown = true };
    }
}
