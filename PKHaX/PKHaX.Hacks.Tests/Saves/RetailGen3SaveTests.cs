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

namespace PKHaX.Hacks.Tests.Saves;

internal static class RetailFixture
{
    public const string Source = "records from PKHeX's PK3 encryptor (pokefirered include/pokemon.h:105-141) with the species word set past the stock table; save blocks as pokefirered include/global.h:327-333 and :770-773 and pokeemerald include/global.h:999-1001 place them, PokemonStorage boxes at +4 (pokefirered include/pokemon_storage_system.h:44-50), sectors as include/save.h:8-24 and src/save.c:182 write them (zeroed past each section), checksums by SectionLayout section sizes (src/save.c:614 CalculateChecksum)";

    public static readonly ShapeHeader FireRed = new(0, 0xF24, 0x3D68, 0x34, 0x38, 0xA, 0);
    public static readonly ShapeHeader Emerald = new(0, 0xF2C, 0x3D88, 0x234, 0x238, 0xA, 0);

    public static byte[] Record(ushort internalSpecies, uint pid, bool party)
    {
        var pk = new PK3
        {
            Species = (ushort)Species.Bulbasaur, PID = pid, TID16 = 1, SID16 = 2, Nickname = "PROOF", OriginalTrainerName = "PKHAX", Language = (int)LanguageID.English,
            EXP = 135, Move1 = 33, Move1_PP = 35, OriginalTrainerFriendship = 70, Ball = 4,
        };
        pk.Stat_Level = 5;
        pk.ResetPartyStats();
        pk.SpeciesInternal = internalSpecies;
        pk.RefreshChecksum();
        var bytes = new byte[party ? pk.SIZE_PARTY : pk.SIZE_STORED];
        if (party)
            pk.WriteEncryptedDataParty(bytes);
        else
            pk.WriteEncryptedDataStored(bytes);
        return bytes;
    }

    public const string GameSource = "SaveBlock2+0xAC holds FireRed's fixed 1, or Emerald's encryption key with Emerald data past 0x890; a Ruby/Sapphire file keeps battle tower data at 0xAC and nothing past 0x890 (PKHeX.Core Saves/Util/SaveUtil.cs:302-320)";

    public static byte[] Save(SectionLayout layout, ShapeHeader header, IReadOnlyList<byte[]> party, IReadOnlyList<(int Box, int Slot, byte[] Record)> boxed, bool fireRedMark, int seed = 11, bool emeraldData = true)
    {
        var small = new byte[layout.BlockSize(SaveBlock.Small)];
        var large = new byte[layout.BlockSize(SaveBlock.Large)];
        var storage = new byte[layout.BlockSize(SaveBlock.Storage)];
        var rng = new Random(seed);
        rng.NextBytes(large);
        large.AsSpan(header.PartyCountOffset, header.PartyOffset - header.PartyCountOffset + (6 * 100)).Clear();
        large[header.PartyCountOffset] = (byte)party.Count;
        for (var i = 0; i < party.Count; i++)
            party[i].CopyTo(large, header.PartyOffset + (i * 100));
        rng.NextBytes(storage.AsSpan(storage.Length - 140));
        foreach (var (box, slot, record) in boxed)
            record.CopyTo(storage, 4 + ((((box - 1) * 30) + slot) * 80));
        small.AsSpan(0, 8).Fill(0xFF);
        StringConverter3.SetString(small.AsSpan(0, 8), "PKHAX", 7, (int)LanguageID.English, StringConverterOption.None);
        BinaryPrimitives.WriteUInt16LittleEndian(small.AsSpan(header.TrainerIdOffset), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(small.AsSpan(header.TrainerIdOffset + 2), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(small.AsSpan(0xAC), fireRedMark ? 1u : 0x5A0C3E21u);
        if (!fireRedMark && emeraldData)
            small[RetailGen3Save.RubySapphireEnd] = 1;
        var policy = ChecksumPolicy.FromLayout(layout);
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
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.IdOffset..], (ushort)id);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.SignatureOffset..], GbaSectorContainer.RetailSignature);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.CounterOffset..], 2);
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.ChecksumOffset..], policy.Compute(id, sector));
        }
        return file;
    }

    public static byte[] FireRedSave() => Save(SectionLayout.FireRedLeafGreen, FireRed, [Record(500, 0x1234_5678, true), Record(1100, 0x0BAD_F00D, true)], [(1, 0, Record(1100, 0x5EED_0001, false)), (5, 3, Record(500, 0x5EED_0002, false))], true);

    public static byte[] EmeraldSave() => Save(SectionLayout.Emerald, Emerald, [Record(500, 0x1234_5678, true)], [(1, 0, Record(277, 0x5EED_0003, false))], false);

    public static byte[] EmptyFireRedSave() => Save(SectionLayout.FireRedLeafGreen, FireRed, [], [], true);

    public static byte[] RubySapphireSave() => Save(SectionLayout.Emerald, Emerald, [Record(500, 0x1234_5678, true)], [], false, emeraldData: false);

    public static SaveShape Shape(ShapeHeader header) => new()
    {
        RomSha1 = new string('0', 40),
        Family = SaveShape.Retail,
        FamilyTier = ShapeTier.Derived,
        Headers = [header],
        PartyCodecCandidates = [Gen3ShapeFinder.RetailPk3, Gen3ShapeFinder.SoulGold96],
        LayoutTier = ShapeTier.Derived,
        Signatures = [GbaSectorContainer.RetailSignature],
    };
}

