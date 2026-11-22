using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class SaveShapeTests
{
    private const uint Second = 0x01121999;

    internal static byte[] Rom(bool rhh, bool header = true, string code = "BPEE", uint? wordAt0x300 = null, int wordPlace = 0x300, uint[]? boxTable = null)
    {
        var rom = new byte[0x1000];
        System.Text.Encoding.ASCII.GetBytes(code).CopyTo(rom, 0xAC);
        rom[0xB2] = 0x96;
        if (rhh)
        {
            "RHHEXP"u8.CopyTo(rom.AsSpan(0x204));
            rom[0x20A] = 1;
            rom[0x20B] = 15;
        }
        if (header)
        {
            var h = ShapeSynth.Emerald;
            void W(int field, int value) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(GfRomHeader.At + field), (uint)value);
            W(0x88, h.SaveBlock2Size);
            W(0x8C, h.SaveBlock1Size);
            W(0x90, h.PartyCountOffset);
            W(0x94, h.PartyOffset);
            W(0x9C, h.TrainerIdOffset);
            W(0xA0, h.PlayerNameOffset);
        }
        if (wordAt0x300 is { } word)
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(wordPlace), word);
        for (var i = 0; boxTable is not null && i < boxTable.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x400 + (i * 4)), boxTable[i]);
        return rom;
    }

    internal static uint[] CfruTable()
    {
        var map = CfruRamMap.FireRed10;
        (uint First, int Count)[] runs = [(map.Storage + 4, 19), (map.ExtraSectors + 0xB0C, 3), (map.SaveBlock1 + 0x1F08, 2), (map.SaveBlock2 + 0xB0, 1)];
        return [.. runs.SelectMany(r => Enumerable.Range(0, r.Count).Select(i => r.First + (uint)(i * CfruBoxTable.BoxBytes)))];
    }

    [Fact]
    public void TheFamilyComesFromTheImage()
    {
        var expansion = Gen3ShapeFinder.Derive(Rom(rhh: true));
        Assert.Equal((SaveShape.Expansion, ShapeTier.Derived), (expansion.Family, expansion.FamilyTier));
        Assert.Equal(ShapeSynth.Emerald, Assert.Single(expansion.Headers));
        Assert.Equal(new RecordCodecKey(RecordCodecId.Pk3x, "1.15"), expansion.PartyCodecCandidates[0]);
        Assert.Equal(new[] { GbaSectorContainer.RetailSignature }, expansion.Signatures.ToArray());
        Assert.Null(expansion.FixedStride);

        var cfru = Gen3ShapeFinder.Derive(Rom(rhh: false, header: false, code: "BPRE", boxTable: CfruTable()));
        Assert.Equal(SaveShape.Cfru, cfru.Family);
        Assert.Equal(Gen3ShapeFinder.FireRedHeader, Assert.Single(cfru.Headers));
        Assert.Equal(SectionLayout.CfruStride, cfru.FixedStride);
        Assert.Equal(0x83D0, cfru.FixedStorageSize);
        Assert.Equal(ShapeTier.Family, cfru.LayoutTier);
        Assert.Equal(new[] { 19, 3, 2, 1 }, cfru.BoxSources.Select(b => b.Count).ToArray());
        Assert.Equal(new[] { CfruBoxArea.Storage, CfruBoxArea.Sectors30And31, CfruBoxArea.SaveBlock1, CfruBoxArea.SaveBlock2 }, cfru.BoxSources.Select(b => b.Area).ToArray());
        Assert.True(cfru.BoxSources[3].NeedsSaveCheck);
        Assert.False(cfru.BoxSources[2].WritableFromRomAlone);

        var retail = Gen3ShapeFinder.Derive(Rom(rhh: false));
        Assert.Equal(SaveShape.Retail, retail.Family);
        Assert.Contains(Gen3ShapeFinder.RetailPk3, retail.PartyCodecCandidates);

        var none = Gen3ShapeFinder.Derive(Rom(rhh: false, header: false));
        Assert.False(none.Known);
        Assert.Contains("no RHHEXP header", none.Reason);
        Assert.False(Gen3ShapeFinder.Derive(new byte[0x1000]).Known);
    }

    [Fact]
    public void TheShapeRoundTripsThroughJson()
    {
        var shape = Gen3ShapeFinder.Derive(Rom(rhh: false, header: false, code: "BPRE", boxTable: CfruTable())) with
        {
            Money = new ShapeMoney(0x290, 0xF20, "census", ShapeTier.Derived),
            Bag = new ShapeBag([new ShapePocket(0x310, 42), new ShapePocket(0x3B8, 30)], "census", ShapeTier.Derived),
            Signatures = [GbaSectorContainer.RetailSignature, 0xFEDC_BA98],
        };
        var json = shape.ToJson();
        var back = SaveShape.FromJson(json, shape.RomSha1);
        Assert.NotNull(back);
        Assert.Equal(json, back!.ToJson());
        Assert.Equal(new[] { GbaSectorContainer.RetailSignature, 0xFEDC_BA98 }, back.Signatures.ToArray());
        Assert.Equal(0x290, back.Money!.Offset);
        Assert.Equal(0xF20, back.Money.KeyOffset);
        Assert.Null(SaveShape.FromJson(json, new string('1', 40)));
        Assert.Null(SaveShape.FromJson("{", shape.RomSha1));
        Assert.Null(SaveShape.FromJson((shape with { Version = SaveShape.CurrentVersion - 1 }).ToJson(), shape.RomSha1));
        Assert.NotNull(SaveShape.FromJson((shape with { Version = SaveShape.CurrentVersion - 1 }).ToJson(), shape.RomSha1, null));
    }

    [Fact]
    public void TheCacheSitsBesideTheTablesAndAnOlderVersionIsRebuilt()
    {
        var root = Path.Combine(Path.GetTempPath(), "pkhax-shape-" + Guid.NewGuid().ToString("N"));
        try
        {
            var rom = Rom(rhh: true);
            var first = SaveShapeCache.ReadOrBuild(root, rom, out var built);
            Assert.True(built);
            var path = SaveShapeCache.PathFor(root, first.RomSha1);
            Assert.True(File.Exists(path));
            Assert.Equal(Path.GetDirectoryName(HackDataCache.PathFor(root, first.RomSha1)), Path.GetDirectoryName(path));
            Assert.Equal(new[] { path }, Directory.GetFiles(root, "*", SearchOption.AllDirectories));
            var second = SaveShapeCache.ReadOrBuild(root, rom, out built);
            Assert.False(built);
            Assert.Equal(first.ToJson(), second.ToJson());
            File.WriteAllText(path, (first with { Version = SaveShape.CurrentVersion - 1, Family = SaveShape.Retail }).ToJson());
            Assert.Null(SaveShapeCache.Load(root, first.RomSha1));
            var third = SaveShapeCache.ReadOrBuild(root, rom, out built);
            Assert.True(built);
            Assert.Equal((SaveShape.CurrentVersion, SaveShape.Expansion), (third.Version, third.Family));
            Assert.Equal(SaveShape.Expansion, SaveShapeCache.Load(root, first.RomSha1)!.Family);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ASecondSignatureNeedsTheSlotAndTheRom()
    {
        var file = ShapeSynth.File(0xF80, true, [(1, 0), (5, 3)], signature: Second);
        var with = new GbaImage(Rom(rhh: true, wordAt0x300: Second));
        var without = new GbaImage(Rom(rhh: true));
        var unaligned = new GbaImage(Rom(rhh: true, wordAt0x300: Second, wordPlace: 0x301));
        var shape = Gen3ShapeFinder.Derive(with);
        Assert.Null(HackSaves.TryOpen(file, shape, WriteEvidence.Unproven, out var refused));
        Assert.Contains("0x01121999", refused);
        var accepted = Gen3ShapeFinder.AcceptSignature(shape, with, file, out var why);
        Assert.True(accepted is not null, why);
        Assert.Contains(Second, accepted!.Signatures);
        Assert.NotNull(HackSaves.TryOpen(file, accepted, WriteEvidence.Unproven, out why));
        Assert.Null(Gen3ShapeFinder.AcceptSignature(Gen3ShapeFinder.Derive(without), without, file, out why));
        Assert.Contains("word-aligned", why);
        Assert.Null(Gen3ShapeFinder.AcceptSignature(Gen3ShapeFinder.Derive(unaligned), unaligned, file, out why));
        Assert.Contains("word-aligned", why);
        var mixed = (byte[])file.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(mixed.AsSpan((3 * SectionLayout.SectorSize) + GbaSectorContainer.SignatureOffset), GbaSectorContainer.RetailSignature);
        Assert.Null(Gen3ShapeFinder.AcceptSignature(shape, with, mixed, out why));
        Assert.Contains("do not carry one signature", why);
        Assert.Null(Gen3ShapeFinder.AcceptSignature(shape, without, file, out why));
        Assert.Contains("not the one", why);
        Assert.True(Gen3ShapeFinder.WordInRom(with.Span, Second));
        Assert.False(Gen3ShapeFinder.WordInRom(unaligned.Span, Second));
    }

    [Fact]
    public void ACfruShapeOpensItsOwnSave()
    {
        var shape = Gen3ShapeFinder.Derive(Rom(rhh: false, header: false, code: "BPRE", boxTable: CfruTable()));
        var seed = Saves.Gen3ContainerTests.Cfru(GbaSectorContainer.RetailSignature, 5);
        var c = GbaSectorContainer.TryOpen(seed, SectionLayout.Cfru, ChecksumPolicy.FromLayout(SectionLayout.Cfru), [GbaSectorContainer.RetailSignature], out var why) ?? throw new InvalidOperationException(why);
        var storage = new byte[SectionLayout.Cfru.BlockSize(SaveBlock.Storage)];
        Saves.CfruSaveTests.CompactRecord(25).CopyTo(storage, 4);
        Saves.CfruSaveTests.CompactRecord(150).CopyTo(storage, 4 + (18 * CfruBoxTable.BoxBytes) + (29 * CfruBoxTable.RecordSize));
        BlockEdit[] edits =
        [
            new(SaveBlock.Large, 0x34, new byte[4 + (6 * 100)]),
            new(SaveBlock.Large, 0x1F08, new byte[2 * CfruBoxTable.BoxBytes]),
            new(SaveBlock.Small, 0xB0, new byte[CfruBoxTable.BoxBytes]),
            new(SaveBlock.Storage, 0, storage),
        ];
        var file = FaithfulWriter.Patch(c, edits, [.. edits.Select(e => new BlockWindow(e.Block, e.Offset, e.Bytes.Length))]).Bytes;
        Array.Clear(file, 30 * SectionLayout.SectorSize, 2 * SectionLayout.SectorSize);
        var save = HackSaves.TryOpen(file, shape, WriteEvidence.Unproven, out why, out var trial);
        Assert.True(save is not null, why);
        Assert.True(trial.Settled);
        Assert.Equal(CfruSave.ShapeVariantId, save!.Variant);
        Assert.Equal(25, save.Boxes.Count);
        Assert.Equal((ushort)25, save.Boxes[0].Slots[0]!.Species);
        Assert.Equal((ushort)150, save.Boxes[18].Slots[29]!.Species);
        Assert.Null(save.BoxesReason);
        Assert.Null(save.BoxWriteRefusal(1));
        Assert.NotNull(save.BoxWriteRefusal(23));
        Assert.NotNull(save.BoxWriteRefusal(25));
        Assert.NotNull(save.ReadOnlyReason);

        var junk = (byte[])file.Clone();
        var cj = GbaSectorContainer.TryOpen(junk, SectionLayout.Cfru, ChecksumPolicy.FromLayout(SectionLayout.Cfru), [GbaSectorContainer.RetailSignature], out why) ?? throw new InvalidOperationException(why);
        BlockEdit[] stray = [new(SaveBlock.Small, 0xB0, Enumerable.Repeat((byte)0x5A, CfruBoxTable.BoxBytes).ToArray())];
        var strayFile = FaithfulWriter.Patch(cj, stray, [new BlockWindow(SaveBlock.Small, 0xB0, CfruBoxTable.BoxBytes)]).Bytes;
        var lone = HackSaves.TryOpen(strayFile, shape, WriteEvidence.Unproven, out why);
        Assert.True(lone is not null, why);
        Assert.Equal(24, lone!.Boxes.Count);
        Assert.Contains("lone pointer", lone.BoxesReason);
    }
}
