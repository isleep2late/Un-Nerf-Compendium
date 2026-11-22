using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public sealed class GbStructureTests
{
    private static readonly GameStrings En = GameInfo.GetStrings("en");

    internal const int NamesAt = 0x1C21E;
    internal const int MapAt = 0x41024;
    internal const int BaseAt = 0x383DE;
    internal const int MovesAt = 0x30000;
    internal const int MoveNamesAt = 0xB0000;
    internal const int VanillaSlots = 190;
    internal const int VanillaDex = 151;
    internal const int GbMoves = 165;

    internal static int Dex(int national) => ((national * 37) % VanillaDex) + 1;

    internal static string GbName(string name, int max = 10)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToUpperInvariant())
        {
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9' or ' ' or '.' or '♂' or '♀')
                sb.Append(c);
        }
        return sb.Length > max ? sb.ToString(0, max) : sb.ToString();
    }

    internal static byte[] OwnDex(int added = 40, bool duplicate = false)
    {
        var rom = new byte[0x100000];
        var slots = VanillaSlots + added;
        rom.AsSpan(MapAt - 16, 16).Fill(0xFF);
        rom.AsSpan(MapAt + slots, 16).Fill(0xFF);
        for (var i = 1; i <= slots; i++)
        {
            var national = i <= VanillaSlots ? SpeciesConverter.GetNational1((byte)i) : 0;
            var name = i > VanillaSlots ? $"ADDED{i - VanillaSlots:00}" : national > 0 ? GbName(En.specieslist[national]) : "MISSINGNO.";
            GbHackData.Padded(name).CopyTo(rom, NamesAt + ((i - 1) * GbHackData.NameWidth));
            var dex = i > VanillaSlots ? VanillaDex + (i - VanillaSlots) : national > 0 ? Dex(national) : 0;
            rom[MapAt + i - 1] = (byte)dex;
            if (dex == 0)
                continue;
            var row = BaseAt + ((dex - 1) * GbHackData.Gen1BaseSize);
            rom[row] = (byte)dex;
            if (national > 0)
            {
                var p = PersonalTable.RB[national];
                byte[] stats = [(byte)Math.Min(255, p.HP + 1), (byte)Math.Min(255, p.ATK + 1), (byte)Math.Min(255, p.DEF + 1), (byte)Math.Min(255, p.SPE + 1), (byte)Math.Min(255, p.SPC + 1), p.Type1, p.Type2, p.CatchRate, (byte)p.BaseEXP];
                stats.CopyTo(rom, row + 1);
                rom[row + 19] = p.EXPGrowth;
            }
            else
            {
                byte[] stats = [50, 50, 50, 50, 50, 0, 0, 45, 64];
                stats.CopyTo(rom, row + 1);
            }
        }
        if (duplicate)
            rom[MapAt + 59] = rom[MapAt + 29];
        return rom;
    }

    internal static byte[] WithMoves(byte[] rom, Func<MoveAnchor, int>? anchorPower = null)
    {
        var names = new List<byte>();
        for (var m = 1; m <= GbMoves; m++)
        {
            names.AddRange(GbHackData.Encode(GbName(En.movelist[m], 20)));
            names.Add(GbHackData.Terminator);
            var at = MovesAt + ((m - 1) * GbHackData.Gen1MoveSize);
            var anchor = MoveAnchors.All.FirstOrDefault(a => a.Move == m);
            var power = anchor is null ? (m % 7 == 0 ? 0 : ((m % 20) + 1) * 5) : anchorPower?.Invoke(anchor) ?? anchor.Power;
            var accuracy = anchor?.Accuracy ?? 100;
            var pp = anchor?.Pp ?? (MoveInfo.GetPP(EntityContext.Gen1, (ushort)m) is var p and > 0 ? p : 10);
            byte[] row = [(byte)m, 0, (byte)power, m is 7 ? (byte)0x14 : (byte)0, (byte)(accuracy * 0xFF / 100), (byte)pp];
            row.CopyTo(rom, at);
        }
        names.CopyTo(rom, MoveNamesAt);
        return rom;
    }

    [Fact]
    public void TheIndexMapIsTheRunOfUniqueDexNumbers()
    {
        var rom = OwnDex();
        var map = GbStructure.IndexMap(rom, VanillaDex + 40);
        Assert.NotNull(map);
        Assert.Equal((MapAt, VanillaSlots + 40), (map!.Offset, map.Length));
        Assert.Equal(VanillaDex + 40, map.Distinct);
        Assert.Equal(Dex(SpeciesConverter.GetNational1(1)), map.Dex[0]);
        Assert.Null(GbStructure.IndexMap(rom, 99));
    }

    [Fact]
    public void ADuplicatedEntrySplitsTheMap()
    {
        var map = GbStructure.IndexMap(OwnDex(duplicate: true), VanillaDex + 40);
        Assert.True(map is null || map.Offset != MapAt, $"offset 0x{map?.Offset:X}");
    }

    [Fact]
    public void BaseRowsAreFoundWithoutTheBulbasaurAnchor()
    {
        var rows = GbStructure.BaseRows(OwnDex(), GbHackData.Gen1BaseSize, 5);
        Assert.Equal(new GbBaseRows(BaseAt, GbHackData.Gen1BaseSize, VanillaDex + 40), rows);
        Assert.Null(GbStructure.BaseRows(OwnDex(), GbHackData.Gen1BaseSize + 1, 5));
    }

    [Fact]
    public void AnOwnDexHackAttachesEveryRowThroughTheMap()
    {
        var r = GbHackData.Read(OwnDex());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(VanillaSlots + 40 + 1, d.Species.Count);
        Assert.Equal((HackTableStatus.Read, (bool?)true), (d.StatusOf(HackTableStatus.Species), d.Table(HackTableStatus.Species)!.Aligned));
        for (var i = 1; i <= VanillaSlots; i++)
        {
            var national = SpeciesConverter.GetNational1((byte)i);
            if (national == 0)
                continue;
            var p = PersonalTable.RB[national];
            Assert.Equal(Dex(national), d.Species[i].Dex);
            Assert.Equal([p.HP + 1, p.ATK + 1, p.DEF + 1, p.SPE + 1, p.SPC + 1, p.SPC + 1], d.Species[i].BaseStats);
        }
        Assert.Equal(VanillaDex + 40, d.Species[VanillaSlots + 40].Dex);
    }

    [Fact]
    public void ARejectedMapLeavesTheStatsUnreadNotAttachedByNationalNumber()
    {
        var d = GbHackData.Read(OwnDex(duplicate: true)).Data!;
        Assert.All(d.Species, s => Assert.Empty(s.BaseStats));
        Assert.Equal(HackTableStatus.Partial, d.StatusOf(HackTableStatus.Species));
        Assert.Equal(VanillaSlots + 1, d.Species.Count);
    }

    [Fact]
    public void Gen1MoveRowsAreReadAndCheckedAgainstTheAnchors()
    {
        var d = GbHackData.Read(WithMoves(OwnDex())).Data!;
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Moves));
        var firePunch = d.Moves[7];
        Assert.Equal((75, 100, 15, 0x14), (firePunch.Power, firePunch.Accuracy, firePunch.Pp, firePunch.Type));
        Assert.Equal(((int?)HackMove.Special, true), (firePunch.Category, firePunch.CategoryByType));
        Assert.Equal(85, d.Moves[3].Accuracy);
        Assert.Contains(d.Notes, n => n.Contains("found by POUND and KARATE CHOP only", StringComparison.Ordinal));

        var repowered = GbHackData.Read(WithMoves(OwnDex(), a => a.Move <= 4 ? a.Power + 1 : a.Power)).Data!;
        Assert.Equal(HackTableStatus.Partial, repowered.StatusOf(HackTableStatus.Moves));
        Assert.Equal(0, repowered.Moves[7].Power);
    }
    [Fact]
    public void AStrayLeadingDexByteGivesTwoWindowsAndTheReadTakesTheOneThatLinesUp()
    {
        var rom = OwnDex();
        var slots = VanillaSlots + 40;
        rom[MapAt - 1] = rom[MapAt + slots - 1];
        Assert.Equal(MapAt - 1, GbStructure.IndexMap(rom, VanillaDex + 40)!.Offset);
        var maps = GbStructure.IndexMaps(rom, VanillaDex + 40);
        Assert.Contains(maps, m => m.Offset == MapAt && m.Length == slots);
        var d = GbHackData.Read(rom).Data!;
        Assert.Equal((HackTableStatus.Read, (bool?)true), (d.StatusOf(HackTableStatus.Species), d.Table(HackTableStatus.Species)!.Aligned));
        var bulbasaurSlot = Enumerable.Range(1, VanillaSlots).First(i => SpeciesConverter.GetNational1((byte)i) == 1);
        Assert.Equal(Dex(1), d.Species[bulbasaurSlot].Dex);
        Assert.Contains(d.Notes, n => n.Contains($"species index map at 0x{MapAt:X} (the best of", StringComparison.Ordinal));
    }

    internal const int Gen2NamesAt = 0x50000;
    internal const int Gen2BaseAt = 0x60000;
    internal const int Gen2Species = 251;

    internal static byte[] Gen2WithoutAnchor(int typeRowOffset = 0)
    {
        var rom = new byte[0x100000];
        for (var i = 1; i <= Gen2Species; i++)
        {
            GbHackData.Padded(GbName(En.specieslist[i])).CopyTo(rom, Gen2NamesAt + ((i - 1) * GbHackData.NameWidth));
            var p = PersonalTable.StockC[i];
            var t = PersonalTable.StockC[Math.Clamp(i + typeRowOffset, 1, Gen2Species)];
            byte[] row = [(byte)i, (byte)Math.Min(255, p.HP + 1), (byte)Math.Min(255, p.ATK + 1), (byte)Math.Min(255, p.DEF + 1), (byte)Math.Min(255, p.SPE + 1), (byte)Math.Min(255, p.SPA + 1), (byte)Math.Min(255, p.SPD + 1), t.Type1, t.Type2, p.CatchRate, (byte)p.BaseEXP];
            row.CopyTo(rom, Gen2BaseAt + ((i - 1) * GbHackData.Gen2BaseSize));
        }
        return rom;
    }

    internal static string MadeUp(string prefix, int i) => $"{prefix}{(char)('A' + ((i / 26) % 26))}{(char)('A' + (i % 26))}";

    internal static byte[] List(IEnumerable<string> names)
    {
        var bytes = new List<byte>();
        foreach (var n in names)
        {
            bytes.AddRange(GbHackData.Encode(n));
            bytes.Add(GbHackData.Terminator);
        }
        return [.. bytes];
    }

    [Fact]
    public void FixedNameRunsNeedOneHundredFiftyPaddedSlots()
    {
        var rom = new byte[0x40000];
        for (var i = 0; i < 190; i++)
            GbHackData.Padded(MadeUp("ZQ", i)).CopyTo(rom, 0x1000 + (i * GbHackData.NameWidth));
        Assert.Equal([new GbNameRun(0x1000, 190, 190, 190)], GbStructure.FixedNameRuns(rom));
        rom[0x1000 + (99 * GbHackData.NameWidth) + 8] = 0x80;
        Assert.Empty(GbStructure.FixedNameRuns(rom));
        Assert.Equal([0x1000], GbStructure.FixedNameRuns(rom, 95).Select(r => r.Offset));
    }

    [Fact]
    public void FillerAndRepeatedSlotsAreNotANameRun()
    {
        var rom = new byte[0x40000];
        rom.AsSpan(0x2000, 0x1000).Fill(0xFF);
        rom.AsSpan(0x8000, 0x1000).Fill(0x80);
        for (var i = 0; i < 200; i++)
            GbHackData.Padded("SAME").CopyTo(rom, 0x10000 + (i * GbHackData.NameWidth));
        Assert.Empty(GbStructure.FixedNameRuns(rom));
    }

    [Fact]
    public void NameListsAreRunsOfShortTerminatedNames()
    {
        var rom = new byte[0x40000];
        List(Enumerable.Range(1, 120).Select(i => MadeUp("ZAP", i))).CopyTo(rom, 0x3000);
        List(Enumerable.Range(1, 30).Select(i => MadeUp("ITM", i))).CopyTo(rom, 0x9000);
        List(["THISNAMEISTOOLONG"]).CopyTo(rom, 0xA000);
        var lists = GbStructure.NameLists(rom, 30);
        Assert.Equal([0x3000, 0x9000], lists.Select(l => l.Offset));
        Assert.Equal(120, lists[0].Count);
        Assert.Equal(MadeUp("ZAP", 1), lists[0].Names[0]);
        Assert.Single(GbStructure.NameLists(rom));
        Assert.Equal(["", MadeUp("ZAP", 3)], lists[0].Indexed(2).Take(2));
    }

    [Fact]
    public void TheListStartLinesUpWithTheStockOrder()
    {
        var names = new List<string> { "ZQX", "QQV", "XQZ" };
        names.AddRange(Enumerable.Range(1, 165).Select(m => GbName(En.movelist[m], 12)));
        Assert.Equal(3, GbStructure.BestStart(names, En.movelist));
        Assert.Equal(0, GbStructure.BestStart(names.Skip(3).ToList(), En.movelist));
        Assert.Equal(0, GbStructure.BestStart(names, En.movelist, 0));
    }

    [Fact]
    public void AFewMoveRowsWithAnotherAnimationByteKeepTheRunWhole()
    {
        var rom = WithMoves(OwnDex());
        foreach (var m in new[] { 30, 60, 90 })
            rom[MovesAt + ((m - 1) * GbHackData.Gen1MoveSize)] = 1;
        var run = Assert.Single(GbStructure.MoveRows(rom, GbHackData.Gen1MoveSize, 100));
        Assert.Equal((MovesAt, GbMoves, 3), (run.Offset, run.Count, run.Renumbered));
        var d = GbHackData.Read(rom).Data!;
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Moves));
        Assert.Contains(d.Notes, n => n.Contains("on all but 3 rows", StringComparison.Ordinal));

        foreach (var m in Enumerable.Range(30, 40))
            rom[MovesAt + ((m - 1) * GbHackData.Gen1MoveSize)] = 1;
        Assert.Empty(GbStructure.MoveRows(rom, GbHackData.Gen1MoveSize, 100));
        Assert.Equal(HackTableStatus.Partial, GbHackData.Read(rom).Data!.StatusOf(HackTableStatus.Moves));
    }

    [Fact]
    public void Gen1MoveRowsAtTheGen2StrideAreStillRead()
    {
        var rom = OwnDex();
        var names = new List<byte>();
        for (var m = 1; m <= GbMoves; m++)
        {
            names.AddRange(GbHackData.Encode(GbName(En.movelist[m], 20)));
            names.Add(GbHackData.Terminator);
            var anchor = MoveAnchors.All.FirstOrDefault(a => a.Move == m);
            var power = anchor?.Power ?? (m % 7 == 0 ? 0 : ((m % 20) + 1) * 5);
            var accuracy = anchor?.Accuracy ?? 100;
            var pp = anchor?.Pp ?? 10;
            byte[] row = [(byte)m, 0, (byte)power, 0, (byte)(accuracy * 0xFF / 100), (byte)pp, 1];
            row.CopyTo(rom, MovesAt + ((m - 1) * GbHackData.Gen2MoveSize));
        }
        names.CopyTo(rom, MoveNamesAt);
        var d = GbHackData.Read(rom).Data!;
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Moves));
        Assert.Equal((40, 100, 35), (d.Moves[1].Power, d.Moves[1].Accuracy, d.Moves[1].Pp));
        Assert.Contains(d.Notes, n => n.Contains("(not the Gen 1 stride of 6)", StringComparison.Ordinal));
    }

    [Fact]
    public void TheMoveShapeTestAcceptsPretRowsAndRejectsNoise()
    {
        var rom = WithMoves(OwnDex());
        var run = Assert.Single(GbStructure.MoveRows(rom, GbHackData.Gen1MoveSize, 100));
        Assert.True(GbStructure.MoveShape(rom, run, 1));
        for (var m = 1; m <= GbMoves; m += 2)
            rom[MovesAt + ((m - 1) * GbHackData.Gen1MoveSize) + 3] = 0x40;
        Assert.False(GbStructure.MoveShape(rom, run, 1));
    }

    [Fact]
    public void Gen2RowsFoundWithoutTheAnchorMustCarryTheStockTypes()
    {
        var d = GbHackData.Read(Gen2WithoutAnchor()).Data!;
        Assert.Equal((HackTableStatus.Read, (bool?)true), (d.StatusOf(HackTableStatus.Species), d.Table(HackTableStatus.Species)!.Aligned));
        Assert.Contains(d.Notes, n => n.Contains("so they line up with the names", StringComparison.Ordinal));
        var shifted = GbHackData.Read(Gen2WithoutAnchor(typeRowOffset: 1)).Data!;
        Assert.Equal((HackTableStatus.Partial, (bool?)false), (shifted.StatusOf(HackTableStatus.Species), shifted.Table(HackTableStatus.Species)!.Aligned));
        Assert.False(shifted.SpeciesOverlayable);
    }
}
