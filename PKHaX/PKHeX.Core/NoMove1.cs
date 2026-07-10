namespace PKHeX.Core;

/// <summary>
/// PKHaX: Generation 1 glitch move ID 0x00 ("No Move"), distinct from an empty (None) slot.
/// Stored in the save as move ID 0 with nonzero PP; represented in the editor by a sentinel ID.
/// </summary>
public static class NoMove1
{
    public const ushort Sentinel = (ushort)Move.MAX_COUNT;
    public const int DefaultPP = 10;

    public static bool IsNoMoveSlot(ushort move, int pp) => move == 0 && pp > 0;
}
