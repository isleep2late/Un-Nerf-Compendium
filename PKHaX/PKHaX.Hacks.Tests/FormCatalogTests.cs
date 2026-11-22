using System;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Analysis.Forms;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class FormCatalogTests
{
    [Theory]
    [InlineData(GameVersion.R, 0, "Normal", new[] { 50, 150, 50, 150, 150, 50 })]
    [InlineData(GameVersion.E, 3, "Speed", new[] { 50, 95, 90, 180, 95, 90 })]
    [InlineData(GameVersion.FR, 1, "Attack", new[] { 50, 180, 20, 150, 180, 20 })]
    [InlineData(GameVersion.LG, 2, "Defense", new[] { 50, 70, 160, 90, 70, 160 })]
    public void Gen3DeoxysIsOneRowPerGame(GameVersion version, int form, string name, int[] stats)
    {
        var g = VanillaForms.Build(version).For(VanillaForms.Deoxys)!;
        var row = Assert.Single(g.Rows);
        Assert.Equal(form, row.Form);
        Assert.Equal(name, row.Name);
        Assert.Equal(stats, row.BaseStats);
        Assert.Equal(FormStorageKind.GameDecides, row.Storage.Kind);
        Assert.Equal(FormCatalog.SourceGen3Deoxys, row.Source);
    }

    [Fact]
    public void UnownIsStoredInThePidInGen3AndTheDvsInGen2()
    {
        var e = VanillaForms.Build(GameVersion.E).For(VanillaForms.Unown)!;
        Assert.Equal(28, e.Rows.Count);
        Assert.All(e.Rows, r => Assert.Equal(FormStorageKind.Pid, r.Storage.Kind));
        var c = VanillaForms.Build(GameVersion.C).For(VanillaForms.Unown)!;
        Assert.Equal(26, c.Rows.Count);
        Assert.All(c.Rows, r => Assert.Equal(FormStorageKind.Dv, r.Storage.Kind));
    }

    [Fact]
    public void BattleOnlyFormsAreMarkedAndNotStorable()
    {
        var castform = VanillaForms.Build(GameVersion.E).For((ushort)Species.Castform)!;
        Assert.Equal(FormAvailability.Outside, castform.Rows[0].Availability);
        Assert.All(castform.Rows.Skip(1), r => Assert.Equal(FormAvailability.BattleOnly, r.Availability));
        Assert.Single(castform.Storable);
        var charizard = VanillaForms.Build(GameVersion.X).For((ushort)Species.Charizard)!;
        Assert.Equal(3, charizard.Rows.Count);
        Assert.Equal([FormAvailability.Outside, FormAvailability.BattleOnly, FormAvailability.BattleOnly], charizard.Rows.Select(r => r.Availability));
    }

    [Fact]
    public void PlatinumKeepsItsOutsideFormesInTheFormField()
    {
        var pt = VanillaForms.Build(GameVersion.Pt);
        Assert.Equal(2, pt.For((ushort)Species.Giratina)!.Storable.Count());
        Assert.Equal(2, pt.For((ushort)Species.Shaymin)!.Storable.Count());
        Assert.Equal(6, pt.For((ushort)Species.Rotom)!.Storable.Count());
        Assert.All(pt.Groups.SelectMany(g => g.Rows), r => Assert.Equal(FormStorageKind.Field, r.Storage.Kind));
        var dp = VanillaForms.Build(GameVersion.D);
        Assert.Null(dp.For((ushort)Species.Rotom));
    }

    [Fact]
    public void TotemFormsAreUnobtainable()
    {
        var us = VanillaForms.Build(GameVersion.US);
        Assert.Contains(us.Groups.SelectMany(g => g.Rows), r => r.Availability == FormAvailability.Unobtainable);
        Assert.DoesNotContain(VanillaForms.Build(GameVersion.X).Groups.SelectMany(g => g.Rows), r => r.Availability == FormAvailability.Unobtainable);
    }

    [Fact]
    public void FormStatsComeFromTheFormEntry()
    {
        var rotom = VanillaForms.Build(GameVersion.Pt).For((ushort)Species.Rotom)!;
        Assert.Equal(PersonalTable.Pt[(ushort)Species.Rotom, 1].ATK, rotom.Rows[1].BaseStats[1]);
        Assert.NotEqual(rotom.Rows[0].BaseStats, rotom.Rows[1].BaseStats);
    }

    [Fact]
    public void Gen1HasNoFormsAndAPairVersionIsRefused()
    {
        Assert.Empty(VanillaForms.Build(GameVersion.RD).Groups);
        Assert.Throws<ArgumentException>(() => VanillaForms.Build(GameVersion.RS));
    }
    private static HackSpecies S(int id, string name, int? national, int[] stats) => new(id, name, national, 0, [12, 3], stats, [65, 0], 0, 0, 45, 64);

    internal static HackData Synthetic() => new()
    {
        RomSha1 = new string('0', 40),
        Reader = "gba-rhh",
        Species =
        [
            S(0, "", null, [0, 0, 0, 0, 0, 0]),
            S(1, "Venusaur", 3, [80, 82, 83, 80, 100, 100]),
            S(2, "Venusaur", 3, [80, 100, 123, 80, 122, 120]),
            S(3, "Vivillon", 666, [80, 52, 50, 89, 90, 50]),
            S(4, "Vivillon", 666, [80, 52, 50, 89, 90, 50]),
            S(5, "Pikachu", 25, [35, 55, 40, 90, 50, 50]),
            S(6, "Charizard", 6, [78, 84, 78, 100, 109, 85]),
            S(7, "Gmax Charizard", null, [78, 84, 78, 100, 109, 85]),
            S(8, "Zorbleflax", null, [1, 2, 3, 4, 5, 6]),
            S(9, "Charizard", 6, [99, 99, 99, 99, 99, 99]),
            S(10, "Charizard", 6, [78, 84, 78, 100, 109, 85]),
            S(11, "Mega Blastoise", null, [79, 103, 120, 78, 135, 115]),
            S(12, "Blastoise", 9, [79, 83, 100, 78, 85, 105]),
            S(13, "Castform", 351, [70, 70, 70, 70, 70, 70]),
            S(14, "Crabminabl", null, [97, 132, 77, 43, 62, 67]),
            S(15, "Crabminabl", null, [97, 132, 77, 43, 62, 67]),
            S(16, "Raichu", 26, [60, 90, 55, 110, 90, 80]),
            S(17, "Raichu", 26, [60, 85, 50, 110, 95, 85]),
        ],
    };

    [Fact]
    public void HackFormsGroupByNationalAndByBattleNames()
    {
        var c = HackForms.Build(Synthetic(), "test");
        Assert.Equal([1, 3, 6, 12, 13, 14, 16], c.Groups.Select(g => g.BaseSpecies));
        Assert.Null(c.For(5));
        Assert.All(c.Groups.Where(g => g.National != 351).SelectMany(g => g.Rows), r => Assert.Equal(FormStorageKind.Species, r.Storage.Kind));
        Assert.Equal([1, 2], c.For(1)!.Rows.Select(r => r.Storage.Value));
        Assert.Equal(c.For(6), c.GroupOf(7));
        Assert.Equal(740, c.For(14)!.National);
    }

    [Fact]
    public void HackFormsOfferOnlyRowsThatMatchAStorablePkhexForm()
    {
        var c = HackForms.Build(Synthetic(), "test");
        var venusaur = c.For(1)!.Rows;
        Assert.Equal((FormAvailability.Outside, "pkhex-match:0"), (venusaur[0].Availability, venusaur[0].Source));
        Assert.Equal((FormAvailability.BattleOnly, "pkhex-match:1"), (venusaur[1].Availability, venusaur[1].Source));
        Assert.Equal(2, c.For(3)!.Storable.Count());
        var charizard = c.For(6)!.Rows.ToDictionary(r => r.Storage.Value);
        Assert.Equal(FormAvailability.BattleOnly, charizard[7].Availability);
        Assert.Equal(FormCatalog.SourceName, charizard[7].Source);
        Assert.Equal((FormAvailability.Unknown, FormCatalog.SourceUnmatched), (charizard[9].Availability, charizard[9].Source));
        Assert.Equal((FormAvailability.Unknown, FormCatalog.SourceAmbiguous), (charizard[10].Availability, charizard[10].Source));
        Assert.Single(c.For(6)!.Storable);
        Assert.Equal(FormAvailability.BattleOnly, c.For(12)!.Rows.Single(r => r.Storage.Value == 11).Availability);
        Assert.Equal(FormAvailability.Outside, c.For(16)!.Rows[1].Availability);
        Assert.Equal(FormAvailability.Unknown, c.For(14)!.Rows[1].Availability);
    }

    [Fact]
    public void AHackCastformOrUnownTakesTheGen3Rows()
    {
        var castform = HackForms.Build(Synthetic(), "test").For(13)!;
        Assert.Equal(4, castform.Rows.Count);
        Assert.Single(castform.Storable);
        Assert.All(castform.Rows.Skip(1), r => Assert.Equal(FormAvailability.BattleOnly, r.Availability));
    }

    [Theory]
    [InlineData("Mega Venusaur", true)]
    [InlineData("Charizard-Mega-X", true)]
    [InlineData("Primal Kyogre", true)]
    [InlineData("Darmanitan Zen", true)]
    [InlineData("Aegislash Blade", true)]
    [InlineData("Megarion", false)]
    [InlineData("Omega Ruby", false)]
    public void BattleOnlyNamesMatchWholeWordsOnly(string name, bool battle) => Assert.Equal(battle, HackForms.NamedBattleOnly(name));

    [Theory]
    [InlineData(GameVersion.D)]
    [InlineData(GameVersion.Pt)]
    [InlineData(GameVersion.HG)]
    public void Gen4KeepsUnownAndCherrim(GameVersion version)
    {
        var c = VanillaForms.Build(version);
        Assert.Equal(28, c.For(VanillaForms.Unown)!.Rows.Count);
        var cherrim = c.For((ushort)Species.Cherrim)!;
        Assert.Equal(FormAvailability.BattleOnly, cherrim.Rows[1].Availability);
    }

    [Fact]
    public void FormsTheGameLacksAreNotListed()
    {
        Assert.DoesNotContain(VanillaForms.Build(GameVersion.PLA).Groups.SelectMany(g => g.Rows), r => r.BaseStats.All(x => x == 0));
        Assert.DoesNotContain(VanillaForms.Build(GameVersion.SW).Groups.SelectMany(g => g.Rows), r => r.BaseStats.All(x => x == 0));
        var gp = VanillaForms.Build(GameVersion.GP).For((ushort)Species.Pikachu);
        Assert.True(gp is null || gp.Rows.All(r => r.Form is 0 or 8), string.Join(",", gp?.Rows.Select(r => r.Form) ?? []));
    }

    [Fact]
    public void Gen4ArceusHasNoUnknownTypeRow()
    {
        var arceus = VanillaForms.Build(GameVersion.Pt).For((ushort)Species.Arceus)!;
        Assert.Equal(17, arceus.Rows.Count);
        Assert.DoesNotContain(arceus.Rows, r => r.Form == 9);
        Assert.Equal(17, VanillaForms.Build(GameVersion.B).For((ushort)Species.Arceus)!.Rows.Count);
    }
}
