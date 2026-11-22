using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Saves;

public sealed class Gen3ContainerTests
{
    private static readonly uint[] Retail = [GbaSectorContainer.RetailSignature];
    private const uint UnboundSignature = 0x01121999;

    internal static byte[] Cfru(uint signature, int seed, uint counterA = 4, uint counterB = 3, int rotateA = 2)
    {
        var layout = SectionLayout.Cfru;
        var policy = ChecksumPolicy.FromLayout(layout);
        var rng = new Random(seed);
        var file = new byte[GbaSectorContainer.FileSize];
        for (var slot = 0; slot < 2; slot++)
        {
            var counter = slot == 0 ? counterA : counterB;
            var rotate = slot == 0 ? rotateA : 0;
            for (var i = 0; i < SectionLayout.SectionCount; i++)
            {
                var id = (i + rotate) % SectionLayout.SectionCount;
                var sector = file.AsSpan(((slot * SectionLayout.SectionCount) + i) * SectionLayout.SectorSize, SectionLayout.SectorSize);
                rng.NextBytes(sector[..layout.Slots[id].Size]);
                if (id is 0 or 4 or 13)
                    rng.NextBytes(sector[layout.Slots[id].Size..GbaSectorContainer.RawPayload]);
                BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.IdOffset..], (ushort)id);
                BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.SignatureOffset..], signature);
                BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.CounterOffset..], counter);
                BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.ChecksumOffset..], policy.Compute(id, sector));
            }
        }
        rng.NextBytes(file.AsSpan(28 * SectionLayout.SectorSize, 4 * SectionLayout.SectorSize));
        return file;
    }

    internal static WriteResult Patch(GbaSectorContainer c, IReadOnlyList<BlockEdit> edits) =>
        FaithfulWriter.Patch(c, edits, [.. edits.Select(e => new BlockWindow(e.Block, e.Offset, e.Bytes.Length))]);

    [Fact]
    public void AnEditThatSpillsPastItsIntendedWindowIsNotConfined()
    {
        var file = Cfru(UnboundSignature, 9);
        var c = Open(file, SectionLayout.Cfru, [UnboundSignature]);
        var bytes = Enumerable.Repeat((byte)0x5A, 8).ToArray();
        var r = FaithfulWriter.Patch(c, [new BlockEdit(SaveBlock.Large, 0x200, bytes)], [new BlockWindow(SaveBlock.Large, 0x200, 4)]);
        Assert.False(r.Confined);
        Assert.Single(r.Outside);
        Assert.True(r.Outside[0].Length is >= 1 and <= 4);
        var whole = new byte[SectionLayout.Cfru.BlockSize(SaveBlock.Large)];
        new Random(1).NextBytes(whole);
        var all = FaithfulWriter.Patch(c, [new BlockEdit(SaveBlock.Large, 0, whole)], [new BlockWindow(SaveBlock.Large, 0x38, 100)]);
        Assert.False(all.Confined);
    }

    private static GbaSectorContainer Open(byte[] file, SectionLayout layout, IReadOnlyCollection<uint>? signatures = null)
    {
        var c = GbaSectorContainer.TryOpen(file, layout, ChecksumPolicy.FromLayout(layout), signatures ?? Retail, out var why);
        Assert.True(c is not null, why);
        return c!;
    }

    [Theory]
    [InlineData(GameVersion.FR)]
    [InlineData(GameVersion.E)]
    public void VanillaBlocksEqualPkhex(GameVersion version)
    {
        var file = TestSaves.Gen3(version);
        var layout = version == GameVersion.E ? SectionLayout.Emerald : SectionLayout.FireRedLeafGreen;
        var c = Open(file, layout);
        var sav = (SAV3)SaveUtil.GetSaveFile(file)!;
        Assert.Equal(sav.Small[..layout.BlockSize(SaveBlock.Small)].ToArray(), c.Block(SaveBlock.Small));
        Assert.Equal(sav.Large[..layout.BlockSize(SaveBlock.Large)].ToArray(), c.Block(SaveBlock.Large));
        Assert.Equal(sav.Storage[..layout.BlockSize(SaveBlock.Storage)].ToArray(), c.Block(SaveBlock.Storage));
    }

    [Fact]
    public void VanillaSizesAreTheGamesBlocks()
    {
        Assert.Equal(0xF24, SectionLayout.FireRedLeafGreen.BlockSize(SaveBlock.Small));
        Assert.Equal(0x3D68, SectionLayout.FireRedLeafGreen.BlockSize(SaveBlock.Large));
        Assert.Equal(0x3D88, SectionLayout.Emerald.BlockSize(SaveBlock.Large));
        Assert.Equal(0x83D0, SectionLayout.Emerald.BlockSize(SaveBlock.Storage));
        Assert.Equal(0x7D0, SectionLayout.Emerald.Slots[13].Size);
    }

    [Fact]
    public void CfruLayoutIsTheCfruTable()
    {
        var expect = new (int, int)[]
        {
            (0, 0xF24), (0, 0xFF0), (0xFF0, 0xFF0), (0x1FE0, 0xFF0), (0x2FD0, 0xD98),
            (0, 0xFF0), (0xFF0, 0xFF0), (0x1FE0, 0xFF0), (0x2FD0, 0xFF0), (0x3FC0, 0xFF0), (0x4FB0, 0xFF0), (0x5FA0, 0xFF0), (0x6F90, 0xFF0), (0x7F80, 0x450),
        };
        Assert.Equal(expect, SectionLayout.Cfru.Slots.Select(s => (s.ToAdd, s.Size)).ToArray());
        var table = new byte[SectionLayout.TableBytes];
        for (var id = 0; id < expect.Length; id++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(id * 4), (ushort)expect[id].Item1);
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan((id * 4) + 2), (ushort)expect[id].Item2);
        }
        Assert.Equal(SectionLayout.Cfru.Slots, SectionLayout.FromTable("rom", table).Slots);
    }

    [Fact]
    public void BrokenTablesAreRefused()
    {
        var slots = SectionLayout.Cfru.Slots.ToArray();
        slots[3] = slots[3] with { ToAdd = slots[3].ToAdd + 4 };
        Assert.Contains("not right after", SectionLayout.Problem(slots));
        slots = SectionLayout.Cfru.Slots.ToArray();
        slots[13] = slots[13] with { Size = 0xFF8 };
        Assert.Contains("multiple of 4", SectionLayout.Problem(slots) ?? "");
        Assert.Throws<ArgumentException>(() => SectionLayout.FromTable("short", new byte[10]));
    }

    [Theory]
    [InlineData(GameVersion.FR)]
    [InlineData(GameVersion.E)]
    public void NoEditWritesTheSameBytes(GameVersion version)
    {
        var file = TestSaves.Gen3(version);
        var c = Open(file, version == GameVersion.E ? SectionLayout.Emerald : SectionLayout.FireRedLeafGreen);
        var r = Patch(c, []);
        Assert.Equal(file, r.Bytes);
        Assert.Empty(r.DiffRanges);
        Assert.Empty(r.TouchedSections);
    }

    [Fact]
    public void VanillaEditStaysConfinedAndPkhexStillReadsIt()
    {
        var file = TestSaves.Gen3(GameVersion.FR);
        var c = Open(file, SectionLayout.FireRedLeafGreen);
        var r = Patch(c, [new BlockEdit(SaveBlock.Large, 0x290, [0x11, 0x22, 0x33, 0x44])]);
        Assert.Equal([1], r.TouchedSections);
        Assert.True(r.Confined, string.Join(", ", r.DiffRanges));
        Assert.Equal(2, r.DiffRanges.Count);
        var sav = (SAV3)SaveUtil.GetSaveFile(r.Bytes)!;
        Assert.True(sav.ChecksumsValid);
        Assert.Equal(0x44332211u, BinaryPrimitives.ReadUInt32LittleEndian(sav.Large[0x290..]));
        var again = Open(r.Bytes, SectionLayout.FireRedLeafGreen);
        Assert.Equal(c.ActiveSlot, again.ActiveSlot);
    }

    [Fact]
    public void AnEditAcrossTwoSectionsChecksumsBoth()
    {
        var file = TestSaves.Gen3(GameVersion.E);
        var c = Open(file, SectionLayout.Emerald);
        var r = Patch(c, [new BlockEdit(SaveBlock.Storage, 0xF7E, [1, 2, 3, 4])]);
        Assert.Equal([5, 6], r.TouchedSections);
        Assert.True(r.Confined);
        Assert.True(((SAV3)SaveUtil.GetSaveFile(r.Bytes)!).ChecksumsValid);
    }

    [Fact]
    public void AnEditPastTheBlockIsRefused()
    {
        var c = Open(Cfru(UnboundSignature, 1), SectionLayout.Cfru, [UnboundSignature]);
        Assert.Throws<FaithfulWriteException>(() => Patch(c, [new BlockEdit(SaveBlock.Small, 0xF22, [1, 2, 3, 4])]));
        Assert.Throws<FaithfulWriteException>(() => Patch(c, [new BlockEdit(SaveBlock.Storage, -1, [1])]));
    }

    [Fact]
    public void CfruOpensOnlyWithItsSignatureAndPicksTheNewerSlot()
    {
        var file = Cfru(UnboundSignature, 2, counterA: 7, counterB: 9);
        Assert.Null(GbaSectorContainer.TryOpen(file, SectionLayout.Cfru, ChecksumPolicy.FromLayout(SectionLayout.Cfru), Retail, out var why));
        Assert.Contains("signature", why);
        var c = Open(file, SectionLayout.Cfru, [UnboundSignature]);
        Assert.Equal(1, c.ActiveSlot);
        Assert.Equal(9u, c.Counter);
        Assert.Equal(UnboundSignature, c.Signature);
    }

    [Fact]
    public void ABrokenNewerSlotFallsBackToTheOlderOne()
    {
        var file = Cfru(UnboundSignature, 3, counterA: 7, counterB: 9);
        file[(SectionLayout.SectionCount * SectionLayout.SectorSize) + 0x10] ^= 0xFF;
        var c = Open(file, SectionLayout.Cfru, [UnboundSignature]);
        Assert.Equal(0, c.ActiveSlot);
    }

    [Fact]
    public void TheCounterWrapPicksZeroOverMaxValue()
    {
        var c = Open(Cfru(UnboundSignature, 4, counterA: uint.MaxValue, counterB: 0), SectionLayout.Cfru, [UnboundSignature]);
        Assert.Equal(1, c.ActiveSlot);
    }

    [Fact]
    public void TheVanillaWindowRejectsACfruSave()
    {
        var file = Cfru(GbaSectorContainer.RetailSignature, 5);
        Assert.Null(GbaSectorContainer.TryOpen(file, SectionLayout.FireRedLeafGreen, ChecksumPolicy.Uniform("0xF80", 0xF80), Retail, out var why));
        Assert.Contains("checksum", why);
        Assert.NotNull(GbaSectorContainer.TryOpen(file, SectionLayout.Cfru, ChecksumPolicy.FromLayout(SectionLayout.Cfru), Retail, out _));
    }

    [Fact]
    public void CfruWriteKeepsParasiteRawSectorsAndTheOtherSlot()
    {
        var file = Cfru(UnboundSignature, 6);
        var c = Open(file, SectionLayout.Cfru, [UnboundSignature]);
        var edits = new List<BlockEdit>
        {
            new(SaveBlock.Small, 0xF20, [9, 9, 9, 9]),
            new(SaveBlock.Large, 0x3D60, [8, 8, 8, 8, 8, 8, 8, 8]),
            new(SaveBlock.Storage, 0x83C0, [7, 7, 7, 7]),
        };
        var r = Patch(c, edits);
        Assert.True(r.Confined, string.Join(", ", r.DiffRanges));
        Assert.Equal([0, 4, 13], r.TouchedSections);
        foreach (var id in new[] { 0, 4, 13 })
        {
            var at = c.SectorOffset(id);
            var size = c.Layout.Slots[id].Size;
            Assert.Equal(file.AsSpan(at + size, GbaSectorContainer.RawPayload - size).ToArray(), r.Bytes.AsSpan(at + size, GbaSectorContainer.RawPayload - size).ToArray());
        }
        var other = (1 - c.ActiveSlot) * SectionLayout.SectionCount * SectionLayout.SectorSize;
        Assert.Equal(file.AsSpan(other, SectionLayout.SectionCount * SectionLayout.SectorSize).ToArray(), r.Bytes.AsSpan(other, SectionLayout.SectionCount * SectionLayout.SectorSize).ToArray());
        Assert.Equal(file.AsSpan(28 * SectionLayout.SectorSize).ToArray(), r.Bytes.AsSpan(28 * SectionLayout.SectorSize).ToArray());
        var again = Open(r.Bytes, SectionLayout.Cfru, [UnboundSignature]);
        Assert.Equal([9, 9, 9, 9], again.Block(SaveBlock.Small)[0xF20..0xF24]);
        Assert.Equal(c.Counter, again.Counter);
    }

    [Fact]
    public void AnEditThatChangesNothingTouchesNoChecksum()
    {
        var file = Cfru(UnboundSignature, 7);
        var c = Open(file, SectionLayout.Cfru, [UnboundSignature]);
        var same = c.Block(SaveBlock.Large)[0x100..0x110];
        var r = Patch(c, [new BlockEdit(SaveBlock.Large, 0x100, same)]);
        Assert.Equal(file, r.Bytes);
        Assert.Empty(r.TouchedSections);
    }

    [Fact]
    public void PkhexWriteStampsAChecksumTheCfruGameRejects()
    {
        var file = Cfru(GbaSectorContainer.RetailSignature, 8);
        var c = Open(file, SectionLayout.Cfru);
        var sector = c.Sector(1);
        var cfru = ChecksumPolicy.FromLayout(SectionLayout.Cfru).Compute(1, sector);
        var pkhex = ChecksumPolicy.Fold(sector[..SectionLayout.VanillaStride]);
        Assert.NotEqual(cfru, pkhex);
    }
}
