using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

internal static class ShapeSynth
{
    public const int BoxRecord = 80;
    public const int MoneyAt = 0x1000;

    public static readonly ShapeHeader Emerald = new(0, 0xF2C, 0x3D88, 0x234, 0x238, 0xA, 0);
    public static readonly RecordCodecKey Pk3x = new(RecordCodecId.Pk3x, "1.15");

    public static SaveShape Shape(bool money = false, params uint[] extraSignatures) => new()
    {
        RomSha1 = new string('0', 40),
        Family = SaveShape.Expansion,
        FamilyTier = ShapeTier.Derived,
        Headers = [Emerald],
        PartyCodecCandidates = [Pk3x],
        LayoutTier = ShapeTier.Derived,
        Signatures = [GbaSectorContainer.RetailSignature, .. extraSignatures],
        Money = money ? new ShapeMoney(MoneyAt, null, "a test offset past the first SaveBlock1 section", ShapeTier.Derived) : null,
    };

    public static PK3 Mon(Species species, uint pid)
    {
        var pk = new PK3
        {
            Species = (ushort)species, PID = pid, TID16 = 1, SID16 = 2, Nickname = "SYNTH", OriginalTrainerName = "PKHAX", Language = (int)LanguageID.English,
            EXP = 135, Move1 = 1, Move1_PP = 35, OriginalTrainerFriendship = 70, Ball = 4,
        };
        pk.Stat_Level = 5;
        pk.ResetPartyStats();
        return pk;
    }

    public static byte[] Party(PK3 pk)
    {
        var bytes = new byte[pk.SIZE_PARTY];
        pk.WriteEncryptedDataParty(bytes);
        return bytes;
    }

    public static byte[] Stored(PK3 pk)
    {
        var bytes = new byte[pk.SIZE_STORED];
        pk.WriteEncryptedDataStored(bytes);
        return bytes;
    }

    public static int StorageSize(int stride) => SectorTrial.StorageFor(stride, BoxRecord)!.Value.Size;

    public static byte[] File(int stride, bool sizesRule, IEnumerable<(int Box, int Slot)> boxed, bool insideTail = false, uint signature = GbaSectorContainer.RetailSignature, int boxesAt = 4, int seed = 7, (int Box, int Slot)? corrupt = null)
    {
        var layout = SectionLayout.FromBlockSizes("synthetic", Emerald.SaveBlock2Size, Emerald.SaveBlock1Size, StorageSize(stride), stride);
        var rng = new Random(seed);
        var small = new byte[layout.BlockSize(SaveBlock.Small)];
        var large = new byte[layout.BlockSize(SaveBlock.Large)];
        var storage = new byte[layout.BlockSize(SaveBlock.Storage)];
        rng.NextBytes(large);
        large.AsSpan(Emerald.PartyCountOffset, 4 + (6 * 100)).Clear();
        large[Emerald.PartyCountOffset] = 1;
        Party(Mon(Species.Treecko, 0x1234_5678)).CopyTo(large, Emerald.PartyOffset);
        rng.NextBytes(storage.AsSpan(storage.Length - 140));
        var n = 0;
        foreach (var (box, slot) in boxed)
            Stored(Mon(Species.Mudkip, 0x0BAD_F00Du + (uint)n++)).CopyTo(storage, boxesAt + ((((box - 1) * 30) + slot) * BoxRecord));
        if (corrupt is { } bad)
            storage[boxesAt + ((((bad.Box - 1) * 30) + bad.Slot) * BoxRecord) + 0x30] ^= 0xFF;
        var policy = sizesRule ? ChecksumPolicy.FromLayout(layout) : ChecksumPolicy.Uniform("synthetic uniform", stride);
        var file = new byte[GbaSectorContainer.FileSize];
        for (var id = 0; id < SectionLayout.SectionCount; id++)
        {
            var sector = file.AsSpan(id * SectionLayout.SectorSize, SectionLayout.SectorSize);
            var slotOf = layout.Slots[id];
            var block = SectionLayout.BlockOf(id) switch
            {
                SaveBlock.Small => small,
                SaveBlock.Large => large,
                _ => storage,
            };
            block.AsSpan(slotOf.ToAdd, slotOf.Size).CopyTo(sector);
            if (insideTail && slotOf.Size + 4 <= stride)
                sector.Slice(slotOf.Size, 4).Fill(0x5A);
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.IdOffset..], (ushort)id);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.SignatureOffset..], signature);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.CounterOffset..], 2);
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.ChecksumOffset..], policy.Compute(id, sector));
        }
        return file;
    }
}

