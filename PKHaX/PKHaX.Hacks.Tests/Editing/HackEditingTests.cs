using System;
using System.Linq;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;
using PKHaX.Hacks.Tests.Saves;
using Xunit;

namespace PKHaX.Hacks.Tests.Editing;

public sealed class HackEditingTests
{
    private static byte[] File()
    {
        var party = CfruSaveTests.WithParty(Gen3ContainerTests.Cfru(CfruVariant.UnboundSignature, 11), CfruVariant.UnboundSignature, 123456,
            CfruSaveTests.PlainRecord(1022, 30), CfruSaveTests.PlainRecord(252, 5, pid: 0x0BAD_F00D));
        var junk = CfruSaveTests.CompactRecord(7);
        junk[0x12] = 0xEE;
        return CfruSaveTests.WithBoxes(party, CfruVariant.UnboundSignature, [(1, 0, CfruSaveTests.CompactRecord(25)), (1, 2, junk), (21, 21, CfruSaveTests.CompactRecord(133))], unbound: true);
    }

    private static HackSaveModel Model(byte[] file, WriteEvidence evidence) =>
        new(HackSaves.TryOpen(file, CfruVariant.Unbound.Id, evidence, out var why) ?? throw new InvalidOperationException(why));

    [Fact]
    public void AReadOnlySaveShowsEverythingAndWritesNothing()
    {
        var file = File();
        var m = Model(file, WriteEvidence.Unproven);
        Assert.False(m.CanWrite);
        Assert.Contains("read-only", m.Banner);
        Assert.Equal("123,456", m.MoneyLine);
        Assert.Equal(25, m.Boxes.Count);
        Assert.Equal(2, m.Party.Count(s => s.Mon is not null));
        Assert.EndsWith(" (#1022) Lv 30", m.Party[0].Title);
        Assert.Equal((ushort)25, m.Boxes[0].Slots[0].Mon!.Species);
        Assert.StartsWith("unreadable record", m.Boxes[0].Slots[2].Title);
        Assert.Equal("empty", m.Boxes[0].Slots[1].Title);
        Assert.Equal((ushort)133, m.Slot(HackSlotRef.InBox(21, 21))!.Mon!.Species);
        var edit = m.Edit(HackSlotRef.InBox(1, 0))!;
        edit.Species = 26;
        Assert.Contains("is not written", m.Apply(edit));
        Assert.Equal(file, m.Preview().Bytes);
        Assert.Null(m.Edit(HackSlotRef.InBox(1, 1)));
        Assert.Null(m.Edit(HackSlotRef.InBox(1, 2)));
        Assert.Null(m.Slot(HackSlotRef.InBox(26, 0)));
    }

    [Fact]
    public void AWritableSaveTakesBoxAndPartyEditsAndReadsThemBack()
    {
        var m = Model(File(), WriteEvidence.GameAccepted);
        Assert.True(m.CanWrite);
        Assert.Null(m.Banner);
        var box = m.Edit(HackSlotRef.InBox(21, 21))!;
        box.Species = 134;
        box.SetMove(1, 45);
        box.Evs = new StatSet(4, 0, 0, 0, 0, 252);
        Assert.Equal(["Species", "Moves", "Evs"], box.Changed);
        Assert.Null(m.Apply(box));
        var party = m.Edit(HackSlotRef.InParty(0))!;
        party.Friendship = 255;
        party.HeldItem = 1;
        Assert.Null(m.Apply(party));
        party.Ball = 2;
        Assert.Null(m.Apply(party));
        Assert.Empty(party.Changed);
        var back = Model(m.Write().Bytes, WriteEvidence.Unproven);
        var gotBox = back.Slot(HackSlotRef.InBox(21, 21))!.Mon!;
        Assert.Equal((ushort)134, gotBox.Species);
        Assert.Equal((ushort)45, gotBox.Moves[1]);
        Assert.Equal(new StatSet(4, 0, 0, 0, 0, 252), gotBox.Evs);
        Assert.EndsWith(" (#1022) Lv 30", back.Party[0].Title);
        Assert.Equal(((ushort)1, (byte?)255, (byte?)2), (back.Party[0].Mon!.HeldItem, back.Party[0].Mon!.Friendship, back.Party[0].Mon!.Ball));
        Assert.Equal((ushort)25, back.Boxes[0].Slots[0].Mon!.Species);
    }

