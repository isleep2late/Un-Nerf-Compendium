using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class NdsHackDataTests
{
    private static readonly GameStrings En = GameInfo.GetStrings("en");

    internal static byte[] BuildNarc(IReadOnlyList<byte[]> files)
    {
        var data = new MemoryStream();
        var fat = new List<(int, int)>();
        foreach (var f in files)
        {
            var start = (int)data.Length;
            data.Write(f);
            fat.Add((start, start + f.Length));
            while (data.Length % 4 != 0)
                data.WriteByte(0xFF);
        }
        var fatbSize = 12 + fat.Count * 8;
        var fntbSize = 16;
        var gmifSize = 8 + (int)data.Length;
        var total = 16 + fatbSize + fntbSize + gmifSize;
        var o = new byte[total];
        Encoding.ASCII.GetBytes("NARC").CopyTo(o, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(4), 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(6), 0x0100);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(8), (uint)total);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(12), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(14), 3);
        var p = 16;
        Encoding.ASCII.GetBytes("BTAF").CopyTo(o, p);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(p + 4), (uint)fatbSize);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(p + 8), (ushort)fat.Count);
        for (var i = 0; i < fat.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(p + 12 + i * 8), (uint)fat[i].Item1);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(p + 16 + i * 8), (uint)fat[i].Item2);
        }
        p += fatbSize;
        Encoding.ASCII.GetBytes("BTNF").CopyTo(o, p);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(p + 4), (uint)fntbSize);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(p + 8), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(p + 14), 1);
        p += fntbSize;
        Encoding.ASCII.GetBytes("GMIF").CopyTo(o, p);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(p + 4), (uint)gmifSize);
        data.ToArray().CopyTo(o, p + 8);
        return o;
    }

    private sealed class Dir
    {
        public readonly SortedDictionary<string, Dir> Dirs = new(StringComparer.Ordinal);
        public readonly List<(string Name, byte[] Data)> Files = [];
        public int Id;
        public int FirstFile;
        public int Parent;
    }

    internal static byte[] BuildNds(string gameCode, IReadOnlyDictionary<string, byte[]> files)
    {
        var root = new Dir();
        foreach (var (path, data) in files.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var parts = path.Split('/');
            var d = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (!d.Dirs.TryGetValue(parts[i], out var next))
                    d.Dirs[parts[i]] = next = new Dir();
                d = next;
            }
            d.Files.Add((parts[^1], data));
        }
        var order = new List<Dir>();
        var queue = new Queue<Dir>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var d = queue.Dequeue();
            d.Id = order.Count;
            order.Add(d);
            foreach (var c in d.Dirs.Values)
            {
                c.Parent = d.Id;
                queue.Enqueue(c);
            }
        }
        var fileData = new List<byte[]>();
        foreach (var d in order)
        {
            d.FirstFile = fileData.Count;
            fileData.AddRange(d.Files.Select(f => f.Data));
        }
        var fnt = new MemoryStream();
        var main = new byte[order.Count * 8];
        fnt.Write(main);
        foreach (var d in order)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(main.AsSpan(d.Id * 8), (uint)fnt.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(main.AsSpan(d.Id * 8 + 4), (ushort)d.FirstFile);
            BinaryPrimitives.WriteUInt16LittleEndian(main.AsSpan(d.Id * 8 + 6), (ushort)(d.Id == 0 ? order.Count : 0xF000 | d.Parent));
            foreach (var f in d.Files)
            {
                fnt.WriteByte((byte)f.Name.Length);
                fnt.Write(Encoding.ASCII.GetBytes(f.Name));
            }
            foreach (var (name, c) in d.Dirs)
            {
                fnt.WriteByte((byte)(0x80 | name.Length));
                fnt.Write(Encoding.ASCII.GetBytes(name));
                fnt.Write(BitConverter.GetBytes((ushort)(0xF000 | c.Id)));
            }
            fnt.WriteByte(0);
        }
        var fntBytes = fnt.ToArray();
        main.CopyTo(fntBytes, 0);
        var image = new MemoryStream();
        image.Write(new byte[NitroFs.HeaderSize]);
        var fntOffset = (int)image.Length;
        image.Write(fntBytes);
        while (image.Length % 4 != 0)
            image.WriteByte(0);
        var fatOffset = (int)image.Length;
        image.Write(new byte[fileData.Count * 8]);
        var fat = new List<(int, int)>();
        foreach (var f in fileData)
        {
            var start = (int)image.Length;
            image.Write(f);
            fat.Add((start, start + f.Length));
            while (image.Length % 4 != 0)
                image.WriteByte(0xFF);
        }
        var o = image.ToArray();
        Encoding.ASCII.GetBytes("POKEMON TEST").CopyTo(o, 0);
        Encoding.ASCII.GetBytes(gameCode).CopyTo(o, 0x0C);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x40), (uint)fntOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x44), (uint)fntBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x48), (uint)fatOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x4C), (uint)(fat.Count * 8));
        for (var i = 0; i < fat.Count; i++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(fatOffset + i * 8), (uint)fat[i].Item1);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(fatOffset + i * 8 + 4), (uint)fat[i].Item2);
        }
        return o;
    }

    internal static byte[] Gen4Text(IReadOnlyList<string> lines, ushort seed = 0x1234)
    {
        var count = lines.Count;
        var entries = new byte[4 + count * 8];
        BinaryPrimitives.WriteUInt16LittleEndian(entries, (ushort)count);
        BinaryPrimitives.WriteUInt16LittleEndian(entries.AsSpan(2), seed);
        var body = new MemoryStream();
        for (var i = 0; i < count; i++)
        {
            var buffer = new byte[(lines[i].Length + 1) * 2 + 8];
            var written = StringConverter4.SetString(buffer, lines[i], lines[i].Length, (int)LanguageID.English, StringConverterOption.None);
            var chars = written / 2 + 1;
            BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(written), 0xFFFF);
            var charKey = (ushort)((0x91BD3 * (i + 1)) & 0xFFFF);
            for (var c = 0; c < chars; c++)
            {
                var v = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(c * 2)) ^ charKey);
                BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(c * 2), v);
                charKey = (ushort)(charKey + 0x493D);
            }
            var offset = (uint)(entries.Length + body.Length);
            body.Write(buffer, 0, chars * 2);
            var key = (uint)((seed * 0x2FD * (i + 1)) & 0xFFFF);
            var keyFull = key | (key << 16);
            BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(4 + i * 8), offset ^ keyFull);
            BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(8 + i * 8), (uint)chars ^ keyFull);
        }
        return [.. entries, .. body.ToArray()];
    }

    internal static byte[] SectionText(IReadOnlyList<string> lines, bool gen5)
    {
        var count = lines.Count;
        var table = new byte[count * 8];
        var data = new MemoryStream();
        var key = GameText.Gen6BaseKey;
        for (var i = 0; i < count; i++)
        {
            ushort[] chars = [.. lines[i].Select(c => (ushort)c), 0xFFFF];
            if (gen5)
            {
                var k = (ushort)(0x5A5A + i);
                var k0 = k;
                for (var j = chars.Length - 1; j >= 0; j--)
                {
                    chars[j] ^= k;
                    k = (ushort)((k >> 3) | (k << 13));
                }
                Assert.Equal((ushort)(0xFFFF ^ k0), chars[^1]);
            }
            else
            {
                GameText.DecryptGen6(chars, key);
            }
            key = (ushort)(key + GameText.Gen6KeyAdvance);
            BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(i * 8), (uint)(4 + table.Length + data.Length));
            BinaryPrimitives.WriteUInt16LittleEndian(table.AsSpan(i * 8 + 4), (ushort)chars.Length);
            foreach (var c in chars)
                data.Write(BitConverter.GetBytes(c));
        }
        var sectionLength = 4 + table.Length + (int)data.Length;
        var o = new byte[0x10 + sectionLength];
        BinaryPrimitives.WriteUInt16LittleEndian(o, 1);
        BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(2), (ushort)count);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(4), (uint)sectionLength);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x0C), 0x10);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(0x10), (uint)sectionLength);
        table.CopyTo(o, 0x14);
        data.ToArray().CopyTo(o, 0x14 + table.Length);
        return o;
    }

    private static byte[][] Split(PersonalInfo[] entries) => [.. entries.Select(e => e.Write())];

    private static byte[][] RawGen4(byte[][] entries)
    {
        foreach (var e in entries)
            for (var at = 6; at <= 7; at++)
                e[at] = (byte)GameTypes.FromPkhex(4, e[at]);
        return entries;
    }

    private static PersonalInfo[] Entries(IPersonalTable t) => [.. Enumerable.Range(0, t.Count).Select(i => (PersonalInfo)t[i])];

    private static string[] Names(string[] list, int count) => [.. list.Take(count)];

    private static byte[] Learn4(params (int Level, int Move)[] l) =>
        [.. l.SelectMany(x => BitConverter.GetBytes((ushort)((x.Level << 9) | x.Move))), 0xFF, 0xFF];

    private static byte[] Learn5(params (int Level, int Move)[] l) =>
        [.. l.SelectMany(x => BitConverter.GetBytes((ushort)x.Move).Concat(BitConverter.GetBytes((ushort)x.Level))), 0xFF, 0xFF, 0xFF, 0xFF];

    internal static byte[] Platinum(Func<int, string?>? rename = null, int fillerBanks = 3, string[]? extra = null)
    {
        var personal = RawGen4(Split(Entries(PersonalTable.StockPt)));
        var learn = Enumerable.Range(0, 494).Select(i => i == 1 ? Learn4((1, 33), (3, 45), (7, 73)) : Learn4((1, 33))).ToArray();
        var species = Names(En.specieslist, 494);
        if (rename is not null)
            species = [.. species.Select((n, i) => rename(i) ?? n)];
        if (extra is not null)
            species = [.. species, .. extra];
        var banks = new List<byte[]>();
        for (var i = 0; i < fillerBanks; i++)
            banks.Add(Gen4Text(["filler", $"line {i}", "more"]));
        banks.Add(Gen4Text(species));
        banks.Add(Gen4Text(Names(En.movelist, 468)));
        banks.Add(Gen4Text(Names(En.itemlist, 468)));
        banks.Add(Gen4Text(Names(En.abilitylist, 124)));
        var moves = Enumerable.Range(0, 468).Select(i => i == 1 ? new byte[] { 0, 0, 0, 40, 0, 100, 35, 0, 0, 0, 0, 0, 0, 0, 0, 0 } : new byte[16]).ToArray();
        return BuildNds("CPUE", new Dictionary<string, byte[]>
        {
            ["poketool/personal/pl_personal.narc"] = BuildNarc(personal),
            ["poketool/personal/wotbl.narc"] = BuildNarc(learn),
            ["msgdata/pl_msg.narc"] = BuildNarc(banks),
            ["poketool/waza/pl_waza_tbl.narc"] = BuildNarc(moves),
            ["arm9.bin"] = new byte[64],
        });
    }

    [Fact]
    public void NitroFsWalksNestedDirectories()
    {
        var nds = BuildNds("IREO", new Dictionary<string, byte[]> { ["a/0/1/6"] = [1, 2, 3], ["a/0/1/8"] = [4], ["a/1/2/3"] = [5, 6], ["root.bin"] = [7] });
        var fs = new NitroFs(nds);
        Assert.Equal("IREO", fs.GameCode);
        Assert.Equal(4, fs.FileCount);
        Assert.Equal([1, 2, 3], fs.File("a/0/1/6")!.Value.ToArray());
        Assert.Equal([5, 6], fs.File("a/1/2/3")!.Value.ToArray());
        Assert.Equal([7], fs.File("root.bin")!.Value.ToArray());
        Assert.Null(fs.File("a/0/1/7"));
    }

    [Fact]
    public void NarcRoundTrips()
    {
        var files = Narc.Read(BuildNarc([[1], [2, 3], [], [4, 5, 6]]))!;
        Assert.Equal(4, files.Count);
        Assert.Equal([2, 3], files[1].ToArray());
        Assert.Empty(files[2].ToArray());
        Assert.Null(Narc.Read(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 }));
    }

    [Fact]
    public void Gen4TextDecodes()
    {
        var lines = GameText.ReadGen4(Gen4Text(["-----", "BULBASAUR", "Nidoran♀", "Mr. Mime"]))!;
        Assert.Equal(["-----", "BULBASAUR", "Nidoran♀", "Mr. Mime"], lines);
    }

    [Fact]
    public void Gen5AndGen6TextDecode()
    {
        string[] lines = ["-----", "Bulbasaur", "Flabébé", "Type: Null"];
        Assert.Equal(lines, GameText.ReadGen5(SectionText(lines, gen5: true)));
        Assert.Equal(lines, GameText.ReadGen6(SectionText(lines, gen5: false)));
    }

    [Fact]
    public void PlatinumReadsPersonalEqualToPkhexAndNamesByContent()
    {
        var r = NdsHackData.Read(Platinum(), "pt-sha");
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(NdsHackData.Reader, d.Reader);
        Assert.Equal(494, d.Species.Count);
        var stock = PersonalTable.StockPt;
        for (var i = 1; i < 494; i++)
        {
            var s = d.Species[i];
            var p = stock[i];
            Assert.Equal([p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], s.BaseStats);
            Assert.Equal([(int)p.Type1, p.Type2], s.Types.Select(t => GameTypes.ToPkhex(4, t) ?? -1));
            Assert.Equal(p.CatchRate, s.CatchRate);
            Assert.Equal(p.EXPGrowth, s.Growth);
            Assert.Equal(p.Gender, s.GenderRatio);
            Assert.Equal(p.BaseEXP, s.ExpYield);
            Assert.Equal(i, s.National);
        }
        Assert.Equal("Bulbasaur", d.Species[1].Name);
        Assert.Equal(1, d.Moves[1].National);
        Assert.Equal(40, d.Moves[1].Power);
        Assert.Equal(35, d.Moves[1].Pp);
        Assert.Equal(1, d.Items[1].National);
        Assert.Equal(1, d.Abilities[1].National);
        Assert.Equal([new HackLearn(1, 33), new HackLearn(3, 45), new HackLearn(7, 73)], d.LevelUp[1]);
        Assert.Contains(d.Notes, n => n.Contains("species names from msgdata/pl_msg.narc bank 3", StringComparison.Ordinal));
        Assert.Contains(d.Notes, n => n.Contains("form entries", StringComparison.Ordinal));
        Assert.Empty(d.UnmappedSpecies);
    }

    [Fact]
    public void BankIsFoundWhereverTheHackPutsIt()
    {
        var d = NdsHackData.Read(Platinum(fillerBanks: 7), "x").Data!;
        Assert.Contains(d.Notes, n => n.Contains("bank 7", StringComparison.Ordinal));
        Assert.Equal("Ivysaur", d.Species[2].Name);
    }

    [Fact]
    public void RenamedFakemonIsUnmapped()
    {
        var d = NdsHackData.Read(Platinum(i => i == 25 ? "Sparkmouse" : null), "x").Data!;
        Assert.Equal("Sparkmouse", d.Species[25].Name);
        Assert.Equal([25], d.UnmappedSpecies);
    }

    [Fact]
    public void Black2ReadsGen5Layout()
    {
        var personal = Split(Entries(PersonalTable.StockB2W2));
        var learn = Enumerable.Range(0, 650).Select(i => i == 1 ? Learn5((1, 33), (3, 45)) : Learn5((1, 33))).ToArray();
        var text = new List<byte[]> { SectionText(["a", "b", "c"], true), SectionText(Names(En.specieslist, 650), true), SectionText(Names(En.movelist, 560), true),
            SectionText(Names(En.itemlist, 639), true), SectionText(Names(En.abilitylist, 165), true) };
        var moves = Enumerable.Range(0, 560).Select(i => i == 1 ? new byte[] { 0, 0, 0, 40, 100, 35 } : new byte[36]).ToArray();
        var nds = BuildNds("IREO", new Dictionary<string, byte[]> { ["a/0/1/6"] = BuildNarc(personal), ["a/0/1/8"] = BuildNarc(learn), ["a/0/0/2"] = BuildNarc(text), ["a/0/2/1"] = BuildNarc(moves) });
        var r = NdsHackData.Read(nds, "b2");
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(650, d.Species.Count);
        var p = PersonalTable.StockB2W2[649];
        Assert.Equal([p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], d.Species[649].BaseStats);
        Assert.Equal("Genesect", d.Species[649].Name);
        Assert.Equal([new HackLearn(1, 33), new HackLearn(3, 45)], d.LevelUp[1]);
        Assert.Equal(40, d.Moves[1].Power);
        Assert.Equal(100, d.Moves[1].Accuracy);
        Assert.Contains(d.Notes, n => n.Contains("species names from a/0/0/2 bank 1", StringComparison.Ordinal));
    }

    [Fact]
    public void LoopingFileNameTableDoesNotHang()
    {
        var nds = BuildNds("CPUE", new Dictionary<string, byte[]> { ["a/c1/x"] = [1], ["a/c2/x"] = [2], ["a/c3/x"] = [3], ["a/c4/x"] = [4] });
        var repointed = 0;
        for (var i = 0; i + 4 < nds.Length; i++)
        {
            if (nds[i] == 0x82 && nds[i + 1] == (byte)'c' && nds[i + 2] is >= (byte)'1' and <= (byte)'4')
            {
                BinaryPrimitives.WriteUInt16LittleEndian(nds.AsSpan(i + 3), 0xF001);
                repointed++;
            }
        }
        Assert.Equal(4, repointed);
        var task = System.Threading.Tasks.Task.Run(() => new NitroFs(nds));
        Assert.True(task.Wait(TimeSpan.FromSeconds(10)), "the FNT walk did not finish");
    }

    [Fact]
    public void Gen5GenderGlyphsMapBothNidoran()
    {
        var lines = GameText.ReadGen5(SectionText(["-----", "Nidoran\u246E", "Nidoran\u246D"], gen5: true))!;
        var m = IdMapper.English();
        Assert.Equal(29, m.Species(lines[1]));
        Assert.Equal(32, m.Species(lines[2]));
    }

    [Fact]
    public void EggLinesAreNotSpecies()
    {
        var d = NdsHackData.Read(Platinum(extra: ["Egg", "Bad Egg"]), "x").Data!;
        Assert.Equal(494, d.Species.Count);
        Assert.Empty(d.UnmappedSpecies);
        Assert.Contains(d.Notes, n => n.Contains("2 trailing species-name lines", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusalsSayWhy()
    {
        Assert.Contains("shorter than an NDS header", NdsHackData.Read(new byte[16], "x").Refusal);
        Assert.Contains("is not a Diamond", NdsHackData.Read(BuildNds("ABCD", new Dictionary<string, byte[]> { ["x"] = [1] }), "x").Refusal);
        var personal = RawGen4(Split(Entries(PersonalTable.StockPt)));
        for (var i = 1; i < personal.Length; i++)
        {
            personal[i][0] = 0;
            personal[i][1] = 200;
        }
        var bad = BuildNds("CPUE", new Dictionary<string, byte[]> { ["poketool/personal/pl_personal.narc"] = BuildNarc(personal) });
        Assert.Contains("not Bulbasaur's 45/49/49", NdsHackData.Read(bad, "x").Refusal);
        var noText = BuildNds("CPUE", new Dictionary<string, byte[]> { ["poketool/personal/pl_personal.narc"] = BuildNarc(RawGen4(Split(Entries(PersonalTable.StockPt)))) });
        var r = NdsHackData.Read(noText, "x");
        Assert.True(r.Ok);
        Assert.Contains(r.Data!.Notes, n => n.Contains("no names are read", StringComparison.Ordinal));
        Assert.Equal(HackTableStatus.Read, r.Data.StatusOf(HackTableStatus.Species));
        Assert.Equal("the species have stats but no names", r.Data.Table(HackTableStatus.Species)!.Reason);
        Assert.True(r.Data.SpeciesOverlayable);
    }

    [Fact]
    public void ATrailingMemberOfAnotherSizeIsDroppedNotPadded()
    {
        var personal = Split(Entries(PersonalTable.StockB2W2)).Take(709).ToList();
        Assert.Equal(709, personal.Count);
        personal.Add(new byte[1370]);
        var rows = NdsHackData.PersonalRows([.. personal.Select(p => (ReadOnlyMemory<byte>)p)], out var dropped);
        Assert.Equal(1, dropped);
        Assert.Equal(709, rows.Count);
        Assert.All(rows, r => Assert.Equal(PersonalInfo5B2W2.SIZE, r.Length));
        var text = new List<byte[]> { SectionText(["a", "b", "c"], true), SectionText(Names(En.specieslist, 650), true), SectionText(Names(En.movelist, 560), true),
            SectionText(Names(En.itemlist, 639), true), SectionText(Names(En.abilitylist, 165), true) };
        var nds = BuildNds("IREO", new Dictionary<string, byte[]> { ["a/0/1/6"] = BuildNarc(personal), ["a/0/0/2"] = BuildNarc(text) });
        var r = NdsHackData.Read(nds, "redux-shape");
        Assert.True(r.Ok, r.Refusal);
        Assert.Contains(r.Data!.Notes, n => n.StartsWith("1 trailing member of a/0/1/6", StringComparison.Ordinal));
        Assert.Contains(r.Data.Notes, n => n.StartsWith("59 personal entries past the 650 named species", StringComparison.Ordinal));
        Assert.DoesNotContain(r.Data.Notes, n => n.Contains("zero-padded", StringComparison.Ordinal));
    }

    [Fact]
    public void MembersOfMixedSizesAreLeftAlone()
    {
        ReadOnlyMemory<byte>[] members = [new byte[4], new byte[4], new byte[4], new byte[9]];
        Assert.Equal(4, NdsHackData.PersonalRows(members, out var dropped).Count);
        Assert.Equal(0, dropped);
        Assert.Empty(NdsHackData.PersonalRows([], out _));
    }

    [Fact]
    public void ABlankFirstEntryIsReadWhenTheOtherEntriesArePlausible()
    {
        var personal = RawGen4(Split(Entries(PersonalTable.StockPt)));
        personal[1] = new byte[44];
        var nds = BuildNds("CPUE", new Dictionary<string, byte[]> { ["poketool/personal/pl_personal.narc"] = BuildNarc(personal) });
        var r = NdsHackData.Read(nds, "x");
        Assert.True(r.Ok, r.Refusal);
        Assert.Contains(r.Data!.Notes, n => n.Contains("is not Bulbasaur's 45/49/49, but", StringComparison.Ordinal));
        var status = r.Data.Table(HackTableStatus.Species)!;
        Assert.Equal((HackTableStatus.Read, (bool?)null), (status.Status, status.Aligned));
        Assert.Contains("by personal index", status.Reason, StringComparison.Ordinal);
        Assert.True(r.Data.SpeciesOverlayable);
    }

    [Fact]
    public void MoveCategoryIsNormalisedForBothGenerations()
    {
        Assert.Equal(HackMove.Physical, NdsHackData.Category(4, 0));
        Assert.Equal(HackMove.Special, NdsHackData.Category(4, 1));
        Assert.Equal(HackMove.StatusCategory, NdsHackData.Category(4, 2));
        Assert.Equal(HackMove.StatusCategory, NdsHackData.Category(5, 0));
        Assert.Equal(HackMove.Physical, NdsHackData.Category(5, 1));
        Assert.Equal(HackMove.Special, NdsHackData.Category(5, 2));
        Assert.Null(NdsHackData.Category(5, 3));
        Assert.NotEqual(NdsHackData.Category(4, 2), NdsHackData.Category(5, 2));
        var d = NdsHackData.Read(Platinum(), "pt").Data!;
        Assert.Equal(HackMove.Physical, d.Moves[1].Category);
        Assert.False(d.Moves[1].CategoryByType);
    }

    internal static List<HackSpecies> StockSpeciesRows(IPersonalTable table, int count) =>
        [.. Enumerable.Range(0, count).Select(i => new HackSpecies(i, En.specieslist[i], i == 0 ? null : i, 0, [table[i].Type1, table[i].Type2],
            [table[i].HP, table[i].ATK, table[i].DEF, table[i].SPE, table[i].SPA, table[i].SPD], [], table[i].EXPGrowth, table[i].Gender, table[i].CatchRate, table[i].BaseEXP) { GenderRead = true })];

    [Fact]
    public void Gen5FormRowsAreLinkedThroughTheFormIndexInTheHackBytes()
    {
        var stock = PersonalTable.StockB2W2;
        var named = stock.MaxSpeciesID + 1;
        var species = StockSpeciesRows(stock, named);
        var forms = NdsHackData.FormRows(stock, NdsHackData.Black2White2, named, species, out var unlinked);
        Assert.Equal(stock.Count - named, forms.Count);
        var attack = stock[(int)Species.Deoxys].FormIndex((ushort)Species.Deoxys, 1);
        var row = forms.Single(f => f.Id == attack);
        Assert.Equal(((int?)Species.Deoxys, 1, "Deoxys"), (row.BaseSpecies, row.Form, row.Name));
        Assert.True(unlinked < forms.Count);

        var bytes = Split(Entries(stock));
        foreach (var e in bytes)
        {
            e[0x1C] = 0;
            e[0x1D] = 0;
        }
        var zeroed = new PersonalTable5B2W2(bytes.SelectMany(b => b).ToArray());
        var none = NdsHackData.FormRows(zeroed, NdsHackData.Black2White2, named, species, out var allUnlinked);
        Assert.Equal(none.Count, allUnlinked);
        Assert.All(none, f => Assert.Null(f.BaseSpecies));
    }

    [Fact]
    public void TheDsReadCarriesPlatformEngineStatusAndKey()
    {
        var d = NdsHackData.Read(Platinum(), "pt").Data!;
        Assert.Equal((HackTables.Nds, "CPUE", HackTables.NdsGen4Engine), (d.Platform, d.GameCode, d.Engine));
        Assert.StartsWith("species:read,moves:read,items:read,abilities:read,levelUp:", HackTables.StatusLine(d));
        Assert.True(d.Table(HackTableStatus.Species)!.Aligned);
        Assert.Equal(HackTableKey.Of(d), d.TableKey);
        Assert.True(d.Species[1].GenderRead);
    }
}
