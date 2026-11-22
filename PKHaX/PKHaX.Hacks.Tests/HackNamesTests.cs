using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Records;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class HackNamesTests
{
    private static HackSpecies Species(int id, string name, params int[] abilities) => new(id, name, null, 0, [0, 0], [45, 49, 49, 45, 65, 65], abilities, 0, 127, 45, 64);

    private static HackData Data() => new()
    {
        RomSha1 = new string('0', 40),
        Reader = "test",
        Species = [Species(0, "??????????"), Species(1, "Bulbasaur", 1, 2, 3), Species(2, "Ivysaur"), Species(3, ""), Species(4, "Charmander", 2), Species(5, "Charmeleon"), Species(1200, "Fakemon")],
        Moves = [new(0, "-", null, 0, 0, 0, 0), new(1, "Pound", null, 40, 0, 100, 35), new(2, "Pound", null, 50, 0, 100, 30), new(3, "Mega Pound", null, 90, 0, 100, 5)],
        Items = [new(0, "????????", null, 0, 0), new(1, "Master Ball", null, 0, 0)],
        Abilities = [new(0, "-------", null), new(1, "Stench", null), new(2, "Drizzle", null), new(3, "Speed Boost", null)],
    };

    [Fact]
    public void NamesAnswerByIdAndSayWhatIsMissing()
    {
        var names = HackNames.From(Data());
        Assert.False(names.IsEmpty);
        Assert.Null(names.NumberBanner);
        Assert.Equal("Bulbasaur", names.Name(HackNameKind.Species, 1));
        Assert.Equal("Fakemon", names.Name(HackNameKind.Species, 1200));
        Assert.Equal(1201, names.Limit(HackNameKind.Species));
        Assert.Equal("#3 (no name read from the ROM)", names.Name(HackNameKind.Species, 3));
        Assert.Equal("#700 (no name read from the ROM)", names.Name(HackNameKind.Species, 700));
        Assert.Equal("#1201 (outside this hack's table of 1201)", names.Name(HackNameKind.Species, 1201));
        Assert.Equal("Bulbasaur (#1)", names.Shown(HackNameKind.Species, 1));
        Assert.Null(names.WhyRefused(HackNameKind.Species, 1200));
        Assert.Contains("outside this hack's table of 1201", names.WhyRefused(HackNameKind.Species, 1201));
        Assert.Contains("not written", names.WhyRefused(HackNameKind.Move, 4));
    }

    [Fact]
    public void TwoEntriesWithOneNameShowTheirIds()
    {
        var names = HackNames.From(Data());
        Assert.Equal("Pound #1", names.Name(HackNameKind.Move, 1));
        Assert.Equal("Pound #2", names.Name(HackNameKind.Move, 2));
        Assert.Equal("Mega Pound", names.Name(HackNameKind.Move, 3));
    }

    [Fact]
    public void SearchPutsExactThenPrefixThenInsideAndTakesAnId()
    {
        var names = HackNames.From(Data());
        Assert.Equal(new[] { 1, 2, 3 }, names.Search(HackNameKind.Move, "pound").Select(e => e.Id).ToArray());
        Assert.Equal(new[] { 4, 5 }, names.Search(HackNameKind.Species, "CHAR").Select(e => e.Id).ToArray());
        Assert.Equal(new[] { 1, 2 }, names.Search(HackNameKind.Species, "saur").Select(e => e.Id).ToArray());
        Assert.Equal(new[] { 1200 }, names.Search(HackNameKind.Species, "#1200").Select(e => e.Id).ToArray());
        Assert.Empty(names.Search(HackNameKind.Species, "#1201"));
        Assert.Equal(3, Assert.Single(names.Search(HackNameKind.Species, " #3 ")).Id);
        Assert.Equal(1200, names.Resolve(HackNameKind.Species, "#1200"));
        Assert.Equal(1200, names.Resolve(HackNameKind.Species, "fakemon"));
        Assert.Null(names.Resolve(HackNameKind.Species, "saur"));
        var many = HackNames.From(Data() with { Items = [.. Enumerable.Range(0, 120).Select(i => new HackItem(i, $"Berry {i}", null, 0, 0))] });
        Assert.Equal(HackNames.SearchCap, many.Search(HackNameKind.Item, "berry").Count);
        Assert.Equal(HackNames.SearchCap, many.Search(HackNameKind.Item, "").Count);
    }

    [Fact]
    public void AbilityRowsAreTheSpeciesOwnSlots()
    {
        var names = HackNames.From(Data());
        Assert.Equal(new[] { "Slot 1: Stench", "Slot 2: Drizzle", "Hidden: Speed Boost" }, names.AbilitySlots(1).ToArray());
        Assert.Equal(new[] { "Slot 1: Drizzle", "Slot 2", "Hidden" }, names.AbilitySlots(4).ToArray());
        Assert.Equal(new[] { "Slot 1", "Slot 2", "Hidden" }, names.AbilitySlots(2).ToArray());
        Assert.Equal(new[] { "Slot 1", "Slot 2", "Hidden" }, names.AbilitySlots(9999).ToArray());
    }

    [Fact]
    public void WithNoTablesTheCatalogIsEmptyAndNumbersAreCappedByTheCodec()
    {
        var names = HackNames.From(null);
        Assert.True(names.IsEmpty);
        Assert.Equal(HackNames.Banner, names.NumberBanner);
        Assert.Equal("#25", names.Name(HackNameKind.Species, 25));
        Assert.Null(names.WhyRefused(HackNameKind.Species, 60000));
        Assert.Empty(names.Search(HackNameKind.Species, "pika"));
        Assert.Equal(25, Assert.Single(names.Search(HackNameKind.Species, "#25")).Id);
        Assert.Equal(0x7FF, EditFieldSpecs.NumberCap(RecordCodecId.Pk3x, EditFieldKind.SpeciesId));
        Assert.Equal(0x3FF, EditFieldSpecs.NumberCap(RecordCodecId.Pk3CfruCompact58, EditFieldKind.MoveId));
        Assert.Equal(0xFFFF, EditFieldSpecs.NumberCap(RecordCodecId.Pk3CfruPlain, EditFieldKind.MoveId));
        Assert.Equal(0x3FF, EditFieldSpecs.NumberCap(RecordCodecId.Pk3x, EditFieldKind.ItemId));
    }

    [Fact]
    public void FieldSpecsFollowTheCodecAndTodaysWritableLists()
    {
        var box = EditFieldSpecs.For(RecordCodecId.Pk3CfruCompact58, RecordShape.Stored);
        Assert.All(EditableFields.For(RecordCodecId.Pk3CfruCompact58).Intersect(EditableFields.Modelled), f => Assert.True(box.Single(s => s.Field == f).Editable, f));
        Assert.Equal(EditFieldSpecs.PidNature, box.Single(s => s.Field == EditFieldSpecs.Nature).Why);
        Assert.Equal(EditFieldSpecs.StoredLevel, box.Single(s => s.Field == EditableFields.Level).Why);
        Assert.Equal((1, 0x7FF, EditFieldKind.SpeciesId), (box[0].Min, box[0].Max, box[0].Kind));
        var party = EditFieldSpecs.For(RecordCodecId.Pk3CfruPlain, RecordShape.Party);
        Assert.Equal(EditFieldSpecs.PartyLocked, party.Single(s => s.Field == "Species").Why);
        Assert.True(party.Single(s => s.Field == "HeldItem").Editable);
        var expansion = EditFieldSpecs.For(RecordCodecId.Pk3x, RecordShape.Stored);
        Assert.Equal(EditableFields.For(RecordCodecId.Pk3x).Order(), expansion.Where(s => s.Editable).Select(s => s.Field).Order());
        Assert.Null(expansion.Single(s => s.Field == EditFieldSpecs.Nickname).Why);
        Assert.Equal(EditFieldSpecs.StoredLevel, expansion.Single(s => s.Field == EditableFields.Level).Why);
        Assert.Equal((12, 2), (expansion.Single(s => s.Field == EditFieldSpecs.Nickname).Max, expansion.Single(s => s.Field == EditFieldSpecs.AbilitySlot).Max));
        Assert.DoesNotContain(EditFieldSpecs.For(RecordCodecId.Pk3SoulGold96, RecordShape.Party), s => s.Field == EditFieldSpecs.AbilitySlot);
        Assert.Empty(EditFieldSpecs.For(RecordCodecId.Pk6, RecordShape.Stored));
    }
}
