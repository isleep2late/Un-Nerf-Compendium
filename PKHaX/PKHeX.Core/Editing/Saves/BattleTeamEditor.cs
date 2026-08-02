using System;

namespace PKHeX.Core;

// PKHaX: unified editor for Battle Teams (Gen 7: lists of box slot indices) and the Battle Box (Gen 6: separate storage).
/// <summary>
/// Exposes a common surface for viewing and editing a save file's battle teams.
/// </summary>
/// <remarks>
/// Gen 7 (<see cref="SAV7"/>) battle teams are lists of box slot indices stored in <see cref="BoxLayout7.TeamSlots"/>.
/// Gen 6 (<see cref="SAV6XY"/>/<see cref="SAV6AO"/>) has a single Battle Box with dedicated storage (<see cref="BattleBox6"/>).
/// </remarks>
public sealed class BattleTeamEditor
{
    public const int SlotsPerTeam = 6;

    private readonly SaveFile SAV;
    private readonly BoxLayout7? Teams7;
    private readonly BattleBox6? Box6;

    private BattleTeamEditor(SaveFile sav, BoxLayout7? teams7, BattleBox6? box6)
    {
        SAV = sav;
        Teams7 = teams7;
        Box6 = box6;
    }

    /// <summary>
    /// Creates an editor if the save file supports battle teams; returns null otherwise.
    /// </summary>
    public static BattleTeamEditor? TryCreate(SaveFile sav) => sav switch
    {
        SAV7 s7 => new(s7, s7.BoxLayout, null),
        SAV6XY xy => new(xy, null, xy.BattleBox),
        SAV6AO ao => new(ao, null, ao.BattleBox),
        _ => null,
    };

    /// <summary> True if teams are stored as box slot indices (Gen 7); false if separate storage (Gen 6 Battle Box). </summary>
    public bool HasIndexedTeams => Teams7 is not null;

    public int TeamCount => Teams7 is not null ? Teams7.TeamSlots.Length / SlotsPerTeam : 1;

    public string GetTeamName(int team) => Teams7 is not null ? $"Battle Team {team + 1}" : "Battle Box";

    public bool GetIsLocked(int team)
    {
        if (Teams7 is not null)
            return Teams7.GetIsTeamLocked(team);
        return Box6?.Locked ?? false;
    }

    public void SetIsLocked(int team, bool value)
    {
        if (Teams7 is not null)
            Teams7.SetIsTeamLocked(team, value);
        else if (Box6 is not null)
            Box6.Locked = value;
        SAV.State.Edited = true;
    }

    /// <summary> Gets the box slot index a team slot points to, or -1 if unassigned (or not an indexed-team save). </summary>
    public int GetTeamSlotIndex(int team, int slot)
    {
        if (Teams7 is null)
            return -1;
        return Teams7.TeamSlots[(team * SlotsPerTeam) + slot];
    }

    /// <summary> Points a team slot at the requested box slot index. Rejects out-of-range and within-team duplicate indices. </summary>
    public bool SetTeamSlotIndex(int team, int slot, int boxIndex)
    {
        if (Teams7 is null)
            return false;
        if ((uint)boxIndex >= (uint)SAV.SlotCount)
            return false;

        var arr = Teams7.TeamSlots;
        int start = team * SlotsPerTeam;
        for (int i = 0; i < SlotsPerTeam; i++)
        {
            if (i != slot && arr[start + i] == boxIndex)
                return false; // already referenced by this team
        }
        arr[start + slot] = boxIndex;
        SAV.State.Edited = true;
        return true;
    }

    /// <summary> Un-assigns a team slot (Gen 7) or blanks out the Battle Box slot's data (Gen 6). </summary>
    public void ClearTeamSlot(int team, int slot)
    {
        if (Teams7 is not null)
        {
            Teams7.TeamSlots[(team * SlotsPerTeam) + slot] = -1;
        }
        else if (Box6 is not null)
        {
            var info = GetSlotInfo(team, slot, writable: true);
            info?.WriteTo(SAV, SAV.BlankPKM);
        }
        SAV.State.Edited = true;
    }

    /// <summary>
    /// Gets a slot accessor for the team slot: the referenced box slot (Gen 7, null if unassigned),
    /// or the Battle Box storage slot (Gen 6).
    /// </summary>
    public ISlotInfo? GetSlotInfo(int team, int slot, bool writable)
    {
        if (Teams7 is not null)
        {
            int index = GetTeamSlotIndex(team, slot);
            if (index < 0)
                return null;
            SAV.GetBoxSlotFromIndex(index, out var box, out var boxSlot);
            return new SlotInfoBox(box, boxSlot, SAV);
        }
        if (Box6 is not null)
            return new SlotInfoMisc(Box6.GetSlot(slot), slot, Mutable: writable) { Type = StorageSlotType.BattleBox };
        return null;
    }

    /// <summary> Reads the current occupant of a team slot; null if the slot is unassigned. </summary>
    public PKM? Read(int team, int slot) => GetSlotInfo(team, slot, writable: false)?.Read(SAV);
}
