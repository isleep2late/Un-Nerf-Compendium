using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using Xunit;

namespace PKHaX.Hacks.Tests;

public sealed class GbLearnsetTests
{
    private const int Venusaur = 0x9A;
    private const int LeechSeed = 73;
    private const int VineWhip = 22;

    private static readonly byte[] Ivysaur1 = [GbLearnsets.EvolveLevel, 32, Venusaur, 0, 7, LeechSeed, 13, VineWhip, 0];

    [Fact]
    public void Gen1RecordsParseAsThePretMacrosLayThemOut()
    {
        Assert.Equal([new HackLearn(7, LeechSeed), new HackLearn(13, VineWhip)], GbLearnsets.Parse(Ivysaur1, 0, 1, 165));
        byte[] item = [GbLearnsets.EvolveItem, 10, 1, 36, 0, 0];
        Assert.Empty(GbLearnsets.Parse(item, 0, 1, 165)!);
        byte[] trade = [GbLearnsets.EvolveTrade, 1, 0x31, 0, 20, 5, 0];
        Assert.Equal([new HackLearn(20, 5)], GbLearnsets.Parse(trade, 0, 1, 165));
        byte[] missingNo = [0, 0];
        Assert.Empty(GbLearnsets.Parse(missingNo, 0, 1, 165)!);
    }

    [Fact]
    public void Gen2HasHappinessAndStatRecordsThatGen1DoesNot()
    {
        byte[] stat = [GbLearnsets.EvolveStat, 20, 2, 107, 0, 1, 33, 0];
        Assert.Equal([new HackLearn(1, 33)], GbLearnsets.Parse(stat, 0, 2, 251));
        Assert.Null(GbLearnsets.Parse(stat, 0, 1, 251));
        byte[] happiness = [GbLearnsets.EvolveHappiness, 1, 182, 0, 1, 33, 0];
        Assert.Equal([new HackLearn(1, 33)], GbLearnsets.Parse(happiness, 0, 2, 251));
        byte[] tradeAnyItem = [GbLearnsets.EvolveTrade, 0xFF, 65, 0, 1, 93, 0];
        Assert.Equal([new HackLearn(1, 93)], GbLearnsets.Parse(tradeAnyItem, 0, 2, 251));
    }

    [Fact]
    public void ALevelPastOneHundredOrAMovePastTheListFailsTheWholeList()
    {
        byte[] level = [0, 101, 5, 0];
        Assert.Null(GbLearnsets.Parse(level, 0, 1, 165));
        byte[] move = [0, 10, 166, 0];
        Assert.Null(GbLearnsets.Parse(move, 0, 1, 165));
        Assert.NotNull(GbLearnsets.Parse(move, 0, 1, 166));
        byte[] unended = [0, 10, 5];
        Assert.Null(GbLearnsets.Parse(unended, 0, 1, 165));
    }

    [Fact]
    public void PointersAreReadInsideTheTablesOwnBank()
    {
        Assert.Equal(0x38000 + 0x0123, GbLearnsets.Target(0x3A000, 0x4123, 0x100000));
        Assert.Null(GbLearnsets.Target(0x3A000, 0x3FFF, 0x100000));
        Assert.Null(GbLearnsets.Target(0x3A000, 0x8000, 0x100000));
        Assert.Null(GbLearnsets.Target(0x1000, 0x4123, 0x100000));
        Assert.Null(GbLearnsets.Target(0xFC000, 0x7FFF, 0xFC000));
    }

    private static byte[] Table(int slots, int at, int badEvery = 0)
    {
        var rom = new byte[0x100000];
        var data = at + (2 * slots);
        for (var i = 0; i < slots; i++)
        {
            var pointer = 0x4000 + (data % GbLearnsets.BankSize);
            rom[at + (2 * i)] = (byte)(pointer & 0xFF);
            rom[at + (2 * i) + 1] = (byte)(pointer >> 8);
            byte[] list = badEvery > 0 && i % badEvery == 0 ? [9, 9, 9, 0] : [0, (byte)(2 + (i % 40)), (byte)(1 + (i % 100)), 0];
            list.CopyTo(rom, data);
            data += list.Length;
        }
        return rom;
    }

    [Fact]
    public void ThePointerRunIsFoundAndEveryListIsDecoded()
    {
        const int at = 0x40000;
        var run = GbLearnsets.Find(Table(190, at), 1, 190, 165, out var why);
        Assert.True(run is not null, why);
        Assert.Equal((at, 190, 0, true), (run!.Offset, run.Count, run.Unread, run.DataFollows));
        Assert.Equal([new HackLearn(2 + 5, 1 + 5)], run.Lists[5]);
    }

    [Fact]
    public void ARunWithMoreThanATenthUnparsedIsRefusedWithAReason()
    {
        Assert.NotNull(GbLearnsets.Find(Table(190, 0x40000, 20), 1, 190, 165, out _));
        Assert.Null(GbLearnsets.Find(Table(190, 0x40000, 5), 1, 190, 165, out var why));
        Assert.Contains("90%", why, System.StringComparison.Ordinal);
        Assert.Null(GbLearnsets.Find(Table(190, 0x40000), 1, 99, 165, out var tooFew));
        Assert.Contains("fewer than the 100", tooFew, System.StringComparison.Ordinal);
    }

    [Fact]
    public void AReadOfAGen1ImageGivesTheLevelOneMovesFirst()
    {
        var rom = GbStructureTests.WithMoves(GbStructureTests.OwnDex());
        var slots = GbStructureTests.VanillaSlots + 40;
        const int at = 0x80000;
        var data = at + (2 * slots);
        for (var i = 0; i < slots; i++)
        {
            var pointer = 0x4000 + (data - at);
            rom[at + (2 * i)] = (byte)(pointer & 0xFF);
            rom[at + (2 * i) + 1] = (byte)(pointer >> 8);
            byte[] list = [0, 10, (byte)(1 + (i % 165)), 0];
            list.CopyTo(rom, data);
            data += list.Length;
        }
        var bulbasaurSlot = Enumerable.Range(1, GbStructureTests.VanillaSlots).First(i => PKHeX.Core.SpeciesConverter.GetNational1((byte)i) == 1);
        var row = GbStructureTests.BaseAt + ((GbStructureTests.Dex(1) - 1) * GbHackData.Gen1BaseSize);
        rom[row + GbHackData.Gen1LevelOneMoves] = 33;
        rom[row + GbHackData.Gen1LevelOneMoves + 1] = 45;
        var d = GbHackData.Read(rom).Data!;
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.LevelUp));
        Assert.Equal(slots, d.LevelUp.Count);
        Assert.Equal(new List<HackLearn> { new(1, 33), new(1, 45), new(10, bulbasaurSlot) }, d.LevelUp[bulbasaurSlot]);
        Assert.Contains(d.Notes, n => n.StartsWith("level-up learnsets from the pointer run at 0x80000", System.StringComparison.Ordinal));
    }
}
