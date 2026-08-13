using FluentAssertions;
using PKHeX.Core;
using Xunit;

namespace PKHeX.Core.Tests.Entity;

public class GBHaxExportTests
{
    [Fact]
    public void Gen1_Export_Includes_Sprite_Types_Status_Level()
    {
        var pk = new PK1 { Species = (ushort)Species.Mew };
        pk.HeaderSpeciesInternal = SpeciesConverter.GetInternal1((ushort)Species.Tentacool); // disguise sprite
        pk.Type1 = 20; // Fire (Gen-1 internal)
        pk.Type2 = 24; // Psychic
        pk.Status_Condition = (byte)StatusCondition.Sleep2;
        pk.Stat_Level = 255;

        var text = ShowdownParsing.GetShowdownText(pk);
        text.Should().Contain("Sprite: Tentacool");
        text.Should().Contain("Types: Fire / Psychic");
        text.Should().Contain("Status: Sleep");
        text.Should().Contain("Level: 255");
    }

    [Fact]
    public void Gen2_Export_Includes_Status_Level_NoSpriteTypes()
    {
        var pk = new PK2 { Species = (ushort)Species.Snorlax };
        pk.Status_Condition = (byte)StatusCondition.Burn;
        pk.Stat_Level = 200;

        var text = ShowdownParsing.GetShowdownText(pk);
        text.Should().Contain("Status: Burn");
        text.Should().Contain("Level: 200");
        text.Should().NotContain("Sprite:");
        text.Should().NotContain("Types:");
    }

    [Fact]
    public void Gen1_Import_Parses_Sprite_Types_Status_Level()
    {
        const string set = "Mew\nSprite: Tentacool\nTypes: Fire / Psychic\nStatus: Sleep\nLevel: 255\n- Pound";
        var ss = new ShowdownSet(set);
        ss.PhSpriteSpecies.Should().Be((ushort)Species.Tentacool);
        ss.PhType1.Should().Be(20);
        ss.PhType2.Should().Be(24);
        GBHaxFormat.GetStatusWord(ss.PhStatusByte).Should().Be("Sleep");
        ss.Level.Should().Be(255);
    }
}
