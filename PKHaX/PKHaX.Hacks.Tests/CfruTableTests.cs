using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class CfruTableTests
{
    private const int HeaderItems = 0x15000;
    private const int HeaderAbilities = 0x12000;
    private const int StaleLearnsets = 0x16000;
    private const int StaleLists = 0x16200;
    private const int RealLearnsets = 0x17000;
    private const int RealLists = 0x17200;
    private const int RealItems = 0x18000;
    private const int RealAbilities = 0x1A000;
    private const int Code = 0x1C000;
    private const int StaleRows = 30;
    private const int RealRows = 20;
    private const int ItemRows = 120;
    private const int StaleAbilities = 21;

    private static void Ptr(byte[] rom, int at, int offset) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), (uint)(0x08000000 + offset));

    private static void U16(byte[] rom, int at, int value) => BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(at), (ushort)value);

    private static void Name(byte[] rom, int at, string text, int length)
    {
        rom.AsSpan(at, length).Fill(0xFF);
        StringConverter3.SetString(rom.AsSpan(at, length), text, length - 1, (int)LanguageID.English);
    }

    private static int Triple(byte[] rom, int at, params (int Level, int Move)[] moves)
    {
        foreach (var (level, move) in moves)
        {
            U16(rom, at, move);
            rom[at + 2] = (byte)level;
            at += 3;
        }
        U16(rom, at, 0);
        rom[at + 2] = Gen3Learnsets.TripleEndLevel;
        return at + 3;
    }

    private static int Packed(byte[] rom, int at, params (int Level, int Move)[] moves)
    {
        foreach (var (level, move) in moves)
        {
            U16(rom, at, level << 9 | move);
            at += 2;
        }
        U16(rom, at, 0xFFFF);
        return at + 2;
    }

    internal static byte[] RelocatedLearnsets(int codeWords = 3)
    {
        var rom = HackDataTests.Image(code: "BPRE");
        var at = StaleLists;
        for (var i = 0; i < StaleRows; i++)
        {
            Ptr(rom, StaleLearnsets + i * 4, at);
            at = Packed(rom, at, (1, 33), (4, 10 + i));
        }
        at = RealLists;
        for (var i = 0; i < RealRows; i++)
        {
            Ptr(rom, RealLearnsets + i * 4, at);
            at = Triple(rom, at, (1, 33), (5, 300 + i));
        }
        for (var i = 0; i < codeWords; i++)
            Ptr(rom, Code + i * 16, RealLearnsets);
        return rom;
    }

    internal static byte[] PermutedItems(bool lostIds = false)
    {
        var rom = HackDataTests.Image(code: "BPRE");
        for (var i = 4; i < ItemRows; i++)
        {
            var b = HeaderItems + i * 44;
            Name(rom, b, i == 54 ? "" : $"ITEM{i:000}", 14);
            if (i == 54)
                rom.AsSpan(b, 14).Fill(0xFF);
            var id = i is >= 60 and < 80 ? i + 9 : i is >= 80 and < 89 ? i - 20 : i;
            U16(rom, b + 0x0E, lostIds && id != i ? 0xFFFF : id);
            rom[b + 0x1A] = 1;
        }
        return rom;
    }

    internal static byte[] RelocatedNames(int codeWords = 3)
    {
        var rom = HackDataTests.Image(code: "BPRE");
        string[] stock = ["????????", "MASTER BALL", "ULTRA BALL", "GREAT BALL"];
        for (var i = 0; i < 60; i++)
        {
            var b = RealItems + i * 44;
            Name(rom, b, i < stock.Length ? stock[i] : $"NEWITEM{i:00}", 14);
            U16(rom, b + 0x0E, i);
            rom[b + 0x1A] = 2;
        }
        Name(rom, HeaderAbilities + 3 * 13, "SPEED BOOST", 13);
        for (var i = 4; i < StaleAbilities; i++)
            Name(rom, HeaderAbilities + i * 13, $"OLDABIL{i:00}", 13);
        string[] abilities = ["-------", "STENCH", "DRIZZLE", "SPEED BOOST", "BATTLE ARMOR", "MEGA SOL"];
        for (var i = 0; i < abilities.Length; i++)
            Name(rom, RealAbilities + i * 17, abilities[i], 17);
        for (var i = 0; i < codeWords; i++)
        {
            Ptr(rom, Code + i * 16, RealItems);
            Ptr(rom, Code + i * 16 + 4, RealAbilities);
        }
        return rom;
    }

    [Fact]
    public void TheLearnsetTableTheCodeLoadsBeatsALongerOneNothingPointsAt()
    {
        var d = GbaHackData.Read(RelocatedLearnsets()).Data!;
        Assert.Contains(d.Notes, n => n == $"level-up learnsets: triple pointer table at 0x{RealLearnsets:X}, 7 of 7 species");
        Assert.Contains(d.Notes, n => n == $"the level-up table is the one the game code loads: 3 ROM words outside the GF header point at 0x{RealLearnsets:X}; the longer run of {StaleRows} packed list pointers at 0x{StaleLearnsets:X}, which no ROM word outside the GF header points at, is not used");
        Assert.Equal([new HackLearn(1, 33), new HackLearn(5, 301)], d.LevelUp[1]);
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.LevelUp));
    }

    [Fact]
    public void WithoutACodePointerTheLongestRunStillWins()
    {
        var d = GbaHackData.Read(RelocatedLearnsets(codeWords: 0)).Data!;
        Assert.Contains(d.Notes, n => n == $"level-up learnsets: packed pointer table at 0x{StaleLearnsets:X}, 7 of 7 species");
        Assert.DoesNotContain(d.Notes, n => n.StartsWith("the level-up table is the one the game code loads", StringComparison.Ordinal));
    }

    [Fact]
    public void TripleListsDecodeAndEndAtMoveZeroLevel255()
    {
        var bytes = HackDataTests.Image(code: "BPRE");
        Triple(bytes, 0x1D001, (1, 33), (7, 354));
        var rom = new GbaImage(bytes);
        Assert.Equal([new HackLearn(1, 33), new HackLearn(7, 354)], Gen3Learnsets.Decode(rom, 0x1D001, LearnsetFormat.Triple, 355));
        Assert.Null(Gen3Learnsets.Decode(rom, 0x1D001, LearnsetFormat.Triple, 354));
        Triple(bytes, 0x1D001, (1, 33), (101, 40));
        Assert.Null(Gen3Learnsets.Decode(new GbaImage(bytes), 0x1D001, LearnsetFormat.Triple, 355));
    }

    [Fact]
    public void ItemRowsWithAnotherItemsIdStillReadToTheEndOfTheTable()
    {
        var d = GbaHackData.Read(PermutedItems()).Data!;
        Assert.Equal(ItemRows, d.Items.Count);
        Assert.Equal("ITEM070", d.Items[70].Name);
        Assert.Equal("ITEM085", d.Items[85].Name);
        Assert.Equal("", d.Items[54].Name);
        Assert.Equal("ITEM119", d.Items[119].Name);
        Assert.Contains(d.Notes, n => n.StartsWith("29 named item rows carry another item's id at +0xE", StringComparison.Ordinal));
    }

    [Fact]
    public void ItemIdsFarFromTheirRowStillEndTheTable()
    {
        var d = GbaHackData.Read(PermutedItems(lostIds: true)).Data!;
        Assert.Equal(54, d.Items.Count);
        Assert.DoesNotContain(d.Notes, n => n.Contains("another item's id", StringComparison.Ordinal));
    }

    [Fact]
    public void TheItemAndAbilityTablesTheCodeLoadsBeatTheHeadersStaleOnes()
    {
        var d = GbaHackData.Read(RelocatedNames()).Data!;
        Assert.Equal(60, d.Items.Count);
        Assert.Equal("NEWITEM59", d.Items[59].Name);
        Assert.Equal(6, d.Abilities.Count);
        Assert.Equal("MEGA SOL", d.Abilities[5].Name);
        Assert.Equal(GbaHackData.ReaderFrFollow, d.Reader);
        Assert.Contains(d.Notes, n => n.StartsWith($"items at 0x{RealItems:X} stride 44 (3 references), not the GF header's 0x{HeaderItems:X}", StringComparison.Ordinal));
        Assert.Contains(d.Notes, n => n.StartsWith($"ability names at 0x{RealAbilities:X} stride 17 (3 references), not the GF header's 0x{HeaderAbilities:X}", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutACodePointerTheHeadersTablesAreKept()
    {
        var d = GbaHackData.Read(RelocatedNames(codeWords: 0)).Data!;
        Assert.Equal(5, d.Items.Count);
        Assert.Equal(StaleAbilities, d.Abilities.Count);
        Assert.Equal(GbaHackData.ReaderGf, d.Reader);
        Assert.True(d.Items.All(i => i.Id == 0 || !i.Name.StartsWith("NEWITEM", StringComparison.Ordinal)));
    }
}
