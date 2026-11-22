using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class HackTableKeyTests
{
    private const int SpeciesInfo = 0x13000;
    private const int CodeAt = 0x1E000;
    private const int BoxTableAt = 0x1C000;

    private static HackData Read(byte[] rom)
    {
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        return r.Data!;
    }

    private static void BoxPointers(byte[] rom, int count)
    {
        for (var i = 0; i < count; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(BoxTableAt + i * 4), CfruBoxTable.Ewram + 0x1000 + (uint)(i * CfruBoxTable.BoxBytes));
    }

    [Fact]
    public void ImagesThatDifferInCodeBytesOnlyShareOneKey()
    {
        var plain = HackDataTests.Image();
        var patched = HackDataTests.Image();
        patched[CodeAt] = 0x46;
        patched[CodeAt + 1] = 0xC0;
        var a = Read(plain);
        var b = Read(patched);
        Assert.NotEqual(a.RomSha1, b.RomSha1);
        Assert.Equal(40, a.TableKey!.Length);
        Assert.Equal(a.TableKey, b.TableKey);
        Assert.Equal(a.Engine, b.Engine);
        Assert.True(HackTableCompare.Twins(a, b));
    }

    [Fact]
    public void OneChangedBaseStatChangesTheKey()
    {
        var changed = HackDataTests.Image();
        changed[SpeciesInfo + 2 * 28 + 1] = 63;
        var a = Read(HackDataTests.Image());
        var b = Read(changed);
        Assert.NotEqual(a.TableKey, b.TableKey);
        Assert.False(HackTableCompare.Twins(a, b));
    }

    [Fact]
    public void TheKeyIgnoresNotesReaderVersionAndTheImageHash()
    {
        var a = Read(HackDataTests.Image());
        var b = a with { Notes = ["another note"], ReaderVersion = HackData.MinReaderVersion, RomSha1 = new string('f', 40), Reader = "other", Tables = [] };
        Assert.Equal(HackTableKey.Of(a), HackTableKey.Of(b));
        Assert.NotEqual(HackTableKey.Of(a), HackTableKey.Of(a with { Engine = Gen3Engine.Cfru }));
        Assert.NotEqual(HackTableKey.Of(a), HackTableKey.Of(a with { Platform = HackTables.Nds }));
        Assert.NotEqual(HackTableKey.Of(a), HackTableKey.Of(a with { Moves = [.. a.Moves.Select(m => m.Id == 1 ? m with { Pp = 30 } : m)] }));
        Assert.NotEqual(HackTableKey.Of(a), HackTableKey.Of(a with { Species = [.. a.Species.Select(s => s.Id == 1 ? s with { Name = "BULBASAUR|X" } : s)] }));
    }

    [Fact]
    public void SharesCountTheIdsOfTheSmallerTableWithTheSameName()
    {
        var a = Read(HackDataTests.Image());
        var more = a with { Species = [.. a.Species, new HackSpecies(a.Species.Count, "ADDED", null, 0, [0, 0], [1, 2, 3, 4, 5, 6], [], null, 0, null, null)] };
        var shares = HackTableCompare.Shares(a, more);
        Assert.Equal(new HackTableShare(a.Species.Count, a.Species.Count), shares.Species);
        Assert.Equal(1.0, shares.Species.Share);
        Assert.Equal($"{a.Species.Count}/{a.Species.Count}", shares.Species.ToString());
        Assert.NotEqual(HackTableKey.Of(a), HackTableKey.Of(more));
        var renamed = a with { Species = [.. a.Species.Select(s => s.Id == 2 ? s with { Name = "IVYSORE" } : s)] };
        var fewer = HackTableCompare.Shares(renamed, more).Species;
        Assert.Equal(a.Species.Count - 1, fewer.Same);
        Assert.True(fewer.Share < 1.0);
        Assert.Equal(1.0, new HackTableShare(0, 0).Share);
    }

    [Fact]
    public void ARunOfEightBoxPointersNamesTheCfruEngine()
    {
        var eight = HackDataTests.Image(code: "BPRE");
        BoxPointers(eight, 8);
        Assert.Equal(Gen3Engine.Cfru, Read(eight).Engine);
        var seven = HackDataTests.Image(code: "BPRE");
        BoxPointers(seven, 7);
        Assert.Equal(Gen3Engine.Vanilla, Read(seven).Engine);
        Assert.True(Gen3Engine.IsCfru(new GbaImage(eight)));
        Assert.False(Gen3Engine.IsCfru(new GbaImage(seven)));
    }
}
