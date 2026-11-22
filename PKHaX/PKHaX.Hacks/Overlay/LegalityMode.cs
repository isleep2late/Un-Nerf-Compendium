namespace PKHaX.Hacks.Overlay;

public enum LegalityLevel
{
    Advisory,
    Off,
    HackLearnsets,
}

public static class LegalityMode
{
    public static LegalityLevel For(ImageKind kind) => ImageKinds.IsHack(kind) ? LegalityLevel.Off : LegalityLevel.Advisory;

    public static LegalityLevel For(string kind) => For(ImageKinds.Parse(kind));

    public static bool RunsVerdict(LegalityLevel level) => level == LegalityLevel.Advisory;

    public static bool LoadsEncounterDatabase(LegalityLevel level) => level == LegalityLevel.Advisory;

    public static bool ShowsLegalityPanel(LegalityLevel level) => level != LegalityLevel.Off;
}
