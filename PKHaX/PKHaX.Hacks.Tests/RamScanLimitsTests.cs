using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class RamScanLimitsTests
{
    [Fact]
    public void DefaultsAreTheVanillaScanConstants()
    {
        var d = RamScanLimits.Default;
        Assert.Equal(440, d.MaxSpecies3);
        Assert.Equal(354, d.MaxMove3);
        Assert.Equal(507, d.MaxSpecies4);
        Assert.Equal(649, d.MaxSpecies5);
        Assert.Equal(807, d.MaxSpecies67);
        Assert.Equal(2, d.MinLevel);
        Assert.Equal(1, d.MinLevel67);
        Assert.Equal(100, d.MaxLevel);
        Assert.Null(d.Gen3Validator);
    }

    [Fact]
    public void HackLimitsWidenLevelsAndIds()
    {
        var h = RamScanLimits.ForHack(1573, 848);
        Assert.Equal(1573, h.MaxSpecies3);
        Assert.Equal(848, h.MaxMove3);
        Assert.Equal(1, h.MinLevel);
        Assert.Equal(255, h.MaxLevel);
    }
}
