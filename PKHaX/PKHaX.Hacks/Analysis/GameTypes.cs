namespace PKHaX.Hacks.Analysis;

public static class GameTypes
{
    public const int MysteryRaw34 = 9;

    public static bool HasMysterySlot(int generation) => generation is 3 or 4;

    public static int? ToPkhex(int generation, int raw)
    {
        if (!HasMysterySlot(generation))
            return raw;
        return raw switch
        {
            < MysteryRaw34 => raw,
            MysteryRaw34 => null,
            _ => raw - 1,
        };
    }

    public static int FromPkhex(int generation, int type) =>
        HasMysterySlot(generation) && type >= MysteryRaw34 ? type + 1 : type;
}
