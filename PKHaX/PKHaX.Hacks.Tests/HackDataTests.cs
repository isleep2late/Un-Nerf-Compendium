using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class HackDataTests
{
    private const int SpeciesNames = 0x10000;
    private const int MoveNames = 0x11000;
    private const int AbilityNames = 0x12000;
    private const int SpeciesInfo = 0x13000;
    private const int Moves = 0x14000;
    private const int Items = 0x15000;

    private static void Name(byte[] rom, int at, string text, int length)
    {
        rom.AsSpan(at, length).Fill(0xFF);
        StringConverter3.SetString(rom.AsSpan(at, length), text, length - 1, (int)LanguageID.English);
    }

    private static void Ptr(byte[] rom, int at, int offset) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), (uint)(0x08000000 + offset));

    private static void U32(byte[] rom, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), value);

    internal static byte[] Image(int shift = 0, int speciesStride = 28, int moveStride = 12, string code = "BPEE", bool rhh = false)
    {
        var rom = new byte[0x20000];
        System.Text.Encoding.ASCII.GetBytes(code).CopyTo(rom, 0xAC);
        rom[0xB2] = 0x96;
        var h = 0x100;
        U32(rom, h, 2);
        U32(rom, h + 4, 2);
        Ptr(rom, h + 0x44, SpeciesNames);
        Ptr(rom, h + 0x48, MoveNames);
        var t = h + shift;
        U32(rom, t + 0x88, 0xF2C);
        U32(rom, t + 0x8C, 0x3D88);
        U32(rom, t + 0x90, 0x234);
        U32(rom, t + 0x94, 0x238);
        Ptr(rom, t + 0xBC, SpeciesInfo);
        Ptr(rom, t + 0xC0, AbilityNames);
        Ptr(rom, t + 0xC8, Items);
        Ptr(rom, t + 0xCC, Moves);
        rom[t + 0xE4] = 30;

        string[] species = ["??????????", "BULBASAUR", "IVYSAUR", "MR. MIME", "NIDORAN♀", "TREECKO", "ZZYZXQUUX"];
        for (var i = 0; i < species.Length; i++)
            Name(rom, SpeciesNames + i * 11, species[i], 11);
        string[] moves = ["-", "POUND", "KARATE CHOP", "DOUBLESLAP", "NEWMOVEX"];
        for (var i = 0; i < moves.Length; i++)
            Name(rom, MoveNames + i * 13, moves[i], 13);
        string[] abilities = ["-------", "STENCH", "DRIZZLE", "LIGHTNINGROD"];
        for (var i = 0; i < abilities.Length; i++)
            Name(rom, AbilityNames + i * 13, abilities[i], 13);

        byte[] bulbasaur = [45, 49, 49, 45, 65, 65];
        bulbasaur.CopyTo(rom, SpeciesInfo + speciesStride);
        rom[SpeciesInfo + speciesStride + 0x06] = 12;
        rom[SpeciesInfo + speciesStride + 0x07] = 3;
        rom[SpeciesInfo + speciesStride + 0x08] = 45;
        rom[SpeciesInfo + speciesStride + 0x16] = 65;

        rom[Moves + moveStride + 1] = 40;
        rom[Moves + moveStride + 2] = 0;
        rom[Moves + moveStride + 3] = 100;
        rom[Moves + moveStride + 4] = 35;
        byte[] ivysaur = [60, 62, 63, 60, 80, 80];
        ivysaur.CopyTo(rom, SpeciesInfo + 2 * speciesStride);
        byte[] karateChop = [43, 50, 1, 100, 25];
        karateChop.CopyTo(rom, Moves + 2 * moveStride);

        string[] items = ["????????", "MASTER BALL", "ULTRA BALL", "GREAT BALL", "ZQXWEIRD"];
        for (var i = 0; i < items.Length; i++)
        {
            Name(rom, Items + i * 44, items[i], 14);
            BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(Items + i * 44 + 0x0E), (ushort)i);
            rom[Items + i * 44 + 0x1A] = 3;
        }
        if (rhh)
        {
            "RHHEXP"u8.CopyTo(rom.AsSpan(0x400));
            rom[0x406] = 1;
            rom[0x407] = 9;
        }
        return rom;
    }

    [Fact]
    public void VanillaLayoutReadsAndMaps()
    {
        var r = GbaHackData.Read(Image());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(GbaHackData.ReaderGf, d.Reader);
        Assert.Equal(7, d.Species.Count);
        Assert.Equal("BULBASAUR", d.Species[1].Name);
        Assert.Equal(1, d.Species[1].National);
        Assert.Equal([45, 49, 49, 45, 65, 65], d.Species[1].BaseStats);
        Assert.Equal([12, 3], d.Species[1].Types);
        Assert.Equal(45, d.Species[1].CatchRate);
        Assert.Equal((int)Species.MrMime, d.Species[3].National);
        Assert.Equal((int)Species.NidoranF, d.Species[4].National);
        Assert.Equal((int)Species.Treecko, d.Species[5].National);
        Assert.Null(d.Species[6].National);
        Assert.Equal([6], d.UnmappedSpecies);
    }

    [Fact]
    public void MovesItemsAndAbilitiesMapByNameOrGen3Index()
    {
        var d = GbaHackData.Read(Image()).Data!;
        Assert.Equal(1, d.Moves[1].National);
        Assert.Equal(40, d.Moves[1].Power);
        Assert.Equal(35, d.Moves[1].Pp);
        Assert.Equal(3, d.Moves[3].National);
        Assert.Null(d.Moves[4].National);
        Assert.Equal([4], d.UnmappedMoves);
        Assert.Equal(1, d.Items[1].National);
        Assert.Equal(2, d.Items[2].National);
        Assert.Equal(3, d.Items[3].National);
        Assert.Null(d.Items[4].National);
        Assert.Equal(3, d.Items[1].Pocket);
        Assert.Equal(5, d.Items.Count);
        Assert.Equal(1, d.Abilities[1].National);
        Assert.Equal((int)Ability.LightningRod, d.Abilities[3].National);
    }

    [Fact]
    public void ANameTableStopsWhereTheNextHeaderTableStarts()
    {
        var rom = Image();
        for (var i = 7; i < 400; i++)
            Name(rom, SpeciesNames + i * 11, "PADDING", 11);
        var d = GbaHackData.Read(rom).Data!;
        Assert.Equal((MoveNames - SpeciesNames) / 11, d.Species.Count);
        Assert.Equal(372, Gen3Tables.Bound(SpeciesNames, 11, [SpeciesNames, MoveNames, null, 0x20]));
        Assert.Equal(Gen3Tables.MaxEntries, Gen3Tables.Bound(Items, 44, [SpeciesNames, null]));
    }

    [Fact]
    public void ShiftedHeaderIsFoundAndSaid()
    {
        var r = GbaHackData.Read(Image(shift: 4));
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal(GbaHackData.ReaderGfShifted, r.Data!.Reader);
        Assert.Contains(r.Data.Notes, n => n.Contains("shifted"));
    }

    [Fact]
    public void StridesComeFromTheDataNotAConstant()
    {
        var r = GbaHackData.Read(Image(speciesStride: 26, moveStride: 9));
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal([45, 49, 49, 45, 65, 65], r.Data!.Species[1].BaseStats);
        Assert.Equal(40, r.Data.Moves[1].Power);
        var wide = GbaHackData.Read(Image(speciesStride: 30)).Data!;
        Assert.Equal([45, 49, 49, 45, 65, 65], wide.Species[1].BaseStats);
        Assert.Empty(wide.Species[1].Abilities);
        Assert.Contains(wide.Notes, n => n.StartsWith("species-info stride 30 is not a vanilla one"));
        var rom = Image();
        rom[SpeciesInfo + 28] = 44;
        var bad = GbaHackData.Read(rom);
        Assert.False(bad.Ok);
        Assert.Contains("Bulbasaur", bad.Refusal);
    }

    [Fact]
    public void ImagesThisReaderDoesNotOwnAreRefusedWithAReason()
    {
        var rs = GbaHackData.Read(Image(code: "AXVE"));
        Assert.True(rs.Ok, rs.Refusal);
        Assert.Equal(GbaHackData.ReaderFollow, rs.Data!.Reader);
        Assert.Contains(rs.Data.Notes, n => n.StartsWith("Ruby and Sapphire have no GF header"));
        Assert.Contains("RHHEXP", GbaHackData.Read(Image(rhh: true)).Refusal);
        var blank = new byte[0x20000];
        Assert.Contains("no GF ROM header", GbaHackData.Read(blank).Refusal);
        Assert.Contains("512 bytes to 32 MiB", GbaHackData.Read(new byte[16]).Refusal);
    }

    [Fact]
    public void RhhHeaderFieldsReadAtTheExpansionOffsets()
    {
        var rom = Image(rhh: true);
        BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x40A), 848);
        BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x40C), 1573);
        BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x40E), 311);
        BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(0x414), 901);
        rom[0x416] = 20;
        var h = RhhRomHeader.Read(new GbaImage(rom))!;
        Assert.Equal((0x400, "1.9.0", 848, 1573, 311, 901, 20), (h.Offset, h.Version, h.Moves, h.Species, h.Abilities, h.Items, h.ItemNameLength));
    }

    [Fact]
    public void CacheRoundTripsAndRefusesAStaleOrForeignFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "pkhax-hackdata-" + Guid.NewGuid().ToString("N"));
        try
        {
            var image = Image();
            var built = HackDataCache.ReadOrBuild(root, image);
            Assert.True(built.Ok, built.Refusal);
            var path = HackDataCache.PathFor(root, built.Data!.RomSha1);
            Assert.True(File.Exists(path));
            var json = File.ReadAllText(path);
            Assert.Contains("\"schema\": \"hackdata/1\"", json);
            var loaded = HackDataCache.Load(root, built.Data.RomSha1)!;
            Assert.Equal(built.Data.Counts, loaded.Counts);
            Assert.Equal(built.Data.Species.Select(s => (s.Id, s.Name, s.National)), loaded.Species.Select(s => (s.Id, s.Name, s.National)));
            Assert.Equal(built.Data.Items.Select(i => i.National), loaded.Items.Select(i => i.National));
            Assert.Null(HackDataCache.FromJson(json, new string('0', 40)));
            Assert.Null(HackDataCache.FromJson(json.Replace($"\"readerVersion\": {HackData.CurrentReaderVersion}", $"\"readerVersion\": {HackData.MinReaderVersion - 1}"), built.Data.RomSha1));
            Assert.Null(HackDataCache.FromJson(json.Replace($"\"readerVersion\": {HackData.CurrentReaderVersion}", $"\"readerVersion\": {HackData.CurrentReaderVersion + 1}"), built.Data.RomSha1));
            Assert.Equal(HackData.MinReaderVersion, HackDataCache.FromJson(json.Replace($"\"readerVersion\": {HackData.CurrentReaderVersion}", $"\"readerVersion\": {HackData.MinReaderVersion}"), built.Data.RomSha1)!.ReaderVersion);
            Assert.Null(HackDataCache.FromJson("{ not json", built.Data.RomSha1));
            Assert.Null(HackDataCache.FromJson(json.Replace("\"species\": [", "\"species\": [ { \"id\": \"x\" },"), built.Data.RomSha1));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public void NamesNormaliseAcrossCaseSpacesAndAccents()
    {
        Assert.Equal(IdMapper.Normalise("Mr. Mime"), IdMapper.Normalise("MR. MIME"));
        Assert.Equal(IdMapper.Normalise("Flabébé"), IdMapper.Normalise("FLABEBE"));
        Assert.NotEqual(IdMapper.Normalise("Nidoran♀"), IdMapper.Normalise("Nidoran♂"));
    }

    [Fact]
    public void NamesWithLoneSurrogatesNormaliseWithoutThrowing()
    {
        Assert.Equal("PIKACHU", IdMapper.Normalise("Pika\uD800chu"));
        Assert.Equal("PIKACHU", IdMapper.Normalise("Pika\uDFFFchu"));
        Assert.Equal("", IdMapper.Normalise("\uDC00\uD83D"));
        Assert.Equal("ABC", IdMapper.Normalise("A\uD83D\uDE00B\uFFFFC\uFFFE"));
        Assert.Equal(IdMapper.Normalise("Flabébé"), IdMapper.Normalise("Flabe\u0301be\u0301\uDBFF"));
    }

    [Fact]
    public void AMissingItemTableLeavesTheOtherTablesRead()
    {
        var rom = Image();
        rom.AsSpan(Items, 0x1000).Clear();
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.StartsWith("species:read,moves:read,items:missing,abilities:read,levelUp:", HackTables.StatusLine(d));
        Assert.Equal(HackTableStatus.Missing, d.StatusOf(HackTableStatus.Items));
        Assert.Contains("MASTER BALL, ULTRA BALL and GREAT BALL", d.Table(HackTableStatus.Items)!.Reason);
        Assert.Empty(d.Items);
        Assert.Equal(7, d.Species.Count);
        Assert.Equal(40, d.Moves[1].Power);
        Assert.Contains(d.Notes, n => n.StartsWith("items are not read", StringComparison.Ordinal));
    }

    [Fact]
    public void TheGbaReadCarriesPlatformCodeEngineAndKey()
    {
        var d = GbaHackData.Read(Image()).Data!;
        Assert.Equal((HackTables.Gba, "BPEE", Gen3Engine.Vanilla), (d.Platform, d.GameCode, d.Engine));
        Assert.Equal(HackData.CurrentReaderVersion, d.ReaderVersion);
        Assert.Equal(HackTableKey.Of(d), d.TableKey);
        Assert.True(d.Species[1].GenderRead);
        Assert.False(GbaHackData.Read(Image(speciesStride: 30)).Data!.Species[1].GenderRead);
        Assert.Equal(HackTables.Gba, HackTables.Sniff(Image()));
        Assert.Null(HackTables.Sniff(new byte[16]));
    }

    [Fact]
    public void NewKeysRoundTripThroughTheCacheJson()
    {
        var built = GbaHackData.Read(Image()).Data!;
        var data = built with
        {
            Forms = [new HackSpecies(7, "Deoxys", 386, 1, [14, 14], [50, 180, 20, 150, 180, 20], [46, 0], 5, 255, 3, 215) { BaseSpecies = 5, FormName = "Attack", Dex = 9, GenderRead = false }],
            Moves = [.. built.Moves.Select(m => m.Id == 1 ? m with { Category = HackMove.Physical, CategoryByType = true } : m)],
            TypeNames = ["NORMAL", "FIGHT"],
        };
        var back = HackDataCache.FromJson(HackDataCache.ToJson(data), data.RomSha1)!;
        Assert.Equal((data.Platform, data.GameCode, data.Engine, data.TableKey), (back.Platform, back.GameCode, back.Engine, back.TableKey));
        Assert.Equal(data.Tables, back.Tables);
        Assert.Equal(data.TypeNames, back.TypeNames);
        var form = Assert.Single(back.Forms);
        Assert.Equal(5, form.BaseSpecies);
        Assert.Equal("Attack", form.FormName);
        Assert.Equal(9, form.Dex);
        Assert.False(form.GenderRead);
        Assert.Equal(HackMove.Physical, back.Moves[1].Category);
        Assert.True(back.Moves[1].CategoryByType);
        Assert.Null(back.Moves[2].Category);
        Assert.Equal(data.Species.Select(s => s.GenderRead), back.Species.Select(s => s.GenderRead));
        Assert.Equal(HackTableKey.Of(data), HackTableKey.Of(back));
        Assert.Equal(data.Counts, back.Counts);
    }

    [Fact]
    public void ARebuildThatRefusesKeepsTheStoredVersion4File()
    {
        var root = Path.Combine(Path.GetTempPath(), "pkhax-hackdata-" + Guid.NewGuid().ToString("N"));
        try
        {
            var image = Image();
            var built = HackDataCache.ReadOrBuild(root, image).Data!;
            var path = HackDataCache.PathFor(root, built.RomSha1);
            var four = File.ReadAllText(path).Replace($"\"readerVersion\": {HackData.CurrentReaderVersion}", $"\"readerVersion\": {HackData.MinReaderVersion}");
            File.WriteAllText(path, four);
            var rebuilt = HackDataCache.ReadOrBuild(root, image).Data!;
            Assert.Equal(HackData.CurrentReaderVersion, rebuilt.ReaderVersion);
            Assert.Equal(HackData.CurrentReaderVersion, HackDataCache.Load(root, built.RomSha1)!.ReaderVersion);

            var broken = Image();
            broken[SpeciesInfo + 28] = 44;
            var key = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(broken));
            var stored = HackDataCache.PathFor(root, key);
            Directory.CreateDirectory(Path.GetDirectoryName(stored)!);
            var old = four.Replace(built.RomSha1, key);
            File.WriteAllText(stored, old);
            Assert.False(GbaHackData.Read(broken).Ok);
            var kept = HackDataCache.ReadOrBuild(root, broken);
            Assert.True(kept.Ok, kept.Refusal);
            Assert.Equal(HackData.MinReaderVersion, kept.Data!.ReaderVersion);
            Assert.Equal(old, File.ReadAllText(stored));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
