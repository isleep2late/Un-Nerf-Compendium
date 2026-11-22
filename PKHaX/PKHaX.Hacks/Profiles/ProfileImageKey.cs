using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PKHaX.Hacks.Profiles;

public static class ProfileImageKey
{
    public const string NoMods = "none";

    public static string? RomImage(string? sha1) => IsHex(sha1, 40) ? sha1!.ToLowerInvariant() : null;

    public static string? CtrBaked(string? imageRoot) => IsHex(imageRoot, 64) ? imageRoot!.ToLowerInvariant() : null;

    public static string ForCtrMods(ulong titleId, string codeSha1, string modSet, uint updateVersion)
    {
        if (!IsHex(codeSha1, 40))
            throw new FormatException($"'{codeSha1}' is not a 40-digit code SHA-1.");
        return Sha1Hex($"3ds\n{TitleId16(titleId)}\n{codeSha1.ToLowerInvariant()}\n{CanonicalModSet(modSet)}\n{updateVersion.ToString(CultureInfo.InvariantCulture)}");
    }

    public static string ForSwitch(ulong titleId, string mainBuildId, uint updateVersion, string modSet) =>
        Sha1Hex($"nx\n{TitleId16(titleId)}\n{CanonicalBuildId(mainBuildId)}\n{updateVersion.ToString(CultureInfo.InvariantCulture)}\n{CanonicalModSet(modSet)}");

    public static string TitleId16(ulong titleId) => titleId.ToString("X16", CultureInfo.InvariantCulture);

    public static bool TryParseTitleId(string? text, out ulong titleId)
    {
        titleId = 0;
        return text is { Length: 16 } && text.All(Uri.IsHexDigit) && ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out titleId);
    }

    public static string CanonicalBuildId(string mainBuildId)
    {
        ArgumentNullException.ThrowIfNull(mainBuildId);
        var id = mainBuildId.Trim().ToUpperInvariant();
        if (id.Length == 0 || id.Length % 2 != 0 || !id.All(Uri.IsHexDigit))
            throw new FormatException($"'{mainBuildId}' is not a hex build id.");
        if (id.Length > 40 && id[40..].All(c => c == '0'))
            id = id[..40];
        if (id.Length == 40 && id[32..].All(c => c == '0'))
            id = id[..32];
        if (id.Length is not (32 or 40))
            throw new FormatException($"'{mainBuildId}' is not a 32- or 40-digit main build id.");
        return id;
    }

    public static string CanonicalModSet(string modSet)
    {
        ArgumentNullException.ThrowIfNull(modSet);
        var m = modSet.Trim();
        if (m == NoMods)
            return m;
        if (!IsHex(m, 64))
            throw new FormatException($"'{modSet}' is not a mod set hash (\"none\" or 64 hex digits).");
        return m.ToLowerInvariant();
    }

    internal static bool IsHex(string? text, int length) => text is not null && text.Length == length && text.All(Uri.IsHexDigit);

    internal static bool IsLowerHex(string? text, int length) => IsHex(text, length) && text!.All(c => !char.IsUpper(c));

    private static string Sha1Hex(string text) => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text)));
}
