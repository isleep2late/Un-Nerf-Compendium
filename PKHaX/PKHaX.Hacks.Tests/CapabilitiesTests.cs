using System.Linq;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class CapabilitiesTests
{
    [Fact]
    public void ScaffoldModulesAreDeclared()
    {
        Assert.Equal("pkhax-hacks/1", PkhaxCapabilities.Schema);
        string[] expected =
        [
            PkhaxCapabilities.HookDetachedStrings,
            PkhaxCapabilities.HookFrontierShift,
            PkhaxCapabilities.HookGen1Offsets,
            PkhaxCapabilities.HookMoveOverride,
            PkhaxCapabilities.HookPersonalOverride,
            PkhaxCapabilities.HookRamScanLimits,
            PkhaxCapabilities.OverlayLegality,
            PkhaxCapabilities.OverlayPersonal,
            PkhaxCapabilities.OverlayStrings,
            PkhaxCapabilities.ReadersHackSave,
        ];
        foreach (var id in expected)
            Assert.True(PkhaxCapabilities.Has(id), id);
        Assert.False(PkhaxCapabilities.Has("records.codecs"));
        Assert.Equal(PkhaxCapabilities.Modules.Order(System.StringComparer.Ordinal), PkhaxCapabilities.Modules);
    }
}