public class SectorTrialTests
{
    private static readonly (int Box, int Slot)[] Late = [(1, 0), (5, 3)];
    private static readonly (int Box, int Slot)[] Straddling = [(1, 0), (2, 19)];
    private static readonly (int Box, int Slot)[] Early = [(1, 0)];
    private static readonly (int Box, int Slot)[] Corrupted = [(1, 0), (14, 28)];

    [Fact]
    public void TheStrideRangeAndStorageSizeAreComputed()
    {
        Assert.Equal((0xF5C, 0xFF4), (SectorTrial.Strides(0x3D68)[0], SectorTrial.Strides(0x3D68)[^1]));
        Assert.Equal(0xFC4, SectorTrial.Strides(0x3F08)[0]);
        Assert.All(SectorTrial.Strides(0x3D88), s => Assert.Equal(0, s % 4));
        Assert.Equal((14, 0x83D0), SectorTrial.StorageFor(0xF80, 80)!.Value);
        Assert.Equal((14, 0x7D40), SectorTrial.StorageFor(0xF80, 0x4C)!.Value);
        Assert.Null(SectorTrial.StorageFor(0, 80));
    }

    [Theory]
    [InlineData(0xF80)]
    [InlineData(0xFF0)]
    [InlineData(0xFA0)]
    public void ASaveSettlesToItsOwnStride(int stride)
    {
        var trial = SectorTrial.Run(ShapeSynth.File(stride, true, Late), ShapeSynth.Shape());
        Assert.Null(trial.Refusal);
        Assert.True(trial.Settled, string.Join(" | ", trial.Survivors.Select(c => c.Name)));
        Assert.Equal(stride, trial.Primary!.Stride);
        Assert.Equal(14, trial.Primary.Boxes);
    }

    [Fact]
    public void AZeroPaddedSaveIsSettledByAStraddlingRecord()
    {
        var trial = SectorTrial.Run(ShapeSynth.File(0xF80, false, Straddling, insideTail: true), ShapeSynth.Shape());
        Assert.Null(trial.Refusal);
        Assert.True(trial.Checksummed.Select(c => c.Stride).Distinct().Count() > 1);
        Assert.True(trial.Settled, string.Join(" | ", trial.Survivors.Select(c => c.Name)));
        Assert.Equal(0xF80, trial.Primary!.Stride);
        Assert.True(trial.Witnesses >= 1);
    }

    [Fact]
    public void AnEarlySaveKeepsSeveralLayoutsAndAPartyEditStillPlans()
    {
        var file = ShapeSynth.File(0xF80, false, Early, insideTail: true);
        var save = HackSaves.TryOpen(file, ShapeSynth.Shape(), WriteEvidence.GameAccepted, out var why, out var trial);
        Assert.True(save is not null, why);
        Assert.True(trial.Layouts > 1);
        var tied = Assert.IsType<TiedSave>(save);
        Assert.Equal(0xF80, trial.Primary!.Stride);
        var mon = tied.Party[0]!;
        tied.SetPartySlot(0, mon with { Evs = mon.Evs with { Hp = 8 } });
        var plan = tied.Plan();
        Assert.True(plan.Confined);
        Assert.NotEmpty(plan.DiffRanges);
    }

    [Fact]
    public void AnEditPastTheFirstSectionIsRefusedUntilTheLayoutIsSettled()
    {
        var shape = ShapeSynth.Shape(money: true);
        var early = HackSaves.TryOpen(ShapeSynth.File(0xF80, false, Early, insideTail: true), shape, WriteEvidence.GameAccepted, out var why);
        Assert.True(early is TiedSave, why);
        early!.SetMoney(1234);
        var refused = Assert.Throws<FaithfulWriteException>(() => early.Plan());
        Assert.Equal(SectorTrial.TieSentence, refused.Message);
        var settled = HackSaves.TryOpen(ShapeSynth.File(0xF80, false, Straddling, insideTail: true), shape, WriteEvidence.GameAccepted, out why, out var trial);
        Assert.True(settled is not null && trial.Settled, why);
        settled!.SetMoney(1234);
        Assert.True(settled.Plan().Confined);
        Assert.Equal(1234u, settled.Money);
    }

