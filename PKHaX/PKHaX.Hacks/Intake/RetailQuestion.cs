using PKHeX.Core;

namespace PKHaX.Hacks.Intake;

public static class RetailQuestion
{
    public static string? GameOf(SaveFile save) => save switch
    {
        SAV3E => "Emerald",
        SAV3FRLG => "FireRed or LeafGreen",
        SAV3RS => "Ruby or Sapphire",
        _ => null,
    };

    public static string Ask(string name, bool emerald)
    {
        var text = $"Is this save from an unmodified Pokemon {name}? ROM hacks of {name} keep its file layout but store "
            + $"their data differently, and the editor would corrupt one by writing it as {name}.";
        if (emerald)
            text += " Emerald with the Un-Nerf Compendium's patches counts as unmodified here: they keep the retail save layout.";
        return text;
    }

    public static string ConfirmLabel(string name) => $"Yes, an unmodified {name}";

    public static string Refusal(string? question) => "Nothing was written. " + question + " Until you answer yes, this save stays read-only.";

    public static string Banner(string? question) => question + " Until you confirm, this save is read-only.";

    public static string NotReproducible(string name, int diff) => diff < 0
        ? $"The editor could not read this save back as a retail {name} save, so it opens read-only."
        : $"Writing this save back unchanged as a retail {name} save would change {diff} byte{(diff == 1 ? "" : "s")}, "
            + "so its layout is not the retail one. The editor opens it read-only.";
}
