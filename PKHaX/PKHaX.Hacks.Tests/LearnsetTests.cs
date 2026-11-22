using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Analysis;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class LearnsetTests
{
    private const int Lists = 0x1D000;
    private const int Table = 0x1F000;
    private const int Decoy = 0x1F400;

    private static void Ptr(byte[] rom, int at, int offset) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), (uint)(0x08000000 + offset));

    private static void U16(byte[] rom, int at, int value) => BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(at), (ushort)value);

    private static int Packed(byte[] rom, int at, params (int Level, int Move)[] moves)
    {
        for (var i = 0; i < moves.Length; i++)
            U16(rom, at + i * 2, moves[i].Level << 9 | moves[i].Move);
        U16(rom, at + moves.Length * 2, 0xFFFF);
        return at + moves.Length * 2 + 2;
    }

    private static byte[] WithPackedTable(int entries = 20)
    {
        var rom = HackDataTests.Image();
        var at = Lists;
        for (var i = 0; i < entries; i++)
        {
            Ptr(rom, Table + i * 4, at);
            at = i == 1 ? Packed(rom, at, (1, 33), (4, 45), (7, 73)) : Packed(rom, at, (1, 1 + i % 3));
        }
        var decoy = 0x1E800;
        for (var i = 0; i < 25; i++)
        {
            Ptr(rom, Decoy + i * 4, decoy);
            decoy = Packed(rom, decoy, (5, 400 + i));
        }
        return rom;
    }

    [Fact]
    public void PackedTableIsTheLongestRunOfValidListPointers()
    {
        var d = GbaHackData.Read(WithPackedTable()).Data!;
        Assert.Contains(d.Notes, n => n == $"level-up learnsets: packed pointer table at 0x{Table:X}, 7 of 7 species");
        Assert.Equal([new HackLearn(1, 33), new HackLearn(4, 45), new HackLearn(7, 73)], d.LevelUp[1]);
        Assert.Equal(7, d.LevelUp.Count);
    }

    [Fact]
    public void ListsThatBreakTheRulesAreNotLearnsets()
    {
        var rom = new GbaImage(WithPackedTable());
        Assert.Null(Gen3Learnsets.Decode(rom, 0x1E800, LearnsetFormat.Packed, 355));
        var bytes = WithPackedTable();
        Packed(bytes, 0x1E000, (9, 10), (3, 11));
        Assert.Equal(2, Gen3Learnsets.Decode(new GbaImage(bytes), 0x1E000, LearnsetFormat.Packed, 355)!.Count);
        Packed(bytes, 0x1E000, (9, 10), (101, 11));
        Assert.Null(Gen3Learnsets.Decode(new GbaImage(bytes), 0x1E000, LearnsetFormat.Packed, 355));
        Assert.Contains("no level-up learnset pointer table", string.Join("|", GbaHackData.Read(HackDataTests.Image()).Data!.Notes));
    }

    [Fact]
    public void ExpansionListsAreReadThroughTheirSpeciesInfoField()
    {
        var rom = RhhHackDataTests.Image();
        var at = 0x78000;
        for (var i = 1; i <= 3; i++)
        {
            Ptr(rom, 0x20000 + i * 0xC4 + 0x48, at);
            U16(rom, at, 1);
            U16(rom, at + 2, 1);
            U16(rom, at + 4, 1 + i);
            U16(rom, at + 6, 3 * i);
            U16(rom, at + 8, 0xFFFF);
            at += 12;
        }
        var d = GbaHackData.Read(rom).Data!;
        Assert.Contains(d.Notes, n => n == "level-up learnsets: wide lists through gSpeciesInfo +0x48, 3 of 7 species");
        Assert.Equal([new HackLearn(1, 1), new HackLearn(6, 3)], d.LevelUp[2]);
    }

    [Fact]
    public void LearnsetsRoundTripThroughTheCache()
    {
        var root = Path.Combine(Path.GetTempPath(), "pkhax-learn-" + Guid.NewGuid().ToString("N"));
        try
        {
            var built = HackDataCache.ReadOrBuild(root, WithPackedTable()).Data!;
            var loaded = HackDataCache.Load(root, built.RomSha1)!;
            Assert.Equal(built.LevelUp.Keys.Order(), loaded.LevelUp.Keys.Order());
            Assert.Equal(built.LevelUp[1], loaded.LevelUp[1]);
            Assert.Contains("\"levelUp\"", File.ReadAllText(HackDataCache.PathFor(root, built.RomSha1)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AnEmptyOrUnsortedListDoesNotBreakTheTable()
    {
        var rom = WithPackedTable();
        U16(rom, 0x1E600, 0xFFFF);
        Ptr(rom, Table, 0x1E600);
        var unsorted = Packed(rom, 0x1E610, (9, 10), (3, 11));
        Ptr(rom, Table + 5 * 4, 0x1E610);
        var d = GbaHackData.Read(rom).Data!;
        Assert.Contains(d.Notes, n => n.StartsWith($"level-up learnsets: packed pointer table at 0x{Table:X}"));
        Assert.Empty(d.LevelUp[0]);
        Assert.Equal([new HackLearn(1, 33), new HackLearn(4, 45), new HackLearn(7, 73)], d.LevelUp[1]);
        Assert.Equal([new HackLearn(9, 10), new HackLearn(3, 11)], d.LevelUp[5]);
        Assert.True(unsorted > 0x1E610);
    }
}