    [Fact]
    public void ARefusedRecordPastTheFirstSectionDoesNotSettleTheLayout()
    {
        var file = ShapeSynth.File(0xF80, false, Corrupted, insideTail: true, corrupt: (14, 28));
        var save = HackSaves.TryOpen(file, ShapeSynth.Shape(money: true), WriteEvidence.Unproven, out var why, out var trial);
        Assert.True(save is TiedSave, why);
        Assert.True(trial.Layouts > 1);
        Assert.Contains(trial.Survivors, c => c.Stride == 0xF80);
        Assert.Equal(0xF80, trial.Primary!.Stride);
        save!.SetMoney(1234);
        var refused = Assert.Throws<FaithfulWriteException>(() => save.Plan());
        Assert.Equal(SectorTrial.TieSentence, refused.Message);
    }

    [Fact]
    public void AFooterIsTrimmedByTheLibrary()
    {
        var file = ShapeSynth.File(0xF80, true, Late);
        var withFooter = new byte[file.Length + 16];
        file.CopyTo(withFooter, 0);
        withFooter.AsSpan(file.Length).Fill(0xA5);
        Assert.Equal(16, GbaSectorContainer.FooterLength(withFooter));
        Assert.Equal(GbaSectorContainer.FileSize, GbaSectorContainer.TrimFooter(withFooter).Length);
        var save = HackSaves.TryOpen(withFooter, ShapeSynth.Shape(), WriteEvidence.Unproven, out var why, out var trial);
        Assert.True(save is not null, why);
        Assert.Equal(16, trial.FooterBytes);
        Assert.True(save!.Plan().Bytes.AsSpan().SequenceEqual(file));
        Assert.Equal(16, save.Footer.Length);
        Assert.True(save.Footer.Span.IndexOfAnyExcept((byte)0xA5) < 0);
        Assert.Null(HackSaves.TryOpen(new byte[GbaSectorContainer.FileSize + 0x41], ShapeSynth.Shape(), WriteEvidence.Unproven, out why));
        Assert.Contains("bytes", why);
    }

    [Fact]
    public void ASlotOfFifteenSectionsIsRefused()
    {
        var file = ShapeSynth.File(0xF80, true, Late);
        var extra = file.AsSpan(14 * SectionLayout.SectorSize, SectionLayout.SectorSize);
        BinaryPrimitives.WriteUInt16LittleEndian(extra[GbaSectorContainer.IdOffset..], 14);
        BinaryPrimitives.WriteUInt32LittleEndian(extra[GbaSectorContainer.SignatureOffset..], GbaSectorContainer.RetailSignature);
        BinaryPrimitives.WriteUInt32LittleEndian(extra[GbaSectorContainer.CounterOffset..], 2);
        Assert.Equal(15, GbaSectorContainer.NewestSectionIds(file, [GbaSectorContainer.RetailSignature]));
        Assert.Null(HackSaves.TryOpen(file, ShapeSynth.Shape(), WriteEvidence.Unproven, out var why));
        Assert.Equal(SectorTrial.SectionsSentence(15), why);
    }

    [Fact]
    public void AnUnknownShapeAndAForeignFileAreRefused()
    {
        Assert.Null(HackSaves.TryOpen(ShapeSynth.File(0xF80, true, Late), SaveShape.Unknown(new string('0', 40), "nothing derived"), WriteEvidence.Unproven, out var why));
        Assert.Contains("nothing derived", why);
        Assert.Null(HackSaves.TryOpen(new byte[GbaSectorContainer.FileSize], ShapeSynth.Shape(), WriteEvidence.Unproven, out why));
        Assert.Contains("signature", why);
        Assert.Null(HackSaves.TryOpen(ShapeSynth.File(0xF80, true, Late, signature: 0x01121999), ShapeSynth.Shape(), WriteEvidence.Unproven, out why));
        Assert.Contains("0x01121999", why);
        Assert.NotNull(HackSaves.TryOpen(ShapeSynth.File(0xF80, true, Late, signature: 0x01121999), ShapeSynth.Shape(false, 0x01121999), WriteEvidence.Unproven, out why));
    }
}
