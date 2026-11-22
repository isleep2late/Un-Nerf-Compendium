using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class ReviewFixTests
{
    private const int Items = 0x15000;

    private static void Ptr(byte[] rom, int at, int offset) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), (uint)(0x08000000 + offset));

    [Fact]
    public void AVanillaItemWithIdZeroDoesNotEndTheList()
    {
        var rom = HackDataTests.Image();
        BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(Items + 2 * 44 + 0x0E), 0);
        var d = GbaHackData.Read(rom).Data!;
        Assert.Equal(5, d.Items.Count);
        Assert.Equal("GREAT BALL", d.Items[3].Name);
    }

    [Fact]
    public void UnknownNamesStayUnmappedButGen3SpellingsMapToTheirSlot()
    {
        var m = IdMapper.English();
        Assert.Null(m.Move(4, "FAKEMOVE"));
        Assert.Null(m.Ability(5, "FAKEABILITY"));
        Assert.Equal((int)Move.HighJumpKick, m.Move((int)Move.HighJumpKick, "HI JUMP KICK"));
        Assert.Equal((int)Move.FeintAttack, m.Move((int)Move.FeintAttack, "FAINT ATTACK"));
        Assert.Equal((int)Move.ViseGrip, m.Move((int)Move.ViseGrip, "VICEGRIP"));
        Assert.Equal((int)Move.SmellingSalts, m.Move((int)Move.SmellingSalts, "SMELLINGSALT"));
        Assert.Null(m.Move((int)Move.Tackle, "HI JUMP KICK"));
        Assert.Equal(2, IdMapper.Distance("HIJUMPKICK", "HIGHJUMPKICK"));
    }

    [Fact]
    public void AHostileSymFileIsIgnoredNotACrash()
    {
        var rom = new byte[0x100000];
        foreach (var sym in new[] { "ffffffff:4000 MonsterNames\n", "7fffffff:4000 MonsterNames\n", "0e:ffffffff MonsterNames\n", "1ff:7fff BaseStats\n3f:4000 MonsterNames\n" })
        {
            var r = GbHackData.Read(rom, sym);
            Assert.False(r.Ok);
        }
        Assert.Null(SymFile.Parse("ffffffff:4000 MonsterNames\n").RomOffset("MonsterNames"));
    }

    [Fact]
    public void BrokenCacheJsonIsNullNotACrash()
    {
        var sha = new string('a', 40);
        var good = "{\"schema\":\"hackdata/1\",\"readerVersion\":" + HackData.CurrentReaderVersion + ",\"romSha1\":\"" + sha + "\",\"reader\":\"gba-gf\"";
        foreach (var json in new[]
        {
            "{\"schema\":5}",
            "{\"schema\":\"hackdata/1\",\"readerVersion\":\"2\"}",
            good + ",\"learnsets\":{\"levelUp\":{\"99999999999\":[]}}}",
            good + ",\"learnsets\":{\"levelUp\":{\"1\":[[5]]}}}",
            good + ",\"learnsets\":{\"levelUp\":{\"1\":[],\"01\":[]}}}",
            "{\"schema\":\"hackdata/1\",\"schema\":\"hackdata/1\"}",
            "[1,2,3]",
            good + ",\"species\":[{\"id\":1}]}",
        })
            Assert.Null(HackDataCache.FromJson(json, sha));
        Assert.NotNull(HackDataCache.FromJson(good + "}", sha));
    }

    [Fact]
    public void ARepointedTableWithOneReferenceBeatsTheHeaderPointer()
    {
        var rom = HackDataTests.Image();
        var repointed = 0x18000;
        string[] species = ["??????????", "BULBASAUR", "IVYSAUR", "VENUSAUR"];
        for (var i = 0; i < species.Length; i++)
        {
            rom.AsSpan(repointed + i * 11, 11).Fill(0xFF);
            StringConverter3.SetString(rom.AsSpan(repointed + i * 11, 11), species[i], 10, (int)LanguageID.English);
        }
        Ptr(rom, 0x1C000, repointed);
        var d = GbaHackData.Read(rom).Data!;
        Assert.Equal(GbaHackData.ReaderFrFollow, d.Reader);
        Assert.Equal("VENUSAUR", d.Species[3].Name);
        Assert.Equal(0, FrTableFollower.References(new GbaImage(HackDataTests.Image()), [0x10000])[0x10000]);
    }

    [Fact]
    public void ARhhTableEndingAtTheRomEndIsReadWithoutACrash()
    {
        var rom = RhhHackDataTests.Image();
        var r = GbaHackData.Read(rom.AsMemory(0, 0x20000 + 7 * 0xC4 + 0x1F + 13));
        Assert.True(r.Ok || r.Refusal is not null);
    }

    [Fact]
    public void OtherRubyAndSapphireLanguagesReadThroughTheFollower()
    {
        Assert.Equal(GbaHackData.ReaderFollow, GbaHackData.Read(HackDataTests.Image(code: "AXVD")).Data!.Reader);
    }

    [Fact]
    public void TheCacheSendsGbImagesToTheGbReaderAndKeepsTheRhhVersion()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pkhax-review-" + Guid.NewGuid().ToString("N"));
        try
        {
            var gb = new byte[0x200000];
            GbHackData.Padded("BULBASAUR").CopyTo(gb, 0x53384);
            GbHackData.Padded("IVYSAUR").CopyTo(gb, 0x53384 + 10);
            GbHackData.Padded("VENUSAUR").CopyTo(gb, 0x53384 + 20);
            Assert.Equal(GbHackData.ReaderAnchor, HackDataCache.ReadOrBuild(root, gb).Data!.Reader);
            var rhh = HackDataCache.ReadOrBuild(root, RhhHackDataTests.Image()).Data!;
            Assert.Equal("1.9.0", rhh.RhhVersion);
            Assert.Equal("1.9.0", HackDataCache.Load(root, rhh.RomSha1)!.RhhVersion);
        }
        finally
        {
            if (System.IO.Directory.Exists(root))
                System.IO.Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TheCacheAcceptsReaderVersionsFromTheMinimumToTheCurrentOne()
    {
        var sha = new string('b', 40);
        string Json(int version) => "{\"schema\":\"hackdata/1\",\"readerVersion\":" + version + ",\"romSha1\":\"" + sha + "\",\"reader\":\"gba-gf\"}";
        Assert.Equal(4, HackData.MinReaderVersion);
        Assert.Equal(6, HackData.CurrentReaderVersion);
        Assert.Null(HackDataCache.FromJson(Json(HackData.MinReaderVersion - 1), sha));
        Assert.Null(HackDataCache.FromJson(Json(HackData.CurrentReaderVersion + 1), sha));
        Assert.Equal(HackData.MinReaderVersion, HackDataCache.FromJson(Json(HackData.MinReaderVersion), sha)!.ReaderVersion);
        Assert.Equal(HackData.CurrentReaderVersion, HackDataCache.FromJson(Json(HackData.CurrentReaderVersion), sha)!.ReaderVersion);
        Assert.Null(HackDataCache.FromJson(Json(HackData.MinReaderVersion), sha, HackData.CurrentReaderVersion));
        var old = HackDataCache.FromJson(Json(HackData.MinReaderVersion), sha)!;
        Assert.Empty(old.Tables);
        Assert.Equal(HackTableStatus.Read, old.StatusOf(HackTableStatus.Species));
        Assert.True(old.SpeciesOverlayable);
        var odd = HackDataCache.FromJson("{\"schema\":\"hackdata/1\",\"readerVersion\":5,\"romSha1\":\"" + sha + "\",\"reader\":\"gba-gf\",\"tables\":[{\"table\":\"species\",\"status\":\"guessed\"},{\"status\":\"read\"},{\"table\":\"items\",\"status\":\"missing\",\"reason\":\"r\"}]}", sha)!;
        var row = Assert.Single(odd.Tables);
        Assert.Equal(HackTableStatus.Items, row.Table);
        Assert.Equal(HackTableStatus.Missing, row.Status);
        Assert.Equal("r", row.Reason);
        Assert.Equal(HackTableStatus.Read, odd.StatusOf(HackTableStatus.Species));
    }
}
