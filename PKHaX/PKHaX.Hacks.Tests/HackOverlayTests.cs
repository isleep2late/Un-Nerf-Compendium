using System;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Overlay;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class HackOverlayTests
{
    private static HackSpecies S(int id, string name, int[] stats, int[] types, int[] abilities) => new(id, name, id, 0, types, stats, abilities, 3, 31, 45, 64);

    private static HackData Platinum() => new()
    {
        RomSha1 = new string('0', 40),
        Reader = "nds-gen4",
        Species =
        [
            S(0, "", [0, 0, 0, 0, 0, 0], [0, 0], [0, 0]),
            S(1, "Bulbasaur", [45, 99, 49, 45, 65, 65], [12, 3], [65, 34]),
            S(35, "Clefairy", [70, 45, 48, 35, 60, 65], [9, 9], [56, 98]),
            S(150, "Mewthree", [106, 110, 90, 130, 154, 90], [14, 14], [46, 0]),
            S(9999, "Zorbleflax", [1, 2, 3, 4, 5, 6], [0, 0], [0, 0]),
        ],
        Moves = [new HackMove(1, "Pound", 1, 40, 0, 100, 35), new HackMove(2, "Mega Bonk", 2, 50, 1, 100, 25)],
    };

    [Fact]
    public void TheOverlayTakesTheHacksStatsTypesAndAbilities()
    {
        var o = HackOverlay.From(Platinum(), GameVersion.Pt);
        var (_, table) = PersonalOverlay.Create(GameVersion.Pt, o.Personal);
        Assert.Equal(99, table[1].ATK);
        Assert.Equal((byte)MoveType.Grass, table[1].Type1);
        Assert.Equal((byte)MoveType.Poison, table[1].Type2);
        Assert.Equal(34, ((IPersonalAbility12)table[1]).Ability2);
        Assert.Equal(HackOverlay.PkhexFairy, table[35].Type1);
        Assert.Equal(PersonalTable.Pt[4].ATK, table[4].ATK);
        Assert.Contains(o.Notes, n => n.Contains("which this hack uses for Fairy"));
        Assert.Contains(o.Notes, n => n.Contains("past the stock table"));
    }

    [Fact]
    public void TheStringsPatchHoldsOnlyRenamedEntries()
    {
        var o = HackOverlay.From(Platinum(), GameVersion.Pt);
        Assert.Equal("Mewthree", o.Strings.Species[150]);
        Assert.False(o.Strings.Species.ContainsKey(1));
        Assert.Equal("Mega Bonk", o.Strings.Moves[2]);
        Assert.False(o.Strings.Moves.ContainsKey(1));
    }

    [Fact]
    public void UseAppliesBothOverlaysAndRestoresThem()
    {
        var o = HackOverlay.From(Platinum(), GameVersion.Pt);
        var before = PersonalTable.Pt[1].ATK;
        using (o.Use())
        {
            Assert.Equal(99, PersonalTable.Pt[1].ATK);
            Assert.Equal("Mewthree", GameInfo.Strings.specieslist[150]);
        }
        Assert.Equal(before, PersonalTable.Pt[1].ATK);
        Assert.Equal("Mewtwo", GameInfo.Strings.specieslist[150]);
    }

    [Fact]
    public void APairVersionIsRefused() => Assert.Throws<ArgumentException>(() => HackOverlay.From(Platinum(), GameVersion.DPPt));

    [Fact]
    public void LetsGoTakesTheOverlayToo()
    {
        var data = new HackData { RomSha1 = new string('0', 40), Reader = CtrHackData.ReaderLgpe, Species = [S(25, "Pikachu", [35, 80, 40, 90, 50, 50], [13, 13], [9, 9, 31])] };
        var (_, table) = PersonalOverlay.Create(GameVersion.GP, HackOverlay.From(data, GameVersion.GP).Personal);
        Assert.Equal(80, table[25].ATK);
        Assert.Equal(31, ((IPersonalAbility12H)table[25]).AbilityH);
    }

    [Fact]
    public void Gen3HacksAreIndexedByTheGamesOwnOrder()
    {
        HackSpecies G(int id, string name, int? national, int[] abilities) => new(id, name, national, 0, [12, 12], [40, 45, 35, 70, 65, 55], abilities, 3, 31, 45, 65);
        var data = new HackData
        {
            RomSha1 = new string('0', 40),
            Reader = GbaHackData.ReaderGf,
            Species = [G(277, "Treecko", 252, [65, 77]), G(278, "Grovyle", 253, []), G(279, "Sceptile", null, [65, 0])],
            Items = [new HackItem(13, "Potion", 17, 0, 300), new HackItem(14, "Mega Potion", null, 0, 500)],
            Abilities = [new HackAbility(76, "Cacophony", null), new HackAbility(77, "Air Lock", 76), new HackAbility(1, "Stenchy", 1)],
        };
        var o = HackOverlay.From(data, GameVersion.FR);
        var (_, table) = PersonalOverlay.Create(GameVersion.FR, o.Personal);
        Assert.Equal(70, table[252].SPE);
        Assert.Equal(PersonalTable.FR[277].SPE, table[277].SPE);
        Assert.Equal(76, ((IPersonalAbility12)table[252]).Ability2);
        Assert.Equal(PersonalTable.FR[253].Gender, table[253].Gender);
        Assert.Empty(o.Strings.Species);
        Assert.Empty(o.Strings.Items);
        Assert.Equal("Mega Potion", Assert.Single(o.Strings.ContextItems).Value);
        Assert.Equal("Stenchy", o.Strings.Abilities[1]);
        Assert.False(o.Strings.Abilities.ContainsKey(76));
        Assert.Contains(o.Notes, n => n.Contains("1 species have no national id"));
        Assert.Contains(o.Notes, n => n.Contains("without their gender or abilities"));
        using (o.Use())
            Assert.Equal("Mega Potion", GameInfo.Strings.GetItemStrings(EntityContext.Gen3, GameVersion.FR)[14]);
    }

    [Fact]
    public void GenderIsWrittenOnlyWhenItWasRead()
    {
        HackSpecies Row(bool genderRead) => new(1, "Bulbasaur", 1, 0, [12, 3], [45, 49, 49, 45, 65, 65], [65, 34], null, 0, 45, 64) { GenderRead = genderRead };
        HackData Data(bool genderRead) => new() { RomSha1 = new string('0', 40), Reader = "nds-narc", Species = [Row(genderRead)] };
        var stock = PersonalTable.Pt[1];
        Assert.NotEqual(0, stock.Gender);
        var (_, unread) = PersonalOverlay.Create(GameVersion.Pt, HackOverlay.From(Data(false), GameVersion.Pt).Personal);
        Assert.Equal(stock.Gender, unread[1].Gender);
        Assert.Equal(stock.EXPGrowth, unread[1].EXPGrowth);
        Assert.Equal(34, ((IPersonalAbility12)unread[1]).Ability2);
        var (_, read) = PersonalOverlay.Create(GameVersion.Pt, HackOverlay.From(Data(true), GameVersion.Pt).Personal);
        Assert.Equal(0, read[1].Gender);
    }

    [Fact]
    public void APartialOrUnalignedSpeciesTableGivesNamesOnly()
    {
        var read = Platinum();
        HackData With(string status, bool? aligned) => read with { Tables = [new HackTableStatus(HackTableStatus.Species, status, "planted", aligned)] };
        foreach (var data in new[] { With(HackTableStatus.Partial, false), With(HackTableStatus.Partial, null), With(HackTableStatus.Read, false), With(HackTableStatus.Missing, null) })
        {
            var o = HackOverlay.From(data, GameVersion.Pt);
            var (_, table) = PersonalOverlay.Create(GameVersion.Pt, o.Personal);
            Assert.Equal(PersonalTable.Pt[1].ATK, table[1].ATK);
            Assert.Equal("Mewthree", o.Strings.Species[150]);
            Assert.Contains(o.Notes, n => n.Contains("so the stock stats stay and only the names are taken", StringComparison.Ordinal));
        }
        var (_, taken) = PersonalOverlay.Create(GameVersion.Pt, HackOverlay.From(With(HackTableStatus.Read, true), GameVersion.Pt).Personal);
        Assert.Equal(99, taken[1].ATK);
    }

    private static HackData Black2WithForms(int extraForms = 0, bool linked = true)
    {
        var stock = PersonalTable.StockB2W2;
        var named = stock.MaxSpeciesID + 1;
        var species = NdsHackDataTests.StockSpeciesRows(stock, named);
        var attack = stock[(int)Species.Deoxys].FormIndex((ushort)Species.Deoxys, 1);
        var forms = Enumerable.Range(named, stock.Count - named + extraForms).Select(i =>
        {
            var p = stock[Math.Min(i, stock.Count - 1)];
            var row = new HackSpecies(i, "", null, 0, [p.Type1, p.Type2], [p.HP, i == attack ? 199 : p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], [], p.EXPGrowth, p.Gender, p.CatchRate, p.BaseEXP) { GenderRead = true };
            return i == attack && linked ? row with { BaseSpecies = (int)Species.Deoxys, Form = 1 } : row;
        }).ToList();
        return new HackData
        {
            RomSha1 = new string('0', 40),
            Reader = NdsHackData.Reader,
            Species = species,
            Forms = forms,
            Tables = [new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, null, true)],
        };
    }

    [Fact]
    public void LinkedFormRowsReachTheStockTableByPersonalIndex()
    {
        var attack = PersonalTable.StockB2W2[(int)Species.Deoxys].FormIndex((ushort)Species.Deoxys, 1);
        var o = HackOverlay.From(Black2WithForms(), GameVersion.B2);
        var (_, table) = PersonalOverlay.Create(GameVersion.B2, o.Personal);
        Assert.Equal(199, table[attack].ATK);
        Assert.Contains(o.Notes, n => n.Contains("1 alternate-form rows take the hack's stats by personal index", StringComparison.Ordinal));

        var unlinked = HackOverlay.From(Black2WithForms(linked: false), GameVersion.B2);
        Assert.Equal(PersonalTable.StockB2W2[attack].ATK, PersonalOverlay.Create(GameVersion.B2, unlinked.Personal).Table[attack].ATK);
    }

    [Fact]
    public void FormRowsAreNotWrittenWhenTheRowCountDiffersFromTheStockTable()
    {
        var attack = PersonalTable.StockB2W2[(int)Species.Deoxys].FormIndex((ushort)Species.Deoxys, 1);
        var o = HackOverlay.From(Black2WithForms(extraForms: 1), GameVersion.B2);
        var (_, table) = PersonalOverlay.Create(GameVersion.B2, o.Personal);
        Assert.Equal(PersonalTable.StockB2W2[attack].ATK, table[attack].ATK);
        Assert.Contains(o.Notes, n => n.Contains("so its 1 form rows are not written", StringComparison.Ordinal));
    }
}
