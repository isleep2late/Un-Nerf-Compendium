using System;
using System.Linq;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Expansion;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class ExpansionBoxTests
{
    private static readonly (int Box, int Slot)[] Boxed = [(1, 0), (5, 3), (14, 29)];

    private static void AssertBoxes(HackSave save)
    {
        Assert.Null(save.BoxesReason);
        Assert.Equal(14, save.Boxes.Count);
        Assert.All(save.Boxes, b => Assert.Equal(30, b.Slots.Count));
        var internalId = SpeciesConverter.GetInternal3((ushort)Species.Mudkip);
        foreach (var (box, slot) in Boxed)
            Assert.Equal(internalId, save.Boxes[box - 1].Slots[slot]!.Species);
        Assert.Equal(Boxed.Length, save.Boxes.Sum(b => b.Slots.Count(s => s is not null)));
        Assert.Equal(0, save.Boxes.Sum(b => b.Undecodable));
        Assert.All(save.Boxes, b => Assert.Equal(RecordShape.Stored, b.Slots.FirstOrDefault(s => s is not null)?.Shape ?? RecordShape.Stored));
    }

    [Fact]
    public void BoxesAreReadFromStoragePlusFourThroughTheShape()
    {
        var save = HackSaves.TryOpen(ShapeSynth.File(0xF80, true, Boxed), ShapeSynth.Shape(), WriteEvidence.Unproven, out var why);
        Assert.True(save is not null, why);
        AssertBoxes(save!);
    }

    [Fact]
    public void BoxesAreReadThroughTheLayoutPathToo()
    {
        var h = ShapeSynth.Emerald;
        var layout = new ExpansionLayout(h.SaveBlock2Size, h.SaveBlock1Size, h.PartyCountOffset, h.PartyOffset, h.TrainerIdOffset, h.PlayerNameOffset, ShapeSynth.Pk3x);
        var save = ExpansionSave.TryOpen(ShapeSynth.File(0xF80, true, Boxed), layout, WriteEvidence.Unproven, out var why);
        Assert.True(save is not null, why);
        Assert.Equal(14, layout.BoxCount);
        AssertBoxes(save!);
    }

    [Fact]
    public void RecordsWrittenAtStoragePlusZeroAreNotBoxes()
    {
        var save = HackSaves.TryOpen(ShapeSynth.File(0xF80, true, Boxed, boxesAt: 0), ShapeSynth.Shape(), WriteEvidence.Unproven, out _);
        Assert.True(save is null || save.Boxes.Sum(b => b.Slots.Count(s => s is not null)) == 0);
    }

    [Fact]
    public void AShapeSaveHasABoxWriterButStaysReadOnlyWithoutTheGate()
    {
        var file = ShapeSynth.File(0xF80, true, Boxed);
        var opened = HackSaves.TryOpen(file, ShapeSynth.Shape(), WriteEvidence.GameAccepted, out var why);
        Assert.True(opened is not null, why);
        Assert.Null(opened!.BoxWriteRefusal(1));
        Assert.Null(opened.BoxWriteRefusal(14));
        Assert.Equal(14, opened.Boxes.Count);
        Assert.Contains("has boxes 1 to 14", opened.BoxWriteRefusal(15));
        Assert.Null(opened.Gate);
        Assert.True(opened.ReadOnly);
        Assert.Equal(WriteEvidence.Confined, opened.Evidence);
        var model = new HackSaveModel(opened);
        Assert.False(model.CanWrite);
        Assert.Contains("read-only", model.WhyNotEditable(HackSlotRef.InBox(1, 0)));
        var edit = model.Edit(HackSlotRef.InBox(1, 0))!;
        edit.HeldItem = (ushort)(edit.HeldItem == 4 ? 5 : 4);
        Assert.Contains("is not written", model.Apply(edit));
        Assert.Equal(file, model.Preview().Bytes);
    }

    [Fact]
    public void TheSoulGoldBoxRecordGivesItsOwnStorageSize()
    {
        var soulGold = new ExpansionLayout(0xF2C, 0x3D88, 0x234, 0x238, 0xA, 0, new RecordCodecKey(RecordCodecId.Pk3SoulGold96, null));
        Assert.Equal(0x4C, soulGold.BoxRecordSize);
        Assert.Equal(80, (soulGold with { Codec = ShapeSynth.Pk3x }).BoxRecordSize);
        Assert.Equal(0x83D0, ExpansionLayout.StorageSizeFor(14, 80));
        Assert.Equal(0x7D40, ExpansionLayout.StorageSizeFor(14, 0x4C));
        Assert.Equal((14, 0x7D40), SectorTrial.StorageFor(0xF80, soulGold.BoxRecordSize)!.Value);
        Assert.Equal(14, (soulGold with { StorageSize = 0x7D40 }).BoxCount);
    }
}
