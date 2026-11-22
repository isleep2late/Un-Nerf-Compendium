using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class RealImageFixTests
{
    private static readonly byte[] PokeblockCase = [0x55, 0x56, 0x57, 0x58, 0x59, 0x00, 0xBD, 0xBB, 0xCD, 0xBF, 0xFF, 0x00, 0x00, 0x00];

    [Fact]
    public void Gen3NamesDecodeTheGlyphBytes()
    {
        var rom = new byte[0x200];
        PokeblockCase.CopyTo(rom, 0x20);
        var name = Gen3Tables.Name(new GbaImage(rom), 0x20, Gen3Tables.ItemNameLength);
        Assert.Equal("POKéBLOCK CASE", name);
        var gen3 = GameInfo.GetStrings("en").GetItemStrings(EntityContext.Gen3, GameVersion.E);
        Assert.Equal(IdMapper.Normalise(gen3[273]), IdMapper.Normalise(name!));
    }

    [Fact]
    public void Gen3NamesStillRefuseBytesOutsideTheCharset()
    {
        var rom = new byte[0x200];
        rom[0x20] = 0xBB;
        rom[0x21] = 0x52;
        rom[0x22] = 0xFF;
        Assert.Null(Gen3Tables.Name(new GbaImage(rom), 0x20, Gen3Tables.ItemNameLength));
    }

    [Theory]
    [InlineData(622, "Breakneck Blitz", 622)]
    [InlineData(623, "Breakneck Blitz", 623)]
    [InlineData(658, "Catastropika", 658)]
    [InlineData(656, "Twinkle Tackle", 656)]
    [InlineData(657, "Twinkle Tackle", 657)]
    [InlineData(900, "Breakneck Blitz", 622)]
    [InlineData(33, "Tackle", 33)]
    public void ZMovePairsMapToTheirOwnSlot(int id, string name, int national)
    {
        Assert.Equal(national, IdMapper.English().Move(id, name));
    }

    [Fact]
    public void TheZSuffixIsOnlyStrippedFromTheEnd()
    {
        Assert.Equal("Breakneck Blitz", IdMapper.WithoutZSuffix("Breakneck Blitz (P)"));
        Assert.Equal("Breakneck Blitz", IdMapper.WithoutZSuffix("Breakneck Blitz (S)"));
        Assert.Equal("Tackle", IdMapper.WithoutZSuffix("Tackle"));
        Assert.Equal("(P) Move", IdMapper.WithoutZSuffix("(P) Move"));
    }
}
