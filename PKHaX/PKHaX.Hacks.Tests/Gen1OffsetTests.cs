using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class Gen1OffsetTests
{
    [Fact]
    public void StockOffsetsKeepTwentyItemBag()
    {
        Assert.Equal(20, SAV1Offsets.INT.BagCapacity);
        Assert.Equal(20, SAV1Offsets.JPN.BagCapacity);
        var sav = new SAV1(LanguageID.English, GameVersion.YW);
        Assert.Equal(20, sav.Inventory.Pouches[0].Items.Length);
    }

    [Fact]
    public void YellowLegacyShiftMatchesMeasuredOffsets()
    {
        var yl = SAV1Offsets.INT.ShiftedAfter(SAV1Offsets.INT.Items, 78, 59);
        Assert.Equal(SAV1Offsets.INT.Items, yl.Items);
        Assert.Equal(SAV1Offsets.INT.DexCaught, yl.DexCaught);
        Assert.Equal(SAV1Offsets.INT.DexSeen, yl.DexSeen);
        Assert.Equal(SAV1Offsets.INT.OT, yl.OT);
        Assert.Equal(0x25F3 + 78, yl.Money);
        Assert.Equal(0x2F7A, yl.Party);
        Assert.Equal(0x310E, yl.CurrentBox);
        Assert.Equal(0x3571, yl.ChecksumOfs);
        Assert.Equal(59, yl.BagCapacity);
    }

    [Fact]
    public void SaveUsesSuppliedOffsetsAndKeepsThemOnClone()
    {
        var blank = new SAV1(LanguageID.English, GameVersion.YW);
        var raw = blank.Write().ToArray();
        var yl = SAV1Offsets.INT.ShiftedAfter(SAV1Offsets.INT.Items, 78, 59);
        var sav = new SAV1(raw, LanguageID.English, GameVersion.YW, yl);
        Assert.Equal(59, sav.Inventory.Pouches[0].Items.Length);
        sav.Money = 123456;
        Assert.Equal(0x12, sav.Data[yl.Money]);
        var clone = (SAV1)sav.Clone();
        Assert.Equal(59, clone.Inventory.Pouches[0].Items.Length);
        Assert.Equal(123456u, clone.Money);
    }

    [Fact]
    public void DefaultConstructorPathIsUnchanged()
    {
        var blank = new SAV1(LanguageID.English, GameVersion.RB);
        blank.Money = 4242;
        var raw = blank.Write().ToArray();
        var a = new SAV1((byte[])raw.Clone(), LanguageID.English, GameVersion.RB);
        var b = new SAV1((byte[])raw.Clone(), LanguageID.English, GameVersion.RB, null);
        Assert.Equal(a.Write().ToArray(), b.Write().ToArray());
        Assert.Equal(4242u, a.Money);
    }
}
