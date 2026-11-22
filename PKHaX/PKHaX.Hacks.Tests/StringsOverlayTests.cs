using System;
using System.Collections.Generic;
using PKHaX.Hacks.Overlay;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class StringsOverlayTests
{
    [Fact]
    public void DetachedStringsAreANewInstance()
    {
        var cached = GameInfo.GetStrings("en");
        var detached = GameStrings.CreateDetached("en");
        Assert.NotSame(cached, detached);
        Assert.NotSame(cached.specieslist, detached.specieslist);
        Assert.NotSame(cached.movelist, detached.movelist);
        Assert.NotSame(cached.itemlist, detached.itemlist);
        Assert.Equal(cached.specieslist, detached.specieslist);
        Assert.Equal(cached.movelist, detached.movelist);
        Assert.Equal(cached.itemlist, detached.itemlist);
        Assert.Equal(cached.abilitylist, detached.abilitylist);
        Assert.Equal(cached.types, detached.types);
        detached.specieslist[1] = "Changed";
        Assert.Equal("Bulbasaur", cached.specieslist[1]);
        Assert.Equal("Bulbasaur", GameStrings.CreateDetached("en").specieslist[1]);
    }

    [Fact]
    public void OverlaySetsAndRestoresGameInfoStrings()
    {
        var before = GameInfo.Strings;
        var patch = new StringsPatch
        {
            Species = new Dictionary<int, string> { [1] = "Hackasaur" },
            Types = new Dictionary<int, string> { [17] = "Fairy*" },
            Items = new Dictionary<int, string> { [1] = "Hack Ball" },
        };
        using (var overlay = StringsOverlay.Use("en", EntityContext.Gen4, GameVersion.Pt, patch))
        {
            Assert.True(StringsOverlay.IsActive);
            Assert.Same(overlay.Strings, GameInfo.Strings);
            Assert.Equal("Hackasaur", GameInfo.Strings.specieslist[1]);
            Assert.Equal("Fairy*", GameInfo.Strings.types[17]);
            Assert.Equal("Hack Ball", GameInfo.Strings.GetItemStrings(EntityContext.Gen4, GameVersion.Pt)[1]);
            Assert.Throws<InvalidOperationException>(() => StringsOverlay.Use("en", EntityContext.Gen4, GameVersion.Pt, patch));
        }
        Assert.False(StringsOverlay.IsActive);
        Assert.Same(before, GameInfo.Strings);
        Assert.Equal("Bulbasaur", GameInfo.Strings.specieslist[1]);
    }

    [Theory]
    [InlineData(EntityContext.Gen1, GameVersion.RB)]
    [InlineData(EntityContext.Gen2, GameVersion.C)]
    [InlineData(EntityContext.Gen3, GameVersion.E)]
    [InlineData(EntityContext.Gen3, GameVersion.FR)]
    public void NationalAndContextItemsPatchTheirOwnIndexSpace(EntityContext context, GameVersion version)
    {
        var stock = GameStrings.CreateDetached("en");
        var stockNational = stock.itemlist[13];
        var stockOwn = stock.GetItemStrings(context, version)[13];
        Assert.NotEqual(stockNational, stockOwn);

        var national = StringsOverlay.Build("en", context, version, new StringsPatch { Items = new Dictionary<int, string> { [13] = "NATIONAL" } });
        Assert.Equal("NATIONAL", national.itemlist[13]);
        Assert.Equal(stockOwn, national.GetItemStrings(context, version)[13]);

        var own = StringsOverlay.Build("en", context, version, new StringsPatch { ContextItems = new Dictionary<int, string> { [13] = "OWN" } });
        Assert.Equal(stockNational, own.itemlist[13]);
        Assert.Equal("OWN", own.GetItemStrings(context, version)[13]);
    }

    [Fact]
    public void Gen3ContextItemsSurviveAnEReaderBerry()
    {
        var berry = new SAV3E();
        "PUMKIN"u8.CopyTo(berry.LargeBlock.EReaderBerry);
        EReaderBerrySettings.LoadFrom(berry);
        try
        {
            Assert.False(EReaderBerrySettings.IsEnigma);
            var strings = StringsOverlay.Build("en", EntityContext.Gen3, GameVersion.E, new StringsPatch { ContextItems = new Dictionary<int, string> { [13] = "HACK POTION" } });
            var first = strings.GetItemStrings(EntityContext.Gen3, GameVersion.E);
            var second = strings.GetItemStrings(EntityContext.Gen3, GameVersion.E);
            Assert.NotSame(first, second);
            Assert.Equal("HACK POTION", first[13]);
            Assert.Equal("HACK POTION", second[13]);
        }
        finally
        {
            EReaderBerrySettings.LoadFrom(new SAV3E());
        }
        Assert.True(EReaderBerrySettings.IsEnigma);
    }

    [Fact]
    public void Gen4NationalItemsReachTheMailList()
    {
        var strings = StringsOverlay.Build("en", EntityContext.Gen4, GameVersion.HGSS, new StringsPatch { Items = new Dictionary<int, string> { [137] = "Hack Mail" } });
        Assert.Equal("Hack Mail", strings.itemlist[137]);
        Assert.Equal("Hack Mail", strings.GetItemStrings(EntityContext.Gen4, GameVersion.HGSS)[137]);
    }

    [Fact]
    public void ContextItemsAreRefusedWhereItemsAreNational()
    {
        var patch = new StringsPatch { ContextItems = new Dictionary<int, string> { [13] = "X" } };
        Assert.Throws<ArgumentException>(() => StringsOverlay.Build("en", EntityContext.Gen4, GameVersion.Pt, patch));
        Assert.Throws<ArgumentException>(() => StringsOverlay.Use("en", EntityContext.Gen8, GameVersion.SWSH, patch));
        Assert.False(StringsOverlay.IsActive);
    }

    [Fact]
    public void OnlyADetachedInstanceHandsOutItsItemLists()
    {
        Assert.Throws<InvalidOperationException>(() => GameInfo.GetStrings("en").GetItemListForEdit(EntityContext.Gen3, GameVersion.E));
    }

    [Fact]
    public void OverlayNeverGrowsAList()
    {
        var patch = new StringsPatch { Species = new Dictionary<int, string> { [100000] = "Nope" } };
        Assert.Throws<ArgumentOutOfRangeException>(() => StringsOverlay.Use("en", EntityContext.Gen3, GameVersion.E, patch));
        Assert.False(StringsOverlay.IsActive);
    }

    [Fact]
    public void ReassertPutsTheOverlayBackAfterTheStringsAreRebuilt()
    {
        Assert.False(StringsOverlay.Reassert());
        using (StringsOverlay.Use("en", EntityContext.Gen4, GameVersion.Pt, new StringsPatch { Species = new System.Collections.Generic.Dictionary<int, string> { [150] = "Mewthree" } }))
        {
            GameInfo.Strings = GameInfo.GetStrings("en");
            Assert.Equal("Mewtwo", GameInfo.Strings.specieslist[150]);
            Assert.True(StringsOverlay.Reassert());
            Assert.Equal("Mewthree", GameInfo.Strings.specieslist[150]);
            Assert.False(StringsOverlay.Reassert());
        }
        Assert.Equal("Mewtwo", GameInfo.Strings.specieslist[150]);
    }
}
