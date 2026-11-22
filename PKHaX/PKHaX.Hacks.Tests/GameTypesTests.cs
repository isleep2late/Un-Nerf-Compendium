using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class GameTypesTests
{
    [Fact]
    public void PkhexStockGen3And4TablesHaveNoMysterySlot()
    {
        foreach (var table in new IPersonalTable[] { PersonalTable.E, PersonalTable.FR, PersonalTable.Pt, PersonalTable.HGSS })
        {
            Assert.Equal(9, table[4].Type1);
            Assert.Equal(16, Enumerable.Range(1, table.MaxSpeciesID).Max(i => System.Math.Max(table[i].Type1, table[i].Type2)));
        }
        Assert.Equal(9, PersonalTable.BW[4].Type1);
    }

    [Theory]
    [InlineData(3, 10, 9)]
    [InlineData(3, 12, 11)]
    [InlineData(3, 17, 16)]
    [InlineData(4, 8, 8)]
    [InlineData(4, 0, 0)]
    [InlineData(5, 9, 9)]
    [InlineData(5, 17, 17)]
    [InlineData(6, 17, 17)]
    public void RawIdsMapToPkhexIds(int generation, int raw, int pkhex)
    {
        Assert.Equal(pkhex, GameTypes.ToPkhex(generation, raw));
        Assert.Equal(raw, GameTypes.FromPkhex(generation, pkhex));
    }

    [Fact]
    public void TheMysterySlotHasNoPkhexType()
    {
        Assert.Null(GameTypes.ToPkhex(3, 9));
        Assert.Null(GameTypes.ToPkhex(4, 9));
        Assert.Equal(9, GameTypes.ToPkhex(5, 9));
    }

    [Fact]
    public void EveryStockGen3And4TypeRoundTripsThroughTheRawId()
    {
        foreach (var (gen, table) in new (int, IPersonalTable)[] { (3, PersonalTable.E), (4, PersonalTable.Pt) })
            for (var i = 1; i <= table.MaxSpeciesID; i++)
                Assert.Equal(table[i].Type1, GameTypes.ToPkhex(gen, GameTypes.FromPkhex(gen, table[i].Type1)));
    }
}