public sealed class RetailGen3SaveTests
{
    private static RetailGen3Save Open(byte[] file, WriteEvidence evidence = WriteEvidence.Unproven) =>
        HackSaves.TryOpen(file, RetailGen3Save.VariantId, evidence, out var why) as RetailGen3Save ?? throw new InvalidOperationException(why);

    [Fact]
    public void TheListedVariantIsHandled()
    {
        Assert.Contains(RetailGen3Save.VariantId, HackSaves.Variants);
        Assert.True(HackSaves.Handles(RetailGen3Save.VariantId));
    }

    [Fact]
    public void AFireRedContainerWithSpeciesPastStockOpens()
    {
        var save = Open(RetailFixture.FireRedSave());
        Assert.Same(RetailGen3Save.FireRedLeafGreen, save.Layout);
        Assert.False(save.FromShape);
        Assert.Equal(new ushort[] { 500, 1100 }, save.Party.Where(p => p is not null).Select(p => p!.Species).ToArray());
        Assert.All(save.Party.Where(p => p is not null), p => Assert.Null(p!.National));
        Assert.Equal(14, save.Boxes.Count);
        Assert.Equal((ushort)1100, save.Boxes[0].Slots[0]!.Species);
        Assert.Equal((ushort)500, save.Boxes[4].Slots[3]!.Species);
        Assert.Equal("PKHAX", save.Trainer.Name);
        Assert.Equal((ushort)1, save.Trainer.Tid16);
        Assert.Null(save.StructureProblem);
        Assert.Null(save.Money);
    }

    [Fact]
    public void AnEmeraldContainerOpensWithEmeraldOffsets()
    {
        var save = Open(RetailFixture.EmeraldSave());
        Assert.Same(RetailGen3Save.Emerald, save.Layout);
        Assert.Equal((ushort)500, save.Party[0]!.Species);
    }

    [Fact]
    public void AFireRedFileWithNoPartySettlesByTheMark()
    {
        var file = RetailFixture.EmptyFireRedSave();
        Assert.NotNull(RetailGen3Save.TryOpen(file, RetailGen3Save.Emerald, WriteEvidence.Unproven, out _));
        var save = Open(file);
        Assert.Same(RetailGen3Save.FireRedLeafGreen, save.Layout);
        Assert.All(save.Party, p => Assert.Null(p));
    }

