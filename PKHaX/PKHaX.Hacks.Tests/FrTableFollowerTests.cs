using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class FrTableFollowerTests
{
    private const int SpeciesNames = 0x10000;
    private const int AbilityNames = 0x12000;
    private const int Repointed = 0x18000;
    private const int Code = 0x1C000;

    private static void Ptr(byte[] rom, int at, int offset) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), (uint)(0x08000000 + offset));

    private static void Name(byte[] rom, int at, string text, int length)
    {
        rom.AsSpan(at, length).Fill(0xFF);
        StringConverter3.SetString(rom.AsSpan(at, length), text, length - 1, (int)LanguageID.English);
    }

    private static byte[] Repoint(int references, params string[] species)
    {
        var rom = HackDataTests.Image();
        for (var i = 0; i < species.Length; i++)
            Name(rom, Repointed + i * 11, species[i], 11);
        for (var i = 0; i < references; i++)
            Ptr(rom, Code + i * 8, Repointed);
        return rom;
    }

    [Fact]
    public void ARepointedTableWithMoreReferencesWinsOverTheStaleHeaderPointer()
    {
        var r = GbaHackData.Read(Repoint(3, "??????????", "BULBASAUR", "IVYSAUR", "VENUSAUR", "ZORBLEFLAX"));
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(GbaHackData.ReaderFrFollow, d.Reader);
        Assert.Contains(d.Notes, n => n == $"species names at 0x{Repointed:X} stride 11 (3 references), not the GF header's 0x{SpeciesNames:X}");
        Assert.Equal(["BULBASAUR", "IVYSAUR", "VENUSAUR", "ZORBLEFLAX"], d.Species.Skip(1).Select(s => s.Name));
        Assert.Equal([45, 49, 49, 45, 65, 65], d.Species[1].BaseStats);
        Assert.Null(d.Species[4].National);
    }

    [Fact]
    public void AnUnreferencedCopyLosesToTheHeaderTable()
    {
        var r = GbaHackData.Read(Repoint(0, "??????????", "BULBASAUR", "IVYSAUR", "VENUSAUR"));
        Assert.Equal(GbaHackData.ReaderGf, r.Data!.Reader);
        Assert.Equal("MR. MIME", r.Data.Species[3].Name);
        Assert.Equal(1, FrTableFollower.References(new GbaImage(Repoint(1, "??????????", "BULBASAUR", "IVYSAUR")), [Repointed])[Repointed]);
    }

    [Fact]
    public void CfruAbilityNamesAtStride17AreFollowed()
    {
        var rom = HackDataTests.Image();
        string[] abilities = ["-------", "STENCH", "DRIZZLE", "SPEED BOOST", "MAGIC GUARDIAN"];
        for (var i = 0; i < abilities.Length; i++)
            Name(rom, Repointed + i * 17, abilities[i], 17);
        Ptr(rom, Code, Repointed);
        Ptr(rom, Code + 8, Repointed);
        Name(rom, AbilityNames + 13, "STINK", 13);
        var d = GbaHackData.Read(rom).Data!;
        Assert.Equal(GbaHackData.ReaderFrFollow, d.Reader);
        Assert.Contains(d.Notes, n => n.StartsWith($"ability names at 0x{Repointed:X} stride 17 (2 references)"));
        Assert.Equal(["STENCH", "DRIZZLE", "SPEED BOOST", "MAGIC GUARDIAN"], d.Abilities.Skip(1).Select(a => a.Name));
        Assert.Null(d.Abilities[4].National);
    }

    [Fact]
    public void ARenamedAnchorFallsBackToTheHeader()
    {
        var rom = HackDataTests.Image();
        Name(rom, SpeciesNames + 11, "BULBASORE", 11);
        var d = GbaHackData.Read(rom).Data!;
        Assert.Equal(GbaHackData.ReaderGf, d.Reader);
        Assert.Equal("BULBASORE", d.Species[1].Name);
    }

    [Fact]
    public void AnEliteReduxLikeLayoutReadsByPointerTablesAndWideStrides()
    {
        var rom = HackDataTests.Image();
        var strings = 0x19000;
        var at = strings;
        int Str(string text)
        {
            var here = at;
            Name(rom, here, text, text.Length + 1);
            at += text.Length + 2;
            return here;
        }
        var speciesPtrs = 0x1A000;
        string[] species = ["-", "Bulbasaur", "Ivysaur", "Venusaur", "Zorbleflax"];
        for (var i = 0; i < species.Length; i++)
            Ptr(rom, speciesPtrs + i * 4, Str(species[i]));
        var abilityPtrs = 0x1A100;
        string[] abilities = ["-", "Stench", "Drizzle", "Speed Boost", "Galvanize"];
        for (var i = 0; i < abilities.Length; i++)
            Ptr(rom, abilityPtrs + i * 4, Str(abilities[i]));
        var info = 0x1B000;
        byte[] bulba = [45, 49, 49, 45, 65, 65, 12, 3, 45];
        bulba.CopyTo(rom, info + 192);
        byte[] ivy = [60, 62, 63, 60, 80, 80];
        ivy.CopyTo(rom, info + 384);
        var items = 0x1E000;
        string[] itemNames = ["????????", "Master Ball", "Ultra Ball", "Great Ball", "Zorblefluxcapaci"];
        for (var i = 0; i < itemNames.Length; i++)
            Name(rom, items + i * 48, itemNames[i], 17);
        foreach (var (table, n) in new[] { (speciesPtrs, 2), (abilityPtrs, 2), (info, 2), (items, 2) })
        {
            for (var i = 0; i < n; i++)
                Ptr(rom, Code + 0x100 * (table & 0xF) + (table >> 8 & 0xFF) * 16 + i * 4, table);
        }
        rom.AsSpan(0x14000 + 12, 5).Clear();

        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(GbaHackData.ReaderFrFollow, d.Reader);
        Assert.Contains(d.Notes, n => n.StartsWith($"species names at 0x{speciesPtrs:X} as a name-pointer table (2 references)"));
        Assert.Contains(d.Notes, n => n.StartsWith("species-info stride 192 is not a vanilla one"));
        Assert.Contains(d.Notes, n => n.StartsWith("item stride 48 is not the vanilla layout"));
        Assert.Contains(d.Notes, n => n.StartsWith("no move table has Pound"));
        Assert.Equal(["Bulbasaur", "Ivysaur", "Venusaur", "Zorbleflax"], d.Species.Skip(1).Select(x => x.Name));
        Assert.Equal([45, 49, 49, 45, 65, 65], d.Species[1].BaseStats);
        Assert.Equal(45, d.Species[1].CatchRate);
        Assert.Equal(["Stench", "Drizzle", "Speed Boost", "Galvanize"], d.Abilities.Skip(1).Select(x => x.Name));
        Assert.Equal([1, 2, 3], d.Items.Skip(1).Take(3).Select(x => x.National ?? 0));
        Assert.Equal("Zorblefluxcapaci", d.Items[4].Name);
        Assert.Equal(1, d.Moves[1].National);
        Assert.Equal(0, d.Moves[1].Power);
    }
}
