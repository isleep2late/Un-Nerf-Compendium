namespace PKHeX.Core;

/// <summary>
/// Box Data <see cref="ISlotInfo"/>
/// </summary>
public sealed record SlotInfoBox : ISlotInfo
{
    public int Box { get; init; }
    public int Slot { get; init; }

    public SlotInfoBox(int box, int slot, SaveFile sav)
    {
        Box = box;
        Slot = slot;
        if (sav is SAV7b s7b)
        {
            var index = s7b.GetBoxSlotFlags(box, slot);
            if (index.IsParty() >= 0)
                Type = StorageSlotType.Party;
        }
    }

    public StorageSlotType Type { get; private set; } = StorageSlotType.Box;

    // PKHaX: allow writing to Battle Team slots even when the team is locked; other lock types stay protected.
    public static bool AllowBattleTeamWrites { get; set; } = true;

    public bool CanWriteTo(SaveFile sav) => sav.HasBox && (!sav.IsBoxSlotLocked(Box, Slot) || IsBattleTeamOverride(sav)); // PKHaX

    private bool IsBattleTeamOverride(SaveFile sav) => AllowBattleTeamWrites && sav.GetBoxSlotFlags(Box, Slot).IsBattleTeam() >= 0; // PKHaX
    public WriteBlockedMessage CanWriteTo(SaveFile sav, PKM pk) => WriteBlockedMessage.None;

    public bool WriteTo(SaveFile sav, PKM pk, EntityImportSettings settings = default)
    {
        sav.SetBoxSlotAtIndex(pk, Box, Slot, settings);
        return true;
    }

    public PKM Read(SaveFile sav)
    {
        return sav.GetBoxSlotAtIndex(Box, Slot);
    }
}