    [Fact]
    public void ARubySapphireFileIsNotReadAsEmerald()
    {
        var file = RetailFixture.RubySapphireSave();
        Assert.NotNull(RetailGen3Save.TryOpen(file, RetailGen3Save.Emerald, WriteEvidence.Unproven, out _));
        Assert.Null(HackSaves.TryOpen(file, RetailGen3Save.VariantId, WriteEvidence.Unproven, out var why));
        Assert.Contains("Ruby/Sapphire", why);
        Assert.False(Open(RetailFixture.EmeraldSave()).IsRubySapphire);
    }

    [Fact]
    public void TheShapePathOpensRetailRecords()
    {
        var file = RetailFixture.FireRedSave();
        var save = HackSaves.TryOpen(file, RetailFixture.Shape(RetailFixture.FireRed), WriteEvidence.Unproven, out var why, out var trial);
        Assert.NotNull(save);
        var retail = Assert.IsType<RetailGen3Save>(WriteGate.Core(save));
        Assert.Null(why);
        Assert.True(retail.FromShape);
        Assert.Equal(1, trial.Layouts);
        Assert.Equal(SectionLayout.VanillaStride, trial.Primary!.Stride);
        Assert.Equal((ushort)1100, retail.Party[1]!.Species);
    }

    [Fact]
    public void AMoveAndItemEditStaysValidAndConfined()
    {
        var file = RetailFixture.FireRedSave();
        var save = Open(file);
        var mon = save.Party[0]!;
        save.SetPartySlot(0, mon with { Moves = [400, mon.Moves[1], mon.Moves[2], mon.Moves[3]], HeldItem = 600 });
        var box = save.Boxes[4].Slots[3]!;
        save.SetBoxSlot(5, 3, box with { HeldItem = 13 });
        var plan = save.Plan();
        Assert.True(plan.Confined);
        var again = Open(plan.Bytes);
        var read = again.Party[0]!;
        Assert.Equal((ushort)400, read.Moves[0]);
        Assert.Equal((ushort)600, read.HeldItem);
        Assert.Equal(mon.Pid, read.Pid);
        Assert.Equal(mon.Tid16, read.Tid16);
        Assert.Equal(mon.Sid16, read.Sid16);
        Assert.True(again.Party[1]!.Raw.Span.SequenceEqual(save.Party[1]!.Raw.Span));
        Assert.Equal((ushort)13, again.Boxes[4].Slots[3]!.HeldItem);
        var pk = new PK3(read.Raw.ToArray());
        Assert.True(pk.ChecksumValid);
        Assert.Equal((ushort)500, pk.SpeciesInternal);
        Assert.Equal(400, pk.Move1);
        Assert.Equal(600, pk.HeldItem);
    }

    [Fact]
    public void NoWriteRunsWithoutTheGate()
    {
        var save = Open(RetailFixture.FireRedSave(), WriteEvidence.GameAccepted);
        Assert.Equal(WriteEvidence.Confined, save.Evidence);
        Assert.NotNull(save.ReadOnlyReason);
        Assert.Throws<FaithfulWriteException>(() => save.Write());
    }

    [Fact]
    public void AStaleEditIsRefused()
    {
        var save = Open(RetailFixture.FireRedSave());
        var other = save.Party[1]!;
        Assert.Throws<FaithfulWriteException>(() => save.SetPartySlot(0, other with { HeldItem = 1 }));
    }

    [Fact]
    public void AnUnreadablePartyRecordMakesTheSaveReadOnly()
    {
        var file = RetailFixture.FireRedSave();
        file[0x1000 + 0x38 + 0x30] ^= 0xFF;
        var section = file.AsSpan(0x1000, SectionLayout.SectorSize);
        BinaryPrimitives.WriteUInt16LittleEndian(section[GbaSectorContainer.ChecksumOffset..], ChecksumPolicy.FromLayout(SectionLayout.FireRedLeafGreen).Compute(1, section));
        var save = Open(file);
        Assert.NotNull(save.StructureProblem);
        Assert.Single(save.PartyRefused);
    }
}
