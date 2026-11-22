using PKHaX.Hacks.Overlay;
using PKHaX.Hacks.Readers;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class FrontierShiftTests
{
    private static byte[] BlankEmerald() => TestSaves.Gen3(GameVersion.E);

    [Fact]
    public void ShiftZeroIsTheVanillaLayout()
    {
        var raw = BlankEmerald();
        var a = new SAV3E((byte[])raw.Clone());
        var b = new SAV3E((byte[])raw.Clone(), 0);
        a.SmallBlock.BP = 1234;
        b.SmallBlock.BP = 1234;
        Assert.Equal(a.Write().ToArray(), b.Write().ToArray());
        Assert.Equal(1234, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(a.SmallBlock.Data[0xEB8..]));
        Assert.Equal(0xF2C, a.SmallBlock.Data.Length);
    }

    [Fact]
    public void ShiftFourMovesFrontierFieldsOnly()
    {
        var sav = new SAV3E(BlankEmerald(), 4);
        Assert.Equal(0xF30, sav.SmallBlock.Data.Length);
        sav.SmallBlock.BP = 777;
        sav.SmallBlock.BPEarned = 4321;
        Assert.Equal(777, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(sav.SmallBlock.Data[0xEBC..]));
        Assert.Equal(4321, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(sav.SmallBlock.Data[0xEBE..]));
        Assert.Equal(0, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(sav.SmallBlock.Data[0xEB8..]));
        sav.SmallBlock.SecurityKey = 0x11223344;
        Assert.Equal(0x11223344u, System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(sav.SmallBlock.Data[0xAC..]));
        Assert.Equal(4, sav.SmallBlock.FrontierShift);
    }

    [Fact]
    public void CloneKeepsTheShift()
    {
        var sav = new SAV3E(BlankEmerald(), 4);
        sav.SmallBlock.BP = 55;
        var clone = (SAV3E)sav.Clone();
        Assert.Equal(4, clone.SmallBlock.FrontierShift);
        Assert.Equal(55, clone.SmallBlock.BP);
    }

    private static HackSaveContext Vanilla(int shift) => new() { ProfileId = "emerald-unnerf-full", Kind = ImageKind.UnNerf, SaveVariant = HackSaveContext.VanillaContainer, FrontierShift = shift };

    [Fact]
    public void TheVanillaPathAppliesTheProfilesFrontierShift()
    {
        var shifted = Assert.IsType<SAV3E>(HackSaveReaders.Open(BlankEmerald(), Vanilla(4)));
        Assert.Equal(4, shifted.SmallBlock.FrontierShift);
        Assert.Equal(0xF30, shifted.SmallBlock.Data.Length);
        var plain = Assert.IsType<SAV3E>(HackSaveReaders.Open(BlankEmerald(), Vanilla(0)));
        Assert.Equal(0, plain.SmallBlock.FrontierShift);
    }

    [Fact]
    public void AShiftOnANonEmeraldSaveIsRefused()
    {
        var ex = Assert.Throws<HackSaveUnavailableException>(() => HackSaveReaders.Open(TestSaves.Gen3(GameVersion.FR), Vanilla(4)));
        Assert.Contains("only an Emerald save has", ex.Message);
    }

    [Theory]
    [InlineData("vanilla", true)]
    [InlineData("vanilla:SAV3E", true)]
    [InlineData("vanilla:SAV5B2W2", true)]
    [InlineData("vanillas", false)]
    [InlineData("gen3-expansion/1", false)]
    public void VanillaVariantsAreRecognised(string variant, bool vanilla) => Assert.Equal(vanilla, HackSaveContext.IsVanilla(variant));

    [Fact]
    public void AClassNamedVanillaVariantOpensThroughTheVanillaPath()
    {
        var context = new HackSaveContext { ProfileId = "emerald-usa-europe+unnerf-full", Kind = ImageKind.UnNerf, SaveVariant = "vanilla:SAV3E", FrontierShift = 4 };
        Assert.Equal(4, Assert.IsType<SAV3E>(HackSaveReaders.Open(BlankEmerald(), context)).SmallBlock.FrontierShift);
    }
}
