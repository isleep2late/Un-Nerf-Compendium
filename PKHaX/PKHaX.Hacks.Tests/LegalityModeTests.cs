using PKHaX.Hacks.Overlay;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class LegalityModeTests
{
    [Theory]
    [InlineData("vanilla", LegalityLevel.Advisory)]
    [InlineData("unnerf", LegalityLevel.Advisory)]
    [InlineData("owner-build", LegalityLevel.Advisory)]
    [InlineData("hack", LegalityLevel.Off)]
    [InlineData("hack+unnerf", LegalityLevel.Off)]
    [InlineData("imported", LegalityLevel.Off)]
    public void KindDecidesLegality(string kind, LegalityLevel expected)
    {
        Assert.Equal(expected, LegalityMode.For(kind));
        Assert.Equal(kind, ImageKinds.Name(ImageKinds.Parse(kind)));
    }

    [Fact]
    public void OffSkipsVerdictPanelAndEncounters()
    {
        Assert.False(LegalityMode.RunsVerdict(LegalityLevel.Off));
        Assert.False(LegalityMode.ShowsLegalityPanel(LegalityLevel.Off));
        Assert.False(LegalityMode.LoadsEncounterDatabase(LegalityLevel.Off));
        Assert.True(LegalityMode.RunsVerdict(LegalityLevel.Advisory));
    }

    [Theory]
    [InlineData("HackUnNerf")]
    [InlineData("Hack")]
    [InlineData("")]
    public void UnknownKindNamesAreRefused(string name)
    {
        Assert.False(ImageKinds.TryParse(name, out _));
        Assert.Throws<System.ArgumentException>(() => LegalityMode.For(name));
    }
}
