using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Saves.Cfru;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class CfruBoxTableTests
{
    private const int TableAt = 0x400;
    private static readonly CfruRamMap Map = CfruRamMap.FireRed10;

    private static uint[] Addresses(int storage = 19, int extra = 3, int saveBlock1 = 2, int saveBlock2 = 1)
    {
        var entries = new List<uint>();
        void Run(uint first, int count)
        {
            for (var i = 0; i < count; i++)
                entries.Add(first + (uint)(i * CfruBoxTable.BoxBytes));
        }
        Run(Map.Storage + 4, storage);
        Run(Map.ExtraSectors + 0xB0C, extra);
        Run(Map.SaveBlock1 + 0x1F08, saveBlock1);
        Run(Map.SaveBlock2 + 0xB0, saveBlock2);
        return [.. entries];
    }

    private static byte[] Image(uint[] entries, string code = "BPRE", byte revision = 0, int at = TableAt)
    {
        var rom = new byte[0x1000];
        System.Text.Encoding.ASCII.GetBytes(code).CopyTo(rom, 0xAC);
        rom[CfruRamMap.RevisionAt] = revision;
        for (var i = 0; i < entries.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at + (i * 4)), entries[i]);
        return rom;
    }

    private static byte[] Record(ushort species)
    {
        var r = new byte[CfruBoxTable.RecordSize];
        BinaryPrimitives.WriteUInt32LittleEndian(r, 0x5EED_0001);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(4), 0x0001_0002);
        r.AsSpan(8, 10).Fill(0xFF);
        r[8] = 0xBB;
        r[0x12] = 2;
        r[0x13] = 2;
        r.AsSpan(0x14, 7).Fill(0xFF);
        r[0x14] = 0xBC;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x1C), species);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x20), 1000);
        r[0x25] = 70;
        r[0x26] = 4;
        r[0x27] = 33;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x36), 31 | (20u << 5));
        return r;
    }

    private static Func<CfruBoxArea, ReadOnlyMemory<byte>> Save(Action<CfruBoxArea, byte[]>? fill = null)
    {
        var areas = new Dictionary<CfruBoxArea, byte[]>
        {
            [CfruBoxArea.Storage] = new byte[Map.StorageSize],
            [CfruBoxArea.Sectors30And31] = new byte[Map.ExtraSectorsSize],
            [CfruBoxArea.SaveBlock1] = new byte[Map.SaveBlock1Size],
            [CfruBoxArea.SaveBlock2] = new byte[Map.SaveBlock2Size],
        };
        foreach (var pair in areas)
            fill?.Invoke(pair.Key, pair.Value);
        return area => areas[area];
    }

    private static void Placed(CfruBoxRun run, int first, int count, CfruBoxArea area, int offset)
    {
        Assert.Equal((first, count, offset), (run.FirstBox, run.Count, run.Offset));
        Assert.Equal(area, run.Area);
        Assert.Equal(CfruRunTier.Derived, run.Tier);
        Assert.True(run.Located);
    }

    [Fact]
    public void BoxBytesIsThirtyCompactRecords()
    {
        Assert.Equal(0x6CC, CfruBoxTable.BoxBytes);
        Assert.Equal(8, CfruBoxTable.EngineRun);
    }

    [Fact]
    public void ATwentyFiveEntryTableSplitsIntoFourRuns()
    {
        var t = CfruBoxTable.Read(new GbaImage(Image(Addresses())))!;
        Assert.Equal(TableAt, t.Offset);
        Assert.Equal(25, t.BoxCount);
        Assert.Equal(4, t.Runs.Count);
        Placed(t.Runs[0], 1, 19, CfruBoxArea.Storage, 4);
        Placed(t.Runs[1], 20, 3, CfruBoxArea.Sectors30And31, 0xB0C);
        Placed(t.Runs[2], 23, 2, CfruBoxArea.SaveBlock1, 0x1F08);
        Placed(t.Runs[3], 25, 1, CfruBoxArea.SaveBlock2, 0xB0);
        Assert.True(t.Runs[0].WritableFromRomAlone);
        Assert.True(t.Runs[1].WritableFromRomAlone);
        Assert.False(t.Runs[2].WritableFromRomAlone);
        Assert.True(t.Runs[3].NeedsSaveCheck);
        Assert.False(t.Runs[3].Readable);
        Assert.Equal(24, t.Sources().Sum(s => s.Count));
    }

    [Fact]
    public void ATwentyTwoEntryTableSplitsIntoTwoRuns()
    {
        var t = CfruBoxTable.Read(new GbaImage(Image(Addresses(saveBlock1: 0, saveBlock2: 0))))!;
        Assert.Equal(22, t.BoxCount);
        Assert.Equal(2, t.Runs.Count);
        Placed(t.Runs[0], 1, 19, CfruBoxArea.Storage, 4);
        Placed(t.Runs[1], 20, 3, CfruBoxArea.Sectors30And31, 0xB0C);
        var sources = t.Sources();
        Assert.Equal(2, sources.Count);
        Assert.All(sources, s => Assert.True(s.Measured));
    }

    [Fact]
    public void TheTableIsFoundWhereverItSits()
    {
        var t = CfruBoxTable.Read(Image(Addresses(), at: 0x7A4), Map)!;
        Assert.Equal(0x7A4, t.Offset);
        Assert.Equal(25, t.BoxCount);
        Assert.Null(CfruBoxTable.Read(Image(Addresses(), at: 0x7A6), Map));
    }

    [Fact]
    public void ARunOfEightMarksTheEngineAndSevenDoesNot()
    {
        var eight = Image(Addresses(storage: 8, extra: 0, saveBlock1: 0, saveBlock2: 0));
        var seven = Image(Addresses(storage: 7, extra: 0, saveBlock1: 0, saveBlock2: 0));
        Assert.True(CfruBoxTable.HasRun(new GbaImage(eight)));
        Assert.True(CfruBoxTable.HasRun(new GbaImage(eight), min: 8));
        Assert.False(CfruBoxTable.HasRun(new GbaImage(seven)));
        Assert.True(CfruBoxTable.HasRun(new GbaImage(seven), 7));
        Assert.False(CfruBoxTable.HasRun(new GbaImage(new byte[0x1000])));
        Assert.Equal((TableAt, 7), CfruBoxTable.LongestRun(seven));
        Assert.Null(CfruBoxTable.Read(Image(Addresses(storage: 2, extra: 0, saveBlock1: 0, saveBlock2: 0)), Map));
    }

    [Fact]
    public void ARunOutsideEveryAreaIsNotLocated()
    {
        uint[] lost = [0x02000100, 0x02000100 + CfruBoxTable.BoxBytes, 0x02000100 + (2 * CfruBoxTable.BoxBytes)];
        var t = CfruBoxTable.Read(Image([.. Addresses(extra: 0, saveBlock1: 0, saveBlock2: 0), .. lost]), Map)!;
        Assert.Equal(2, t.Runs.Count);
        Assert.Null(t.Runs[1].Area);
        Assert.Equal(CfruRunTier.Unknown, t.Runs[1].Tier);
        Assert.False(t.Runs[1].Readable);
        Assert.Contains("not located", t.Runs[1].Origin);
        Assert.Null(t.Runs[1].Source());
        Assert.Equal(19, t.Sources().Sum(s => s.Count));
    }

    [Fact]
    public void ARunThatWouldLeaveItsAreaIsNotLocated()
    {
        var late = Map.SaveBlock2 + (uint)Map.SaveBlock2Size - 0x100;
        var t = CfruBoxTable.Read(Image([.. Addresses(saveBlock2: 0), late]), Map)!;
        var last = t.Runs[^1];
        Assert.Equal((25, 1), (last.FirstBox, last.Count));
        Assert.False(last.Located);
        Assert.Contains("run past", last.Origin);
    }

    [Fact]
    public void OverlappingRunsLoseTheShorterOne()
    {
        var inside = Map.Storage + 4 + (uint)(2 * CfruBoxTable.BoxBytes) + 8;
        var t = CfruBoxTable.Read(Image([.. Addresses(extra: 0, saveBlock1: 0, saveBlock2: 0), inside, inside + CfruBoxTable.BoxBytes]), Map)!;
        Assert.Equal(2, t.Runs.Count);
        Assert.True(t.Runs[0].Located);
        Assert.False(t.Runs[1].Located);
        Assert.Contains("overlap", t.Runs[1].Origin);
    }

    [Fact]
    public void AStrayPointerAfterTheTableIsNotABox()
    {
        var t = CfruBoxTable.Read(Image([.. Addresses(), Map.SaveBlock1 + 0x100]), Map)!;
        Assert.Equal(5, t.Runs.Count);
        Assert.True(t.Runs[4].Located);
        Assert.True(t.Runs[4].NeedsSaveCheck);
        Assert.False(t.Runs[4].Readable);
        var settled = t.WithSave(Save((area, bytes) =>
        {
            if (area == CfruBoxArea.SaveBlock1)
                bytes.AsSpan(0x100, CfruBoxTable.BoxBytes).Fill(0x5A);
            if (area == CfruBoxArea.SaveBlock2)
                Record(25).CopyTo(bytes.AsSpan(0xB0 + (4 * CfruBoxTable.RecordSize)));
        }));
        Assert.False(settled.Runs[4].Located);
        Assert.Equal(CfruRunTier.Unknown, settled.Runs[4].Tier);
        Assert.Contains("slot 1 is refused", settled.Runs[4].Origin);
        Assert.True(settled.Runs[3].Readable);
        Assert.True(settled.Runs[3].SaveChecked);
        Assert.False(settled.Runs[3].WritableFromRomAlone);
        Assert.False(settled.Runs[3].Source()!.Measured);
        Assert.Equal(25, settled.Sources().Sum(s => s.Count));
        Assert.False(t.Runs[3].SaveChecked);
    }

    [Fact]
    public void ALoneBoxIsRefusedWhenTheSaveAreaIsTooShort()
    {
        var t = CfruBoxTable.Read(Image(Addresses()), Map)!;
        var settled = t.WithSave(_ => new byte[0x80]);
        Assert.False(settled.Runs[3].Located);
        Assert.Contains("short of the box", settled.Runs[3].Origin);
        Assert.True(settled.Runs[0].Located);
    }

    [Theory]
    [InlineData("BPRE", 1)]
    [InlineData("BPEE", 0)]
    [InlineData("BPGE", 0)]
    public void OtherCodesAndRevisionsPlaceOnlyTheStorageRun(string code, int revision)
    {
        var rom = new GbaImage(Image(Addresses(), code, (byte)revision));
        Assert.Null(CfruRamMap.For(rom, out var why));
        Assert.Contains("revision 0 only", why);
        var t = CfruBoxTable.Read(rom)!;
        Assert.Null(t.Map);
        Assert.Equal(4, t.Runs.Count);
        Assert.Equal((CfruBoxArea.Storage, 4, CfruRunTier.Family), (t.Runs[0].Area!.Value, t.Runs[0].Offset, t.Runs[0].Tier));
        Assert.False(t.Runs[0].WritableFromRomAlone);
        Assert.All(t.Runs.Skip(1), r => Assert.False(r.Located));
        Assert.Contains(t.Notes, n => n.Contains(code));
    }

    [Fact]
    public void ALonePointerBeforeTheTableThatMapsNowhereIsDropped()
    {
        var t = CfruBoxTable.Read(Image([0x02000040, .. Addresses()]), Map)!;
        Assert.Equal(TableAt + 4, t.Offset);
        Assert.Equal(25, t.BoxCount);
        Assert.Equal(1, t.Runs[0].FirstBox);
        Assert.Contains(t.Notes, n => n.Contains("before the table"));
        Assert.False(t.NumberingProvisional);
    }

    [Theory]
    [InlineData("BPRE", 1)]
    [InlineData("BPEE", 0)]
    public void ALonePointerBeforeTheTableIsDroppedWithoutAMap(string code, int revision)
    {
        var t = CfruBoxTable.Read(new GbaImage(Image([0x02000040, Map.SaveBlock1 + 0x100, .. Addresses()], code, (byte)revision)))!;
        Assert.Null(t.Map);
        Assert.Equal(TableAt + 8, t.Offset);
        Assert.Equal(25, t.BoxCount);
        Assert.Equal((1, 19, CfruRunTier.Family), (t.Runs[0].FirstBox, t.Runs[0].Count, t.Runs[0].Tier));
        Assert.Equal(19, t.LocatedBoxCount);
        Assert.Contains(t.Notes, n => n.Contains("2 lone EWRAM pointer(s) before the table"));
    }

    [Fact]
    public void ALonePointerBeforeTheTableThatMapsIntoAnAreaDoesNotShiftBoxNumbers()
    {
        var t = CfruBoxTable.Read(Image([Map.SaveBlock1 + 0x100, .. Addresses()]), Map)!;
        Assert.Equal(TableAt, t.Offset);
        Assert.Equal(5, t.Runs.Count);
        Assert.True(t.NumberingProvisional);
        Assert.Equal((1, 1), (t.Runs[0].FirstBox, t.Runs[0].Count));
        Assert.False(t.Runs[0].Readable);
        Assert.Contains(t.Notes, n => n.Contains("provisional"));
        var settled = t.WithSave(Save((area, bytes) =>
        {
            if (area == CfruBoxArea.SaveBlock1)
                bytes.AsSpan(0x100, CfruBoxTable.BoxBytes).Fill(0x5A);
        }));
        Assert.Equal(TableAt + 4, settled.Offset);
        Assert.Equal(25, settled.BoxCount);
        Assert.Equal(4, settled.Runs.Count);
        Placed(settled.Runs[0], 1, 19, CfruBoxArea.Storage, 4);
        Placed(settled.Runs[1], 20, 3, CfruBoxArea.Sectors30And31, 0xB0C);
        Placed(settled.Runs[2], 23, 2, CfruBoxArea.SaveBlock1, 0x1F08);
        Assert.Equal(25, settled.Runs[3].FirstBox);
        Assert.True(settled.Runs[3].SaveChecked);
        Assert.False(settled.NumberingProvisional);
        Assert.Contains(settled.Notes, n => n.Contains("before the table is not a box"));
        Assert.DoesNotContain(settled.Notes, n => n.Contains("provisional"));
        Assert.Contains("entry 1 =", settled.Runs[0].Origin);
        var empty = t.WithSave(Save());
        Assert.Equal(TableAt, empty.Offset);
        Assert.True(empty.Runs[0].SaveChecked);
        Assert.False(empty.Runs[0].WritableFromRomAlone);
        Assert.False(empty.NumberingProvisional);
    }

    [Fact]
    public void ALonePointerAfterTheTableThatMapsNowhereIsDropped()
    {
        var t = CfruBoxTable.Read(Image([.. Addresses(), 0x02000040, 0x02000080]), Map)!;
        Assert.Equal(TableAt, t.Offset);
        Assert.Equal(25, t.BoxCount);
        Assert.Equal(25, t.LocatedBoxCount);
        Assert.Equal(4, t.Runs.Count);
        Assert.Equal(25, t.Runs[^1].LastBox);
        Assert.Contains(t.Notes, n => n.Contains("2 lone EWRAM pointer(s) after the table"));
    }

    private static uint[] Names(int stride = CfruBoxTable.NameStride)
    {
        var first = Map.Storage + 0x8344;
        return [.. Enumerable.Range(0, 25).Concat(Enumerable.Range(25, 25).Reverse()).Select(i => first + (uint)(i * stride))];
    }

    [Fact]
    public void ANamePointerTableAfterTheBoxTableIsNotCounted()
    {
        var t = CfruBoxTable.Read(new GbaImage(Image([.. Addresses(), .. Names()])))!;
        Assert.Equal(TableAt, t.Offset);
        Assert.Equal(25, t.BoxCount);
        Assert.Equal(25, t.Entries.Count);
        Assert.Equal(4, t.Runs.Count);
        Placed(t.Runs[0], 1, 19, CfruBoxArea.Storage, 4);
        Placed(t.Runs[1], 20, 3, CfruBoxArea.Sectors30And31, 0xB0C);
        Placed(t.Runs[2], 23, 2, CfruBoxArea.SaveBlock1, 0x1F08);
        Placed(t.Runs[3], 25, 1, CfruBoxArea.SaveBlock2, 0xB0);
        Assert.Equal(25, t.Runs[^1].LastBox);
        Assert.Single(t.Notes, n => n.Contains("name pointer table"));
        Assert.Contains(t.Notes, n => n.StartsWith("50 EWRAM pointer(s) after the table"));
        Assert.DoesNotContain(t.Notes, n => n.Contains("cut at") || n.Contains("lone"));
        Assert.All(t.Runs, r => Assert.DoesNotContain("run past", r.Origin));
        var settled = t.WithSave(Save());
        Assert.Equal(25, settled.BoxCount);
        Assert.Equal(25, settled.Sources().Sum(s => s.Count));
        var shape = Gen3ShapeFinder.Derive(new GbaImage(Image([.. Addresses(), .. Names()])));
        Assert.Contains(": 25 box pointers at ROM 0x400", shape.FamilyOrigin);
        Assert.DoesNotContain(shape.BoxNotes, n => n.StartsWith("boxes 26"));
    }

    [Fact]
    public void ANamePointerTableBeforeTheBoxTableIsNotCounted()
    {
        var names = Names();
        var t = CfruBoxTable.Read(Image([.. names, .. Addresses()]), Map)!;
        Assert.Equal(TableAt + (names.Length * 4), t.Offset);
        Assert.Equal(25, t.BoxCount);
        Placed(t.Runs[0], 1, 19, CfruBoxArea.Storage, 4);
        Assert.False(t.NumberingProvisional);
        Assert.Contains(t.Notes, n => n.StartsWith("50 EWRAM pointer(s) before the table"));
    }

    [Fact]
    public void ATableOfExactlyTheMostEntriesIsNotSaidToBeCut()
    {
        uint[] Row(int count) => [.. Enumerable.Range(0, count).Select(i => CfruBoxTable.Ewram + (uint)(i * CfruBoxTable.BoxBytes))];
        var full = CfruBoxTable.Read(Image(Row(CfruBoxTable.MaxEntries)), null)!;
        Assert.Equal(CfruBoxTable.MaxEntries, full.Entries.Count);
        Assert.DoesNotContain(full.Notes, n => n.Contains("cut at"));
        var over = CfruBoxTable.Read(Image(Row(CfruBoxTable.MaxEntries + 1)), null)!;
        Assert.Equal(CfruBoxTable.MaxEntries, over.Entries.Count);
        Assert.Contains(over.Notes, n => n.Contains("cut at"));
    }
}
