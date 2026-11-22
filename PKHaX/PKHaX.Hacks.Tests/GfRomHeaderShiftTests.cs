using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class GfRomHeaderShiftTests
{
    private const int SpeciesNames = 0x10000;
    private const int MoveNames = 0x11000;
    private const int AbilityNames = 0x12000;
    private const int SpeciesInfo = 0x13000;
    private const int Moves = 0x14000;
    private const int Items = 0x15000;
    private const int AbilityDescriptions = 0x16000;
    private const int BallGfx = 0x17000;
    private const int Decorations = 0x18000;
    private const int IconPalettes = 0x19000;

    private static byte[] Image(int shift, int early = 0, bool names = true, bool speciesInfo = true, bool tables = true, bool neighbours = true, bool items = true, bool moves = true)
    {
        var rom = new byte[0x20000];
        void Word(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), value);
        void Ptr(int at, int offset) => Word(at, GbaImage.RomBase + (uint)offset);
        var head = GfRomHeader.At + early;
        var tail = GfRomHeader.At + shift;
        if (neighbours)
        {
            Ptr(head + 0x40, IconPalettes);
            Ptr(head + 0x4C, Decorations);
            Ptr(tail + 0xC4, AbilityDescriptions);
            Ptr(tail + 0xD0, BallGfx);
        }
        if (names)
        {
            Ptr(head + 0x44, SpeciesNames);
            Ptr(head + 0x48, MoveNames);
            Ptr(tail + 0xC0, AbilityNames);
        }
        Word(head + 0x50, 0xEE0);
        Word(head + 0x54, 0x1000);
        Word(head + 0x58, 0x18);
        Word(head + 0x70, 386);
        Word(tail + 0x88, 0xF24);
        Word(tail + 0x8C, 0x3D68);
        Word(tail + 0x90, 0x34);
        Word(tail + 0x94, 0x38);
        Word(tail + 0x9C, 0xA);
        if (tables)
        {
            if (speciesInfo)
                Ptr(tail + 0xBC, SpeciesInfo);
            if (items)
                Ptr(tail + 0xC8, Items);
            if (moves)
                Ptr(tail + 0xCC, Moves);
        }
        Word(tail + 0xEC, 0x298);
        Word(tail + 0xF4, 0x30EC);
        Word(tail + 0xF8, 0x34);
        return rom;
    }

    [Fact]
    public void ShiftsAreZeroThenOneWordEarlyThenOneWordLate()
    {
        Assert.Equal(new[] { 0, -4, 4 }, GfRomHeader.Shifts.ToArray());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-4, 0)]
    [InlineData(-4, -4)]
    [InlineData(4, 0)]
    public void EachShiftReadsItsOwnHeader(int shift, int early)
    {
        var h = GfRomHeader.Read(new GbaImage(Image(shift, early)), out var why);
        Assert.True(h is not null, why);
        Assert.Equal((shift, early), (h!.Shift, h.EarlyShift));
        Assert.Equal((0xF24u, 0x3D68u, 0x34u, 0x38u, 0xAu), (h.SaveBlock2Size, h.SaveBlock1Size, h.PartyCountOffset, h.PartyOffset, h.TrainerIdOffset));
        Assert.Equal(SpeciesNames, h.SpeciesNames);
        Assert.Equal(MoveNames, h.MoveNames);
        Assert.Equal(AbilityNames, h.AbilityNames);
        Assert.Equal(SpeciesInfo, h.SpeciesInfo);
        Assert.Equal(Items, h.Items);
        Assert.Equal(Moves, h.Moves);
        Assert.Equal((0xEE0u, 0x1000u, 0x18u, 386u), (h.FlagsOffset, h.VarsOffset, h.PokedexOffset, h.PokedexCount));
        Assert.Equal((0x298u, 0x30ECu, 0x34u), (h.PcItemsOffset, h.EnigmaBerryOffset, h.EnigmaBerrySize));
        Assert.Equal(shift != 0, h.Shifted);
        Assert.False(h.NamesInline);
        Assert.False(h.EarlyPlaceAmbiguous);
    }

    [Fact]
    public void TheShiftListDecidesWhichHeadersAreTried()
    {
        var rom = new GbaImage(Image(-4));
        Assert.Null(GfRomHeader.Read(rom, [0, 4], out var why));
        Assert.Contains("shift 0:", why);
        Assert.Contains("shift 4:", why);
        Assert.Empty(GfRomHeader.ReadAll(rom, [0, 4]));
        Assert.Equal(-4, Assert.Single(GfRomHeader.ReadAll(rom)).Shift);
    }

    [Fact]
    public void TheSizesDecideTheShiftNotThePointers()
    {
        var rom = new GbaImage(Image(-4, -4));
        var plain = GfRomHeader.ReadAt(rom, 0)!;
        Assert.True(plain.SaneTables);
        Assert.False(plain.SaneSizes);
        Assert.Equal(AbilityNames, plain.SpeciesInfo);
        Assert.Equal(Moves, plain.Items);
        Assert.Equal(BallGfx, plain.Moves);
        Assert.Contains("block sizes", plain.Problem);
        var read = GfRomHeader.Read(rom)!;
        Assert.Equal(-4, read.Shift);
        Assert.Equal(SpeciesInfo, read.SpeciesInfo);
        Assert.Equal(Items, read.Items);
        Assert.Equal(Moves, read.Moves);
    }

    [Fact]
    public void NullNamePointersDoNotStopTheTableReader()
    {
        var h = GfRomHeader.Read(new GbaImage(Image(0, names: false)), out var why);
        Assert.True(h is not null, why);
        Assert.True(h!.SaneTables);
        Assert.True(h.NamesInline);
        Assert.Null(h.SpeciesNames);
        Assert.Null(h.MoveNames);
        Assert.Null(h.AbilityNames);
        Assert.Null(h.Problem);
    }

    [Fact]
    public void AMissingTablePointerIsRefusedByName()
    {
        var rom = new GbaImage(Image(0, speciesInfo: false));
        Assert.Null(GfRomHeader.Read(rom, out var why));
        Assert.Contains("shift 0: the species-info pointer (+0xBC) is not a ROM pointer", why);
        var h = GfRomHeader.ReadAt(rom, 0)!;
        Assert.True(h.SaneSizes);
        Assert.False(h.SaneTables);
        Assert.Equal("species-info pointer (+0xBC)", h.MissingTable);
    }

    [Theory]
    [InlineData(false, true, "item pointer (+0xC8)")]
    [InlineData(true, false, "move pointer (+0xCC)")]
    public void AMissingItemOrMovePointerIsRefusedByName(bool items, bool moves, string missing)
    {
        var rom = new GbaImage(Image(0, items: items, moves: moves));
        Assert.Null(GfRomHeader.Read(rom, out var why));
        Assert.Contains($"shift 0: the {missing} is not a ROM pointer", why);
        var h = GfRomHeader.ReadAt(rom, 0)!;
        Assert.True(h.SaneSizes);
        Assert.False(h.SaneTables);
        Assert.Equal(missing, h.MissingTable);
        Assert.Equal(SpeciesInfo, h.SpeciesInfo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(4)]
    public void ReadAllKeepsAHeaderWithNoPointerAtAll(int shift)
    {
        var rom = new GbaImage(Image(shift, names: false, tables: false, neighbours: false));
        Assert.Null(GfRomHeader.Read(rom));
        var h = Assert.Single(GfRomHeader.ReadAll(rom));
        Assert.Equal(shift, h.Shift);
        Assert.True(h.SaneSizes);
        Assert.False(h.SaneTables);
        Assert.Equal((0x30ECu, 0x34u), (h.EnigmaBerryOffset, h.EnigmaBerrySize));
        Assert.Equal(shift == -4, h.EarlyPlaceAmbiguous);
        Assert.Equal(0, h.EarlyShift);
    }

    [Fact]
    public void AShortImageHasNoHeader()
    {
        var rom = new GbaImage(new byte[0x200]);
        Assert.NotNull(GfRomHeader.ReadAt(rom, -4));
        Assert.Null(GfRomHeader.ReadAt(rom, 0));
        Assert.Null(GfRomHeader.Read(rom, out var why));
        Assert.Contains("too short", why);
        Assert.Empty(GfRomHeader.ReadAll(rom));
    }

    [Fact]
    public void NullNamePointersOneWordEarlyAreFoundAtTheirOwnPlace()
    {
        var early = GfRomHeader.ReadAt(new GbaImage(Image(-4, -4, names: false)), -4)!;
        Assert.Equal(-4, early.EarlyShift);
        Assert.False(early.EarlyPlaceAmbiguous);
        Assert.Equal(0xEE0u, early.FlagsOffset);
        var late = GfRomHeader.ReadAt(new GbaImage(Image(-4, 0, names: false)), -4)!;
        Assert.Equal(0, late.EarlyShift);
        Assert.Equal(0xEE0u, late.FlagsOffset);
        Assert.Equal(386u, early.PokedexCount);
        Assert.Equal(386u, late.PokedexCount);
    }
}
