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

public sealed class CtrHackDataTests : IDisposable
{
    private static readonly GameStrings En = GameInfo.GetStrings("en");
    private readonly string root = Path.Combine(Path.GetTempPath(), "pkhax-ctr-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    internal static byte[] BuildGarc(IReadOnlyList<byte[]> files, IReadOnlySet<int>? empty = null)
    {
        var data = new MemoryStream();
        var fatb = new MemoryStream();
        var count = files.Count;
        var offsets = new List<int>();
        for (var i = 0; i < count; i++)
        {
            offsets.Add((int)fatb.Length);
            if (empty?.Contains(i) == true)
            {
                fatb.Write(BitConverter.GetBytes(0u));
                continue;
            }
            var start = (int)data.Length;
            data.Write(files[i]);
            while (data.Length % 4 != 0)
                data.WriteByte(0);
            fatb.Write(BitConverter.GetBytes(1u));
            fatb.Write(BitConverter.GetBytes((uint)start));
            fatb.Write(BitConverter.GetBytes((uint)data.Length));
            fatb.Write(BitConverter.GetBytes((uint)files[i].Length));
        }
        var fatoSize = 12 + count * 4;
        var fatbSize = 12 + (int)fatb.Length;
        var dataOffset = 0x1C + fatoSize + fatbSize + 12;
        var o = new MemoryStream();
        o.Write(Encoding.ASCII.GetBytes("CRAG"));
        o.Write(BitConverter.GetBytes(0x1Cu));
        o.Write(BitConverter.GetBytes((ushort)0xFEFF));
        o.Write(BitConverter.GetBytes((ushort)0x0400));
        o.Write(BitConverter.GetBytes(4u));
        o.Write(BitConverter.GetBytes((uint)dataOffset));
        o.Write(BitConverter.GetBytes((uint)(dataOffset + data.Length)));
        o.Write(BitConverter.GetBytes(0u));
        o.Write(Encoding.ASCII.GetBytes("OTAF"));
        o.Write(BitConverter.GetBytes((uint)fatoSize));
        o.Write(BitConverter.GetBytes((ushort)count));
        o.Write(BitConverter.GetBytes((ushort)0xFFFF));
        foreach (var off in offsets)
            o.Write(BitConverter.GetBytes((uint)off));
        o.Write(Encoding.ASCII.GetBytes("BTAF"));
        o.Write(BitConverter.GetBytes((uint)fatbSize));
        o.Write(BitConverter.GetBytes((uint)count));
        o.Write(fatb.ToArray());
        o.Write(Encoding.ASCII.GetBytes("BMIF"));
        o.Write(BitConverter.GetBytes(12u));
        o.Write(BitConverter.GetBytes((uint)data.Length));
        o.Write(data.ToArray());
        return o.ToArray();
    }

    private static byte[] Table(IPersonalTable t) => [.. Enumerable.Range(0, t.Count).SelectMany(i => ((PersonalInfo)t[i]).Write())];

    private static byte[] Learn(params (int Level, int Move)[] l) =>
        [.. l.SelectMany(x => BitConverter.GetBytes((ushort)x.Move).Concat(BitConverter.GetBytes((ushort)x.Level))), 0xFF, 0xFF, 0xFF, 0xFF];

    private void Put(string dir, string path, byte[] bytes)
    {
        var full = Path.Combine(root, dir, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }

    private string Folder(string dir) => Path.Combine(root, dir);

    private void Title(string dir, CtrLayout layout, IPersonalTable stock, int speciesNames, bool joined = false)
    {
        var table = Table(stock);
        var size = layout.PersonalSize;
        var perSpecies = Enumerable.Range(0, stock.Count).Select(i => table.AsSpan(i * size, size).ToArray()).ToList();
        Put(dir, layout.Personal, BuildGarc(joined ? perSpecies : [.. perSpecies, table]));
        Put(dir, layout.Learnsets, BuildGarc([.. Enumerable.Range(0, speciesNames).Select(i => i == 1 ? Learn((1, 33), (3, 45)) : Learn((1, 33)))]));
        var text = new List<byte[]> { NdsHackDataTests.SectionText(["x", "y", "z"], false), NdsHackDataTests.SectionText([.. En.specieslist.Take(speciesNames)], false),
            NdsHackDataTests.SectionText([.. En.movelist.Take(600)], false), NdsHackDataTests.SectionText([.. En.itemlist.Take(700)], false),
            NdsHackDataTests.SectionText([.. En.abilitylist.Take(190)], false) };
        Put(dir, layout.Text[0], BuildGarc(text));
    }

    [Fact]
    public void GarcReadsFilesAndEmptySlots()
    {
        var files = Garc.Read(BuildGarc([[1, 2, 3], [9], [4, 5]], new HashSet<int> { 1 }))!;
        Assert.Equal(3, files.Count);
        Assert.Equal([1, 2, 3], files[0].ToArray());
        Assert.True(files[1].IsEmpty);
        Assert.Equal([4, 5], files[2].ToArray());
        Assert.Null(Garc.Read(new byte[64]));
    }

    [Fact]
    public void Lz11Decompresses()
    {
        byte[] packed = [0x11, 8, 0, 0, 0b0000_1000, (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0x30, 0x03];
        Assert.Equal("ABCDABCD", Encoding.ASCII.GetString(Lz11.Decompress(packed)!));
        var garc = Garc.Read(BuildGarc([packed]))!;
        Assert.Equal(packed, garc[0].ToArray());
        Assert.Null(Lz11.Decompress([0x11, 8, 0, 0, 0b0000_1000, (byte)'A', 0x30, 0x03]));
    }

    [Fact]
    public void OrasReadsPersonalEqualToPkhex()
    {
        Title("oras", CtrHackData.ORAS, PersonalTable.StockAO, 722);
        var r = CtrHackData.Read(new CtrRomFs(Folder("oras")), "oras-key");
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(722, d.Species.Count);
        for (var i = 1; i < 722; i++)
        {
            var p = PersonalTable.StockAO[i];
            Assert.Equal([p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], d.Species[i].BaseStats);
            Assert.Equal(i, d.Species[i].National);
        }
        Assert.Equal("Volcanion", d.Species[721].Name);
        Assert.Equal([new HackLearn(1, 33), new HackLearn(3, 45)], d.LevelUp[1]);
        Assert.Equal(1, d.Moves[1].National);
        Assert.Equal(1, d.Items[1].National);
        Assert.Contains(d.Notes, n => n.StartsWith("3DS oras", StringComparison.Ordinal));
        Assert.Contains(d.Notes, n => n.Contains("species names from a/0/7/3 file 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Lz11RefusesInputItDidNotRead()
    {
        byte[] packed = [0x11, 8, 0, 0, 0b0000_1000, (byte)'A', (byte)'B', (byte)'C', (byte)'D', 0x30, 0x03];
        Assert.Equal("ABCDABCD", Encoding.ASCII.GetString(Lz11.Decompress([.. packed, 0])!));
        Assert.Null(Lz11.Decompress([.. packed, 1, 2, 3, 4, 5, 6, 7, 8]));
        byte[] raw = [0x11, 4, 0, 0, 0x00, 1, 2, 3, 4, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9];
        Assert.Null(Lz11.Decompress(raw));
    }

    [Theory]
    [InlineData(0x04, 0x7FFFFFF8u)]
    [InlineData(0x04, 0xFFFFFFF8u)]
    [InlineData(0x20, 0x7FFFFFF0u)]
    [InlineData(0x20, 0xFFFFFFF0u)]
    [InlineData(0x28, 0x7FFFFFF0u)]
    [InlineData(0x28, 0xFFFFFFF0u)]
    public void GarcOffsetsNearTheIntLimitAreRefusedNotThrown(int field, uint value)
    {
        var garc = BuildGarc([[1, 2, 3], [4, 5]]);
        BitConverter.GetBytes(value).CopyTo(garc, field);
        Assert.Null(Garc.Read(garc));
    }

    [Fact]
    public void ANameBankLongerThanTheSpeciesListStopsAtTheLastSpecies()
    {
        Title("oras812", CtrHackData.ORAS, PersonalTable.StockAO, 812);
        var d = CtrHackData.Read(new CtrRomFs(Folder("oras812")), "oras812").Data!;
        Assert.Equal(PersonalTable.StockAO.MaxSpeciesID + 1, d.Species.Count);
        Title("xy799", CtrHackData.XY, PersonalTable.StockXY, 799, joined: true);
        Assert.Equal(PersonalTable.StockXY.MaxSpeciesID + 1, CtrHackData.Read(new CtrRomFs(Folder("xy799")), "xy799").Data!.Species.Count);
    }

    [Fact]
    public void AShortPersonalTableIsNotPaddedWithEntryZero()
    {
        var dir = "short";
        var layout = CtrHackData.ORAS;
        var table = Table(PersonalTable.StockAO);
        var size = layout.PersonalSize;
        var perSpecies = Enumerable.Range(0, 400).Select(i => table.AsSpan(i * size, size).ToArray()).ToList();
        Put(dir, layout.Personal, BuildGarc(perSpecies));
        Put(dir, layout.Learnsets, BuildGarc([.. Enumerable.Range(0, 722).Select(_ => Learn((1, 33)))]));
        Put(dir, layout.Text[0], BuildGarc([NdsHackDataTests.SectionText(["x", "y", "z"], false), NdsHackDataTests.SectionText([.. En.specieslist.Take(722)], false)]));
        var d = CtrHackData.Read(new CtrRomFs(Folder(dir)), dir).Data!;
        Assert.Equal(400, d.Species.Count);
    }

    [Fact]
    public void XyJoinsPerSpeciesFilesWhenTheLastIsNotTheTable()
    {
        Title("xy", CtrHackData.XY, PersonalTable.StockXY, 722, joined: true);
        var d = CtrHackData.Read(new CtrRomFs(Folder("xy")), "xy").Data!;
        var p = PersonalTable.StockXY[150];
        Assert.Equal([p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], d.Species[150].BaseStats);
        Assert.Contains(d.Notes, n => n.Contains("joined from the per-species files", StringComparison.Ordinal));
    }

    [Fact]
    public void SunMoonAndUltraAreToldApartByTableLength()
    {
        Title("sm", CtrHackData.SMUSUM, PersonalTable.StockSM, 803);
        Title("usum", CtrHackData.SMUSUM, PersonalTable.StockUSUM, 808);
        Assert.Contains(CtrHackData.Read(new CtrRomFs(Folder("sm")), "sm").Data!.Notes, n => n.StartsWith("3DS sm", StringComparison.Ordinal));
        var u = CtrHackData.Read(new CtrRomFs(Folder("usum")), "usum").Data!;
        Assert.Contains(u.Notes, n => n.StartsWith("3DS usum", StringComparison.Ordinal));
        Assert.Equal("Zeraora", u.Species[807].Name);
        Assert.Equal(808, u.Species.Count);
    }

    [Fact]
    public void OverlayWinsOverTheBase()
    {
        Title("base", CtrHackData.ORAS, PersonalTable.StockAO, 722);
        var table = Table(PersonalTable.StockAO);
        var size = CtrHackData.ORAS.PersonalSize;
        table[25 * size] = 99;
        Put("mod/romfs", CtrHackData.ORAS.Personal, BuildGarc([table]));
        var fs = new CtrRomFs(Folder("base"), CtrRomFs.RomFsFolderOf(Folder("mod")));
        var d = CtrHackData.Read(fs, "hack").Data!;
        Assert.Equal(99, d.Species[25].BaseStats[0]);
        Assert.Contains(d.Notes, n => n.Contains("personal from the overlay", StringComparison.Ordinal));
        Assert.Equal("Pikachu", d.Species[25].Name);
    }

    [Fact]
    public void LetsGoReadsTheOverlayPersonalTotal()
    {
        var table = Table(PersonalTable.StockGG);
        Put("lgpe-base", CtrHackData.LgpePersonal, table);
        var hacked = (byte[])table.Clone();
        var e = new PersonalInfo7GG(hacked.AsMemory(150 * PersonalInfo7GG.SIZE, PersonalInfo7GG.SIZE));
        e.BaseEXP = 1;
        Put("lgpe-mod/romfs", CtrHackData.LgpePersonal, hacked);
        var d = CtrHackData.ReadLgpe(new CtrRomFs(Folder("lgpe-base"), Folder("lgpe-mod/romfs")), "lgpe").Data!;
        Assert.Equal(CtrHackData.ReaderLgpe, d.Reader);
        Assert.Equal("Mewtwo", d.Species[150].Name);
        Assert.Equal(1, d.Species[150].ExpYield);
        Assert.Contains(d.Notes, n => n == "1 species have an EXP yield different from vanilla");
        Assert.Contains(d.Notes, n => n.Contains("personal from the overlay", StringComparison.Ordinal));
    }

    internal static byte[] BuildRomFsBin(IReadOnlyDictionary<string, byte[]> files)
    {
        var dirs = new List<(string Path, int Parent, List<int> Children, List<int> Files)> { ("", 0, [], []) };
        var fileList = new List<(string Name, byte[] Data, int Dir)>();
        foreach (var (path, bytes) in files.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var parts = path.Split('/');
            var dir = 0;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var sub = string.Join("/", parts.Take(i + 1));
                var found = dirs.FindIndex(d => d.Path == sub);
                if (found < 0)
                {
                    dirs.Add((sub, dir, [], []));
                    found = dirs.Count - 1;
                    dirs[dir].Children.Add(found);
                }
                dir = found;
            }
            fileList.Add((parts[^1], bytes, dir));
            dirs[dir].Files.Add(fileList.Count - 1);
        }
        static int Size(string name) => (name.Length * 2 + 3) & ~3;
        var dirOffsets = new int[dirs.Count];
        var o = 0;
        for (var i = 0; i < dirs.Count; i++)
        {
            dirOffsets[i] = o;
            o += 0x18 + Size(i == 0 ? "" : dirs[i].Path.Split('/')[^1]);
        }
        var dirMeta = new byte[o];
        var fileOffsets = new int[fileList.Count];
        o = 0;
        for (var i = 0; i < fileList.Count; i++)
        {
            fileOffsets[i] = o;
            o += 0x20 + Size(fileList[i].Name);
        }
        var fileMeta = new byte[o];
        var data = new MemoryStream();
        for (var i = 0; i < dirs.Count; i++)
        {
            var at = dirOffsets[i];
            var name = i == 0 ? "" : dirs[i].Path.Split('/')[^1];
            var siblings = dirs[dirs[i].Parent].Children;
            var idx = siblings.IndexOf(i);
            BinaryPrimitives.WriteUInt32LittleEndian(dirMeta.AsSpan(at), (uint)dirOffsets[dirs[i].Parent]);
            BinaryPrimitives.WriteUInt32LittleEndian(dirMeta.AsSpan(at + 4), i == 0 || idx + 1 >= siblings.Count ? 0xFFFFFFFF : (uint)dirOffsets[siblings[idx + 1]]);
            BinaryPrimitives.WriteUInt32LittleEndian(dirMeta.AsSpan(at + 8), dirs[i].Children.Count == 0 ? 0xFFFFFFFF : (uint)dirOffsets[dirs[i].Children[0]]);
            BinaryPrimitives.WriteUInt32LittleEndian(dirMeta.AsSpan(at + 12), dirs[i].Files.Count == 0 ? 0xFFFFFFFF : (uint)fileOffsets[dirs[i].Files[0]]);
            BinaryPrimitives.WriteUInt32LittleEndian(dirMeta.AsSpan(at + 16), 0xFFFFFFFF);
            BinaryPrimitives.WriteUInt32LittleEndian(dirMeta.AsSpan(at + 20), (uint)(name.Length * 2));
            Encoding.Unicode.GetBytes(name).CopyTo(dirMeta, at + 0x18);
        }
        for (var i = 0; i < fileList.Count; i++)
        {
            var at = fileOffsets[i];
            var siblings = dirs[fileList[i].Dir].Files;
            var idx = siblings.IndexOf(i);
            var start = data.Length;
            data.Write(fileList[i].Data);
            while (data.Length % 16 != 0)
                data.WriteByte(0);
            BinaryPrimitives.WriteUInt32LittleEndian(fileMeta.AsSpan(at), (uint)dirOffsets[fileList[i].Dir]);
            BinaryPrimitives.WriteUInt32LittleEndian(fileMeta.AsSpan(at + 4), idx + 1 >= siblings.Count ? 0xFFFFFFFF : (uint)fileOffsets[siblings[idx + 1]]);
            BinaryPrimitives.WriteUInt64LittleEndian(fileMeta.AsSpan(at + 8), (ulong)start);
            BinaryPrimitives.WriteUInt64LittleEndian(fileMeta.AsSpan(at + 16), (ulong)fileList[i].Data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(fileMeta.AsSpan(at + 24), 0xFFFFFFFF);
            BinaryPrimitives.WriteUInt32LittleEndian(fileMeta.AsSpan(at + 28), (uint)(fileList[i].Name.Length * 2));
            Encoding.Unicode.GetBytes(fileList[i].Name).CopyTo(fileMeta, at + 0x20);
        }
        var l3 = new MemoryStream();
        var header = new byte[IvfcRomFs.Level3HeaderLength];
        l3.Write(header);
        var dirHash = (int)l3.Length; l3.Write(new byte[4]);
        var dirMetaAt = (int)l3.Length; l3.Write(dirMeta);
        var fileHash = (int)l3.Length; l3.Write(new byte[4]);
        var fileMetaAt = (int)l3.Length; l3.Write(fileMeta);
        while (l3.Length % 16 != 0)
            l3.WriteByte(0);
        var dataAt = (int)l3.Length; l3.Write(data.ToArray());
        var l3b = l3.ToArray();
        uint[] fields = [IvfcRomFs.Level3HeaderLength, (uint)dirHash, 4, (uint)dirMetaAt, (uint)dirMeta.Length, (uint)fileHash, 4, (uint)fileMetaAt, (uint)fileMeta.Length, (uint)dataAt];
        for (var i = 0; i < fields.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(l3b.AsSpan(i * 4), fields[i]);
        var image = new byte[0x1000 + l3b.Length];
        Encoding.ASCII.GetBytes("IVFC").CopyTo(image, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(4), 0x10000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(8), 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(0x4C), 12);
        l3b.CopyTo(image, 0x1000);
        return image;
    }

    [Fact]
    public void RomFsBinExtractsToAFolderTheReaderUses()
    {
        Title("src", CtrHackData.ORAS, PersonalTable.StockAO, 722);
        var src = Folder("src");
        var files = Directory.GetFiles(src, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(src, f).Replace(Path.DirectorySeparatorChar, '/'), File.ReadAllBytes);
        files["sound/x.bin"] = [1, 2, 3];
        var bin = Path.Combine(root, "romfs.bin");
        File.WriteAllBytes(bin, BuildRomFsBin(files));
        var count = IvfcRomFs.Extract(bin, Folder("extracted"));
        Assert.Equal(files.Count, count);
        Assert.Equal([1, 2, 3], File.ReadAllBytes(Path.Combine(Folder("extracted"), "sound", "x.bin")));
        var d = CtrHackData.Read(new CtrRomFs(Folder("extracted")), "bin").Data!;
        Assert.Equal("Volcanion", d.Species[721].Name);
        File.WriteAllBytes(Path.Combine(root, "plain.bin"), new byte[0x100]);
        Assert.Throws<ArgumentException>(() => IvfcRomFs.Extract(Path.Combine(root, "plain.bin"), Folder("x")));
    }

    [Fact]
    public void SunMoonWithAnXyPathStillReadsAsSunMoon()
    {
        Title("smx", CtrHackData.SMUSUM, PersonalTable.StockSM, 803);
        Put("smx", CtrHackData.XY.Personal, BuildGarc([new byte[0x40 * 3], [1, 2, 3]]));
        Put("smx", CtrHackData.ORAS.Personal, BuildGarc([new byte[0x50]]));
        var r = CtrHackData.Read(new CtrRomFs(Folder("smx")), "smx");
        Assert.True(r.Ok, r.Refusal);
        Assert.Contains(r.Data!.Notes, n => n.StartsWith("3DS sm", StringComparison.Ordinal));
    }

    [Fact]
    public void XyNamesComeFromTheEnglishFileNotTheFirstThatExists()
    {
        Title("xyen", CtrHackData.XY, PersonalTable.StockXY, 722);
        Put("xyen", "a/0/7/2", BuildGarc([NdsHackDataTests.SectionText(["kana", "fushigidane", "fushigisou"], false)]));
        var d = CtrHackData.Read(new CtrRomFs(Folder("xyen")), "xy").Data!;
        Assert.Equal("Bulbasaur", d.Species[1].Name);
        Assert.Contains(d.Notes, n => n.Contains("species names from a/0/7/4", StringComparison.Ordinal));
    }

    [Fact]
    public void HostileGarcAndIvfcAreRefusedNotThrownOrHung()
    {
        var garc = BuildGarc([[1, 2, 3]]);
        BinaryPrimitives.WriteUInt32LittleEndian(garc.AsSpan(0x10), 0xFFFFFF00);
        Assert.Null(Garc.Read(garc));
        var bin = BuildRomFsBin(new Dictionary<string, byte[]> { ["a/x"] = [1] });
        var l3 = 0x1000;
        var dirMetaAt = l3 + (int)BinaryPrimitives.ReadUInt32LittleEndian(bin.AsSpan(l3 + 0x0C));
        BinaryPrimitives.WriteUInt32LittleEndian(bin.AsSpan(dirMetaAt + 0x08), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bin.AsSpan(dirMetaAt + 0x04), 0);
        var path = Path.Combine(root, "loop.bin");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(path, bin);
        var task = System.Threading.Tasks.Task.Run(() => IvfcRomFs.Extract(path, Folder("loop")));
        Assert.True(task.Wait(TimeSpan.FromSeconds(10)), "the IVFC walk did not finish");
    }

    [Fact]
    public void RefusalsSayWhy()
    {
        Directory.CreateDirectory(Folder("empty"));
        Assert.Contains("not an XY, ORAS, SM or USUM", CtrHackData.Read(new CtrRomFs(Folder("empty")), "x").Refusal);
        var lg = CtrHackData.ReadLgpe(new CtrRomFs(Folder("empty")), "x").Refusal; Assert.True(lg!.Contains("not a Let's Go romfs", StringComparison.Ordinal), lg);
        Put("bad", CtrHackData.ORAS.Personal, BuildGarc([new byte[0x50 * 4]]));
        Assert.Contains("not Bulbasaur's 45/49/49", CtrHackData.Read(new CtrRomFs(Folder("bad")), "x").Refusal);
        Assert.Throws<ArgumentException>(() => new CtrRomFs(Folder("missing")));
    }

    [Fact]
    public void The3dsReadCarriesPlatformEngineStatusAndKey()
    {
        Title("oras-status", CtrHackData.ORAS, PersonalTable.StockAO, 722);
        var d = CtrHackData.Read(new CtrRomFs(Folder("oras-status")), "oras-status-key").Data!;
        Assert.Equal((HackTables.Ctr, HackTables.CtrGen6Engine), (d.Platform, d.Engine));
        Assert.Null(d.GameCode);
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Species));
        Assert.Equal(HackTableStatus.Partial, d.StatusOf(HackTableStatus.Moves));
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Items));
        Assert.Equal(HackTableKey.Of(d), d.TableKey);
        Assert.Equal("oras-status-key", d.RomSha1);
    }
}
