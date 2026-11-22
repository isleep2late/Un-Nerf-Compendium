using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Expansion;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class WriteGateTests
{
    private static readonly (int Box, int Slot)[] Late = [(1, 0), (5, 3)];
    private static readonly string ThisImage = new('0', 40);

    private static (HackSave Save, TrialResult Trial, SaveShape Shape, byte[] File) Open()
    {
        var shape = ShapeSynth.Shape();
        var file = ShapeSynth.File(0xF80, true, Late);
        var save = HackSaves.TryOpen(file, shape, WriteEvidence.GameAccepted, out var why, out var trial);
        Assert.True(save is not null, why);
        return (save!, trial, shape, file);
    }

    private static WriteGateInput Good(SaveShape shape, TrialResult trial) => new() { Shape = shape, Trial = trial, MatchAllowsWrite = true, FromFile = true };

    private static AcceptanceRow Row(HackSave save, TrialResult trial, string image, IEnumerable<string> fields) =>
        new(WriteGate.KeyOf(save, trial)!, "test.md", [image], [.. fields]);

    private static IReadOnlyList<string> All => [.. EditableFields.Writable(RecordCodecId.Pk3x, RecordShape.Party), EngineAcceptance.Money];

    [Fact]
    public void ASyntheticImageIsNotWritableWithTheShippedTable()
    {
        var (save, trial, shape, _) = Open();
        var verdict = WriteGate.Admit(save, Good(shape, trial) with { ProfileClaim = WriteEvidence.GameAccepted });
        Assert.True(save.ReadOnly);
        Assert.False(verdict.Passed);
        Assert.Equal(WriteEvidence.Confined, verdict.Effective);
        Assert.Throws<FaithfulWriteException>(() => save.Write());
    }

    [Fact]
    public void ARowForThisImageUnlocksTheListedFieldsOnly()
    {
        var (save, trial, shape, _) = Open();
        var verdict = WriteGate.Admit(save, Good(shape, trial), [Row(save, trial, ThisImage, All.Where(f => f != "Ball"))]);
        Assert.True(verdict.Passed, verdict.Reason);
        Assert.Equal(WriteEvidence.GameAccepted, verdict.Effective);
        var model = new HackSaveModel(save);
        var lead = model.Edit(HackSlotRef.InParty(0))!;
        Assert.False(lead.CanEdit("Ball"));
        Assert.Equal(WriteGate.FieldNotAccepted, lead.LockedReason("Ball"));
        Assert.False(lead.CanEdit("Species"));
        lead.HeldItem = 7;
        Assert.Null(model.Apply(lead));
        Assert.True(save.Write().Confined);
    }

    [Fact]
    public void AFamilyRowFromAnotherImageIsConfined()
    {
        var (save, trial, shape, _) = Open();
        var verdict = WriteGate.Admit(save, Good(shape, trial), [Row(save, trial, new string('1', 40), All)]);
        Assert.Equal(WriteGate.ConfinedSentence, verdict.Reason);
        Assert.Equal(WriteEvidence.Confined, verdict.Effective);
        var (again, againTrial, againShape, _) = Open();
        Assert.True(WriteGate.Admit(again, Good(againShape, againTrial) with { OnDeviceCheck = true }, [Row(again, againTrial, new string('1', 40), All)]).Passed);
    }

    [Fact]
    public void ALiveSessionAndAWeakMatchStayReadOnly()
    {
        var (save, trial, shape, _) = Open();
        Assert.Equal(WriteGate.LiveSentence, WriteGate.Admit(save, Good(shape, trial) with { FromFile = false }, [Row(save, trial, ThisImage, All)]).Reason);
        Assert.Equal(WriteGate.MatchSentence, WriteGate.Admit(save, Good(shape, trial) with { MatchAllowsWrite = false }, [Row(save, trial, ThisImage, All)]).Reason);
        Assert.Equal(WriteGate.ListedMapSentence, WriteGate.Admit(save, Good(shape, trial) with { Shape = null }, [Row(save, trial, ThisImage, All)]).Reason);
    }

    [Fact]
    public void TheSelfCheckPassesARealEditAndCatchesAWrongValue()
    {
        var (save, trial, shape, file) = Open();
        WriteGate.Admit(save, Good(shape, trial), [Row(save, trial, ThisImage, All)]);
        var model = new HackSaveModel(save);
        var box = model.Edit(HackSlotRef.InBox(5, 3))!;
        box.HeldItem = 12;
        Assert.Null(model.Apply(box));
        var check = model.CheckedWrite(shape, file, out var bytes);
        Assert.True(check.Passed, check.ToString());
        Assert.NotNull(bytes);
        var plan = save.Plan();
        var wrong = model.Edits.Select(e => e with { Expected = e.Expected with { HeldItem = 13 } }).ToList();
        var failed = WriteSelfCheck.Before(save, shape, file, plan, WriteSelfCheck.Output(save, plan), wrong, null);
        Assert.Contains(failed.Failures, f => f.Contains("reads HeldItem 12", StringComparison.Ordinal));
        var disk = Array.Empty<byte>();
        var reads = 0;
        var undone = WriteSelfCheck.Commit(bytes!, file, file, b => disk = b, () =>
        {
            var copy = (byte[])disk.Clone();
            if (reads++ == 0)
                copy[0x200] ^= 1;
            return copy;
        });
        Assert.True(undone.Undone);
        Assert.Equal(file, disk);
        var writes = 0;
        var empty = WriteSelfCheck.Commit(bytes!, ReadOnlyMemory<byte>.Empty, file, _ => writes++, () => bytes!);
        var stale = (byte[])file.Clone();
        stale[0x100] ^= 1;
        var fromStale = WriteSelfCheck.Commit(bytes!, stale, file, _ => writes++, () => bytes!);
        Assert.Equal(0, writes);
        Assert.Equal(WriteSelfCheck.BackupUnusable, Assert.Single(empty.Failures));
        Assert.Equal(WriteSelfCheck.BackupUnusable, Assert.Single(fromStale.Failures));
    }

    [Fact]
    public void TheModelCommitWritesReadsBackAndUndoesThroughTheSameCheck()
    {
        var (save, trial, shape, file) = Open();
        WriteGate.Admit(save, Good(shape, trial), [Row(save, trial, ThisImage, All)]);
        var model = new HackSaveModel(save);
        var box = model.Edit(HackSlotRef.InBox(5, 3))!;
        box.HeldItem = 12;
        Assert.Null(model.Apply(box));
        Assert.True(model.CheckedWrite(shape, file, out var bytes).Passed);
        var disk = (byte[])file.Clone();
        var done = model.Commit(bytes!, file, disk, b => disk = b, () => (byte[])disk.Clone());
        Assert.True(done.Passed, done.ToString());
        Assert.False(done.Undone);
        Assert.Equal(bytes, disk);
        disk = (byte[])file.Clone();
        var reads = 0;
        var undone = model.Commit(bytes!, file, disk, b => disk = b, () =>
        {
            var copy = (byte[])disk.Clone();
            if (reads++ == 0)
                copy[0x200] ^= 1;
            return copy;
        });
        Assert.False(undone.Passed);
        Assert.True(undone.Undone);
        Assert.Equal(file, disk);
        var writes = 0;
        var refused = model.Commit(bytes!, ReadOnlyMemory<byte>.Empty, file, _ => writes++, () => bytes!);
        Assert.Equal(0, writes);
        Assert.Equal(WriteSelfCheck.BackupUnusable, Assert.Single(refused.Failures));
    }

    [Fact]
    public void ACheckedWriteNeedsTheGate()
    {
        var (shaped, _, _, file) = Open();
        Assert.True(shaped.ReadOnly);
        var h = ShapeSynth.Emerald;
        var literal = new ExpansionLayout(h.SaveBlock2Size, h.SaveBlock1Size, h.PartyCountOffset, h.PartyOffset, h.TrainerIdOffset, h.PlayerNameOffset, ShapeSynth.Pk3x);
        var save = ExpansionSave.TryOpen(file, literal, WriteEvidence.GameAccepted, out var why);
        Assert.True(save is not null, why);
        Assert.Null(save!.Gate);
        Assert.False(save.ReadOnly);
        var model = new HackSaveModel(save);
        var lead = model.Edit(HackSlotRef.InParty(0))!;
        lead.HeldItem = (ushort)(lead.HeldItem + 1);
        Assert.Null(model.Apply(lead));
        var check = model.CheckedWrite(null, file, out var bytes);
        Assert.False(check.Passed);
        Assert.Null(bytes);
        Assert.Contains(HackSaveModel.NotAdmitted, check.Failures);
    }

    [Fact]
    public void ARowMatchesSavesThatShareAChecksumRuleAndUseOnlyItsAreas()
    {
        var row = new AcceptanceRow(new AcceptanceKey(SaveShape.Expansion, "FullWindows+SectionSizes", "0xF80", "pk3x", "pk3x", "Storage"), "test.md", [ThisImage], ["HeldItem"]);
        Assert.Same(row, EngineAcceptance.Find(row.Key with { ChecksumRule = "SectionSizes" }, [row]));
        Assert.Same(row, EngineAcceptance.Find(row.Key with { Areas = EngineAcceptance.NoAreas }, [row]));
        Assert.Null(EngineAcceptance.Find(row.Key with { ChecksumRule = "UniformStride" }, [row]));
        Assert.Null(EngineAcceptance.Find(row.Key with { Areas = "SaveBlock2+Storage" }, [row]));
        Assert.Null(EngineAcceptance.Find(row.Key with { Stride = "0xFF0" }, [row]));
    }

    [Fact]
    public void ExpansionBoxSlotsAreWrittenIntoTheirOwnRecord()
    {
        var (save, _, shape, _) = Open();
        var current = save.Boxes[4].Slots[3]!;
        save.SetBoxSlot(5, 3, current with { Friendship = 9 });
        var plan = save.Plan();
        Assert.True(plan.Confined);
        var back = HackSaves.TryOpen(plan.Bytes, shape, WriteEvidence.Unproven, out _)!;
        Assert.Equal((byte?)9, back.Boxes[4].Slots[3]!.Friendship);
        Assert.Equal(save.Boxes[0].Slots[0]!.Raw.ToArray(), back.Boxes[0].Slots[0]!.Raw.ToArray());
        Assert.Throws<FaithfulWriteException>(() => save.SetBoxSlot(5, 4, current));
    }

    [Fact]
    public void EveryEncoderFieldRoundTripsWithinItsBits()
    {
        var pk = ShapeSynth.Mon(Species.Treecko, 0x1234_5678);
        var mon = RecordCodecs.For(RecordCodecId.Pk3x, "1.15").Decode(ShapeSynth.Stored(pk), out var why)!;
        Assert.True(mon is not null, why);
        foreach (var field in EditableFields.Encoded(RecordCodecId.Pk3x, RecordShape.Stored))
        {
            var edited = field.Field switch
            {
                "Species" => mon with { Species = (ushort)(mon.Species + 1) },
                "HeldItem" => mon with { HeldItem = 5 },
                "Exp" => mon with { Exp = mon.Exp + 1 },
                "Friendship" => mon with { Friendship = 1 },
                "Ball" => mon with { Ball = 3 },
                "Moves" => mon with { Moves = [2, 0, 0, 0] },
                "Evs" => mon with { Evs = mon.Evs with { Hp = 9 } },
                "Ivs" => mon with { Ivs = (mon.Ivs ?? default) with { Hp = 9 } },
                "IsEgg" => mon with { IsEgg = !mon.IsEgg },
                EditFieldSpecs.AbilitySlot => mon with { AbilitySlot = 1 },
                EditFieldSpecs.Nature => mon with { Nature = (byte)((mon.Nature!.Value + 1) % 25) },
                EditFieldSpecs.Nickname => mon with { Nickname = "LEAF" },
                _ => throw new InvalidOperationException(field.Field),
            };
            var bytes = Pk3xEncoder.Encode(edited);
            var back = RecordCodecs.For(RecordCodecId.Pk3x, "1.15").Decode(bytes, out _)!;
            Assert.Equal(WriteSelfCheck.FieldValue(edited, field.Field), WriteSelfCheck.FieldValue(back, field.Field));
            var before = Pk3xEncoder.Plain(mon.Raw.Span);
            var after = Pk3xEncoder.Plain(bytes);
            var mask = EncodedField.Mask(before.Length, field.Bits.Concat(Pk3xEncoder.Derived));
            Assert.All(Enumerable.Range(0, before.Length), i => Assert.Equal(0, (before[i] ^ after[i]) & ~mask[i]));
        }
        Assert.Throws<RecordEncodeException>(() => Pk3xEncoder.Encode(mon with { HeldItem = 0x400 }));
    }

    [Fact]
    public void AGhostAcceptanceRowIsReported()
    {
        var key = new AcceptanceKey(SaveShape.Expansion, "SectionSizes", "0xF80", "pk3x", "pk3x", "Storage");
        var problems = EngineAcceptance.Problems([new AcceptanceRow(key, "no-such-file.md", [ThisImage], ["HeldItem"])], AppContext.BaseDirectory);
        Assert.Contains(problems, p => p.Contains("does not exist", StringComparison.Ordinal));
    }
}
