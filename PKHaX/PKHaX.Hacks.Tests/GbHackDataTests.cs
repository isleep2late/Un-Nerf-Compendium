using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class GbHackDataTests
{
    private static void Fixed(byte[] rom, int at, params string[] names)
    {
        for (var i = 0; i < names.Length; i++)
            GbHackData.Padded(names[i]).CopyTo(rom, at + i * GbHackData.NameWidth);
    }

    private static void List(byte[] rom, int at, params string[] names)
    {
        var bytes = new List<byte>();
        foreach (var n in names)
        {
            bytes.AddRange(GbHackData.Encode(n));
            bytes.Add(GbHackData.Terminator);
        }
        bytes.CopyTo(rom, at);
    }

    private static byte[] Gen1()
    {
        var rom = new byte[0x100000];
        Fixed(rom, 0x1C21E, "RHYDON", "KANGASKHAN", "NIDORAN♂", "CLEFAIRY", "MISSINGNO.");
        List(rom, 0xB0000, "POUND", "KARATE CHOP", "DOUBLESLAP", "COMET PUNCH");
        List(rom, 0x472B, "MASTER BALL", "ULTRA BALL", "GREAT BALL", "ZQXWEIRD");
        return rom;
    }

    [Fact]
    public void Gen1TablesAreFoundByTheirVanillaAnchors()
    {
        var r = GbHackData.Read(Gen1());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(GbHackData.ReaderAnchor, d.Reader);
        Assert.Contains("Gen 1", d.Notes);
        Assert.Equal(["RHYDON", "KANGASKHAN", "NIDORAN♂", "CLEFAIRY", "MISSINGNO."], d.Species.Skip(1).Select(s => s.Name));
        Assert.Equal([(int)Species.Rhydon, (int)Species.Kangaskhan, (int)Species.NidoranM, (int)Species.Clefairy], d.Species.Skip(1).Take(4).Select(s => s.National ?? 0));
        Assert.Null(d.Species[5].National);
        Assert.Equal([1, 2, 3, 4], d.Moves.Skip(1).Select(m => m.National ?? 0));
        Assert.Equal(1, d.Items[1].National);
        Assert.Equal(2, d.Items[2].National);
        Assert.Equal(3, d.Items[3].National);
        Assert.Null(d.Items[4].National);
    }

    [Fact]
    public void BaseStatsFillByDexNumberInBothGenerations()
    {
        var rom = Gen1();
        byte[] decoy = [1, 45, 49, 49, 45, 65, 9, 9];
        decoy.CopyTo(rom, 0x30000);
        var g1 = 0x383DE;
        byte[] bulbasaur = [1, 45, 49, 49, 45, 65, 0x16, 0x03, 45, 64];
        bulbasaur.CopyTo(rom, g1);
        rom[g1 + 19] = 3;
        byte[] ivysaur = [2, 60, 62, 63, 60, 80];
        ivysaur.CopyTo(rom, g1 + GbHackData.Gen1BaseSize);
        var d1 = GbHackData.Read(rom).Data!;
        Assert.Empty(d1.Species[1].BaseStats);
        Fixed(rom, 0x1C21E + 5 * GbHackData.NameWidth, "BULBASAUR", "IVYSAUR");
        d1 = GbHackData.Read(rom).Data!;
        var bulba = d1.Species.First(x => x.Name == "BULBASAUR");
        Assert.Equal([45, 49, 49, 45, 65, 65], bulba.BaseStats);
        Assert.Equal([0x16, 0x03], bulba.Types);
        Assert.Equal((45, 64, 3), (bulba.CatchRate, bulba.ExpYield, bulba.Growth));
        Assert.Contains(d1.Notes, n => n.StartsWith("base stats for 2 species"));
        Assert.Equal(HackTableStatus.Read, d1.StatusOf(HackTableStatus.Species));
        Assert.Null(d1.Table(HackTableStatus.Species)!.Aligned);
        Assert.True(d1.SpeciesOverlayable);

        var rom2 = new byte[0x200000];
        Fixed(rom2, 0x53384, "BULBASAUR", "IVYSAUR", "VENUSAUR");
        var g2 = 0x51424;
        byte[] b2 = [1, 45, 49, 49, 45, 65, 65, 22, 3, 45, 64, 0, 0, 31];
        b2.CopyTo(rom2, g2);
        rom2[g2 + 22] = 3;
        byte[] i2 = [2, 60, 62, 63, 60, 80, 80];
        i2.CopyTo(rom2, g2 + GbHackData.Gen2BaseSize);
        var d2 = GbHackData.Read(rom2).Data!;
        Assert.Equal([45, 49, 49, 45, 65, 65], d2.Species[1].BaseStats);
        Assert.Equal((31, 3), (d2.Species[1].GenderRatio, d2.Species[1].Growth));
        Assert.Equal([60, 62, 63, 60, 80, 80], d2.Species[2].BaseStats);
        Assert.Empty(d2.Species[3].BaseStats);
        Assert.True(d2.Table(HackTableStatus.Species)!.Aligned);
    }

    [Fact]
    public void Gen2TableIsInNationalOrder()
    {
        var rom = new byte[0x200000];
        Fixed(rom, 0x53384, "BULBASAUR", "IVYSAUR", "VENUSAUR", "CHARMANDER");
        var d = GbHackData.Read(rom).Data!;
        Assert.Contains("Gen 2", d.Notes);
        Assert.Equal([1, 2, 3, 4], d.Species.Skip(1).Select(s => s.National ?? 0));
        Assert.Contains("no move-name list was found", d.Notes);
    }

    [Fact]
    public void Gen2ItemsMapThroughTheGen2IdWhenTheNameMatches()
    {
        var rom = new byte[0x200000];
        Fixed(rom, 0x53384, "BULBASAUR", "IVYSAUR", "VENUSAUR");
        List(rom, 0x1C8000, "MASTER BALL", "ULTRA BALL", "BRIGHTPOWDER");
        var d = GbHackData.Read(rom).Data!;
        Assert.Equal(1, d.Items[1].National);
        Assert.Equal(2, d.Items[2].National);
        Assert.Equal(ItemConverter.GetItemFuture2(3), d.Items[3].National);
    }

    [Fact]
    public void SymFileFindsAHackTableWithoutTheAnchor()
    {
        var rom = new byte[0x100000];
        Fixed(rom, 0x0E * 0x4000 + 0x21E, "ZORUAX", "PIKACHU", "RAICHU");
        var sym = "; File generated by rgblink\n00:0150 Start\n0e:421e MonsterNames ; the names\n01:7f00 Foo\nnot a line\n";
        Assert.False(GbHackData.Read(rom).Ok);
        var r = GbHackData.Read(rom, sym);
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal(GbHackData.ReaderSym, r.Data!.Reader);
        Assert.Equal(["ZORUAX", "PIKACHU", "RAICHU"], r.Data.Species.Skip(1).Select(s => s.Name));
        Assert.Null(r.Data.Species[1].National);
        Assert.Equal((int)Species.Pikachu, r.Data.Species[2].National);
    }

    [Fact]
    public void SymOffsetsFollowTheBankRule()
    {
        var sym = SymFile.Parse("00:0150 Start\n0e:421e MonsterNames\n00:4000 Wrong\n03:3fff AlsoWrong\n00:c000 wRam\n01:7f00 Foo ; tail\n");
        Assert.Equal(6, sym.Count);
        Assert.Equal(0x150, sym.RomOffset("Start"));
        Assert.Equal(0x0E * 0x4000 + 0x21E, sym.RomOffset("MonsterNames"));
        Assert.Equal(0x7F00, sym.RomOffset("Foo"));
        Assert.Null(sym.RomOffset("Wrong"));
        Assert.Null(sym.RomOffset("AlsoWrong"));
        Assert.Null(sym.RomOffset("wRam"));
        Assert.Null(sym.RomOffset("Missing"));
    }

    [Fact]
    public void ImagesWithoutAnyTableAreRefusedWithAReason()
    {
        Assert.Contains("no .sym file", GbHackData.Read(new byte[0x8000]).Refusal);
        Assert.Contains("names neither", GbHackData.Read(new byte[0x8000], "00:0150 Start\n").Refusal);
        Assert.Contains("32 KiB to 8 MiB", GbHackData.Read(new byte[16]).Refusal);
    }

    [Fact]
    public void TheAnchorRouteStopsAtTheVanillaTableSize()
    {
        var rom = Gen1();
        for (var i = 5; i < 300; i++)
            GbHackData.Padded("PADDING").CopyTo(rom, 0x1C21E + i * GbHackData.NameWidth);
        Assert.Equal(GbHackData.Gen1SpeciesSlots + 1, GbHackData.Read(rom).Data!.Species.Count);
    }

    [Fact]
    public void NamesEncodeAndDecodeBothWays()
    {
        Assert.Equal("NIDORAN♂", GbHackData.Text(GbHackData.Padded("NIDORAN♂")));
        Assert.Equal("MR.MIME", GbHackData.Text(GbHackData.Padded("MR.MIME")));
        Assert.Null(GbHackData.Text([0x00, 0x01, 0x50]));
        Assert.Null(GbHackData.Text([GbHackData.Terminator]));
    }

    private static byte[] TotalConversion()
    {
        var rom = new byte[0x100000];
        const int names = GbStructureTests.NamesAt;
        const int map = GbStructureTests.MapAt;
        const int baseAt = GbStructureTests.BaseAt;
        rom.AsSpan(map - 16, 16).Fill(0xFF);
        rom.AsSpan(map + GbStructureTests.VanillaSlots, 16).Fill(0xFF);
        for (var i = 1; i <= GbStructureTests.VanillaSlots; i++)
        {
            GbHackData.Padded(GbStructureTests.MadeUp("ZQ", i)).CopyTo(rom, names + ((i - 1) * GbHackData.NameWidth));
            var dex = SpeciesConverter.GetNational1((byte)i);
            rom[map + i - 1] = (byte)dex;
            if (dex == 0)
                continue;
            byte[] row = [(byte)dex, (byte)(40 + (dex % 50)), 60, 61, 62, 63, 0x14, 0x15, 45, 64];
            row.CopyTo(rom, baseAt + ((dex - 1) * GbHackData.Gen1BaseSize));
        }
        GbStructureTests.List(Enumerable.Range(1, GbStructureTests.GbMoves).Select(m => GbStructureTests.MadeUp("ZAP", m))).CopyTo(rom, GbStructureTests.MoveNamesAt);
        for (var m = 1; m <= GbStructureTests.GbMoves; m++)
        {
            byte[] row = [(byte)m, 0, (byte)(5 * (m % 20)), (byte)(m % 2 == 0 ? 0x14 : 0x00), 0xFF, 20];
            row.CopyTo(rom, GbStructureTests.MovesAt + ((m - 1) * GbHackData.Gen1MoveSize));
        }
        return rom;
    }

    [Fact]
    public void AnImageWithEveryNameReplacedIsReadByStructureAndNotOverlaid()
    {
        var r = GbHackData.Read(TotalConversion());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(GbHackData.ReaderStructure, d.Reader);
        Assert.Equal(GbStructureTests.VanillaSlots + 1, d.Species.Count);
        Assert.Equal(151, d.Species.Count(s => s.BaseStats.Count == 6));
        Assert.Equal((HackTableStatus.Partial, (bool?)false), (d.StatusOf(HackTableStatus.Species), d.Table(HackTableStatus.Species)!.Aligned));
        Assert.False(d.SpeciesOverlayable);
        Assert.Equal(HackTableStatus.Partial, d.StatusOf(HackTableStatus.Moves));
        Assert.Equal((25, 20), (d.Moves[5].Power, d.Moves[5].Pp));
        Assert.Contains(d.Notes, n => n.Contains("chosen by count", StringComparison.Ordinal));
        Assert.Contains(d.Notes, n => n.Contains("found by structure only", StringComparison.Ordinal));
        Assert.True(PKHaX.Hacks.Overlay.MoveOverlay.From(d, GameVersion.RD).IsEmpty);
    }

    [Fact]
    public void WithoutAnyNameRunTheRefusalSaysWhatStructureLookedFor()
    {
        var rom = new byte[0x100000];
        for (var dex = 1; dex <= 151; dex++)
        {
            byte[] row = [(byte)dex, 50, 60, 61, 62, 63];
            row.CopyTo(rom, 0x38000 + ((dex - 1) * GbHackData.Gen1BaseSize));
        }
        var refusal = GbHackData.Read(rom).Refusal;
        Assert.Contains("no .sym file", refusal, StringComparison.Ordinal);
        Assert.Contains("Gen 1 base rows were found", refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void MoveAndItemListsWithoutTheirAnchorNamesAreFoundByAVote()
    {
        var rom = GbStructureTests.WithMoves(GbStructureTests.OwnDex());
        var en = GameInfo.GetStrings("en");
        rom.AsSpan(GbStructureTests.MoveNamesAt, 0x2000).Clear();
        var moves = new List<string> { "ZQX", "QQV", "XQZ" };
        moves.AddRange(Enumerable.Range(1, GbStructureTests.GbMoves).Select(m => m == 2 ? "KARATECHOP" : GbStructureTests.GbName(en.movelist[m], 12)));
        GbStructureTests.List(moves).CopyTo(rom, GbStructureTests.MoveNamesAt);
        var g1 = en.GetItemStrings(EntityContext.Gen1);
        var items = Enumerable.Range(1, 83).Select(i => i == 2 ? "ULTRABALL" : GbStructureTests.GbName(g1[i], 12).Trim()).Select(n => n.Length == 0 || !char.IsLetter(n[0]) ? "UNUSED" : n);
        GbStructureTests.List(items).CopyTo(rom, 0xB4000);
        var d = GbHackData.Read(rom).Data!;
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Moves));
        Assert.Equal(GbStructureTests.GbMoves + 1, d.Moves.Count);
        Assert.Equal(("POUND", (int?)1), (d.Moves[1].Name, d.Moves[1].National));
        Assert.Equal(75, d.Moves[7].Power);
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Items));
        Assert.Equal(("MASTER BALL", (int?)1), (d.Items[1].Name, d.Items[1].National));
        Assert.Equal(84, d.Items.Count);
        Assert.Contains(d.Notes, n => n.Contains("the first 3 names do not line up with the stock move order", StringComparison.Ordinal));
    }

    [Fact]
    public void TheGameBoyReadCarriesPlatformEngineAndStatus()
    {
        var d = GbHackData.Read(Gen1()).Data!;
        Assert.Equal((HackTables.Gb, HackTables.Gb1Engine), (d.Platform, d.Engine));
        Assert.Equal(HackTableStatus.Partial, d.StatusOf(HackTableStatus.Species));
        Assert.Equal(HackTableStatus.Partial, d.StatusOf(HackTableStatus.Moves));
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Items));
        Assert.Equal(HackTableStatus.Missing, d.StatusOf(HackTableStatus.Abilities));
        Assert.False(d.SpeciesOverlayable);
        Assert.Equal(HackTableKey.Of(d), d.TableKey);
        Assert.Equal(HackTables.Gb, HackTables.Sniff(Gen1()));
    }
}