    [Fact]
    public void OnlyTheFieldsTheEncoderWritesAreEditable()
    {
        var m = Model(File(), WriteEvidence.GameAccepted);
        var box = m.Edit(HackSlotRef.InBox(1, 0))!;
        Assert.False(box.CanEdit(EditableFields.Level));
        Assert.Contains("follows EXP", box.LockedReason(EditableFields.Level));
        Assert.Throws<InvalidOperationException>(() => box.Level = 9);
        Assert.True(box.CanEdit("Exp"));
        Assert.False(box.CanEdit("Pp"));
        var party = m.Edit(HackSlotRef.InParty(0))!;
        Assert.False(party.CanEdit("Pp"));
        Assert.False(party.CanEdit("Exp"));
        Assert.False(party.CanEdit(EditableFields.Level));
        Assert.Contains("would not be recalculated", party.LockedReason("Exp"));
        Assert.Throws<InvalidOperationException>(() => party.Exp = 1);
        Assert.All(party.Editable.Concat(box.Editable), f => Assert.Contains(f, EditableFields.Modelled));
        Assert.Throws<InvalidOperationException>(() => party.Friendship = null);
        Assert.Throws<InvalidOperationException>(() => box.Ball = null);
        Assert.Contains(EditFieldSpecs.Nature, EditableFields.For(RecordCodecId.Pk3x));
        Assert.Null(EditableFields.WhyNone(RecordCodecId.Pk3x));
        Assert.Contains("view-only", EditableFields.WhyNone(RecordCodecId.Pk6));
        Assert.Null(EditableFields.WhyNone(RecordCodecId.Pk3CfruCompact58));
    }

    [Fact]
    public void UndoneEditsAndRefusalsLeaveTheFileAlone()
    {
        var file = File();
        var m = Model(file, WriteEvidence.GameAccepted);
        var box = m.Edit(HackSlotRef.InBox(1, 0))!;
        box.Species = 26;
        box.Species = 25;
        box.SetMove(0, 99);
        box.SetMove(0, box.Original.Moves[0]);
        Assert.Empty(box.Changed);
        Assert.Null(m.Apply(box));
        var outside = Assert.Throws<InvalidOperationException>(() => box.SetMove(3, 1024));
        Assert.Contains("1023", outside.Message);
        Assert.Empty(box.Changed);
        Assert.Null(m.Apply(box));
        Assert.Equal(file, m.Preview().Bytes);
        var stale = m.Edit(HackSlotRef.InBox(1, 0))!;
        var fresh = m.Edit(HackSlotRef.InBox(1, 0))!;
        fresh.HeldItem = 4;
        Assert.Null(m.Apply(fresh));
        stale.HeldItem = 5;
        Assert.Contains("changed since", m.Apply(stale));
        Assert.Equal((ushort)4, Model(m.Preview().Bytes, WriteEvidence.Unproven).Slot(HackSlotRef.InBox(1, 0))!.Mon!.HeldItem);
        Assert.Throws<ArgumentOutOfRangeException>(() => box.SetMove(4, 1));
    }

    [Fact]
    public void ARefusedPartyRecordShowsItsReasonAndAPartyEditKeepsItsGuard()
    {
        var bad = CfruSaveTests.PlainRecord(252, 5, pid: 0x0BAD_F00D);
        bad[0x12] = 0xEE;
        var file = CfruSaveTests.WithParty(Gen3ContainerTests.Cfru(CfruVariant.UnboundSignature, 11), CfruVariant.UnboundSignature, 5, CfruSaveTests.PlainRecord(1022, 30), bad);
        var m = Model(file, WriteEvidence.GameAccepted);
        Assert.StartsWith("unreadable record", m.Party[1].Title);
        Assert.Equal("empty", m.Party[2].Title);
        Assert.Contains("does not read", m.WhyNotEditable(HackSlotRef.InParty(1)));
        Assert.Null(m.WhyNotEditable(HackSlotRef.InParty(0)));
        var stale = m.Edit(HackSlotRef.InParty(0))!;
        var fresh = m.Edit(HackSlotRef.InParty(0))!;
        fresh.HeldItem = 9;
        Assert.Null(m.Apply(fresh));
        stale.HeldItem = 8;
        Assert.Contains("changed since", m.Apply(stale));
        Assert.Equal((ushort)9, Model(m.Preview().Bytes, WriteEvidence.Unproven).Party[0].Mon!.HeldItem);
    }

    [Fact]
    public void RadicalRedBoxesSayUpFrontThatTheyAreNotWritten()
    {
        var rr = CfruSaveTests.WithBoxes(CfruSaveTests.WithParty(Gen3ContainerTests.Cfru(GbaSectorContainer.RetailSignature, 5), GbaSectorContainer.RetailSignature, 0, CfruSaveTests.PlainRecord(25, 9)),
            GbaSectorContainer.RetailSignature, [(1, 0, CfruSaveTests.CompactRecord(1))], unbound: false);
        var m = new HackSaveModel(HackSaves.TryOpen(rr, CfruVariant.RadicalRed.Id, WriteEvidence.GameAccepted, out var why) ?? throw new InvalidOperationException(why));
        Assert.Contains("not written until", m.WhyNotEditable(HackSlotRef.InBox(1, 0)));
        Assert.Null(m.WhyNotEditable(HackSlotRef.InParty(0)));
        var edit = m.Edit(HackSlotRef.InBox(1, 0))!;
        edit.HeldItem = 3;
        Assert.Contains("not written until", m.Apply(edit));
        Assert.Equal(rr, m.Preview().Bytes);
    }
}
