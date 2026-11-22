using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.HookPersonalOverride)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.HookDetachedStrings)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.HookFrontierShift)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.HookGen1Offsets)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.HookRamScanLimits)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.HookMoveOverride)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.OverlayPersonal)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.OverlayStrings)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.OverlayLegality)]
[assembly: PKHaX.Hacks.PkhaxModule(PKHaX.Hacks.PkhaxCapabilities.ReadersHackSave)]

namespace PKHaX.Hacks;

public static class PkhaxCapabilities
{
    public const string Schema = "pkhax-hacks/1";

    public const string HookPersonalOverride = "hook.personal-override";
    public const string HookDetachedStrings = "hook.detached-strings";
    public const string HookFrontierShift = "hook.frontier-shift";
    public const string HookGen1Offsets = "hook.gen1-offsets";
    public const string HookRamScanLimits = "hook.ram-scan-limits";
    public const string HookMoveOverride = "hook.move-override";
    public const string OverlayPersonal = "overlay.personal";
    public const string OverlayStrings = "overlay.strings";
    public const string OverlayLegality = "overlay.legality";
    public const string ReadersHackSave = "readers.hack-save";

    private static readonly Lazy<string[]> modules = new(Load);

    public static IReadOnlyList<string> Modules => modules.Value;

    public static bool Has(string id) => Array.BinarySearch(modules.Value, id, StringComparer.Ordinal) >= 0;

    private static string[] Load() => typeof(PkhaxCapabilities).Assembly
        .GetCustomAttributes<PkhaxModuleAttribute>()
        .Select(a => a.Id)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();
}
