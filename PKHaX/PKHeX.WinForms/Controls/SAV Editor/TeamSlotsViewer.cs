// PKHaX: Battle Team / Battle Box slot panel with full drag & drop support.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// Displays the six slots of a battle team (Gen 7) or the Battle Box (Gen 6),
/// participating in the shared <see cref="SlotChangeManager"/> drag &amp; drop machinery.
/// </summary>
public sealed class TeamSlotsViewer : UserControl, ISlotViewer<PictureBox>
{
    private readonly ComboBox CB_Team;
    private readonly CheckBox CHK_Locked;
    private readonly PokeGrid TeamPokeGrid;

    public BattleTeamEditor? Teams { get; private set; }
    public SlotChangeManager? M { get; private set; }
    public SaveFile SAV => M?.SE.SAV ?? throw new ArgumentNullException(nameof(SAV));
    public IList<PictureBox> SlotPictureBoxes => TeamPokeGrid.Entries;
    public int ViewIndex => -4;

    private Func<PKM, bool>? _searchFilter;
    private bool _updatingLock;

    public TeamSlotsViewer()
    {
        SuspendLayout();
        CB_Team = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(0, 0),
            Size = new Size(128, 25),
            Name = "CB_Team",
        };
        CB_Team.SelectedIndexChanged += (_, _) => ChangeTeam();

        CHK_Locked = new CheckBox
        {
            Text = "Locked",
            AutoSize = true,
            Location = new Point(0, 27),
            Name = "CHK_Locked",
        };
        CHK_Locked.CheckedChanged += (_, _) => ToggleLock();

        TeamPokeGrid = new PokeGrid
        {
            Location = new Point(0, 50),
            Margin = Padding.Empty,
            Name = "TeamPokeGrid",
        };

        Controls.Add(CB_Team);
        Controls.Add(CHK_Locked);
        Controls.Add(TeamPokeGrid);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Name = "TeamSlotsViewer";
        ResumeLayout(false);
    }

    public void Setup(SlotChangeManager m, BattleTeamEditor teams, ContextMenuStrip? mnu)
    {
        M = m;
        Teams = teams;

        TeamPokeGrid.InitializeGrid(2, 3, SpriteUtil.Spriter);
        TeamPokeGrid.Location = new Point(0, CHK_Locked.Bottom + 2);
        foreach (var pb in TeamPokeGrid.Entries)
        {
            pb.MouseEnter += (_, args) => M?.MouseEnter(pb, args);
            pb.MouseLeave += (_, args) => M?.MouseLeave(pb, args);
            pb.MouseClick += (_, args) => M?.MouseClick(pb, args);
            pb.MouseMove += (_, args) => M?.MouseMove(pb, args);
            pb.MouseDown += (_, args) => M?.MouseDown(pb, args);
            pb.MouseUp += (_, args) => M?.MouseUp(pb, args);

            pb.DragEnter += (_, args) => M?.DragEnter(pb, args);
            pb.DragDrop += (_, args) => M?.DragDrop(pb, args);
            pb.QueryContinueDrag += (_, args) => M?.QueryContinueDrag(pb, args);
            pb.GiveFeedback += (_, e) => e.UseDefaultCursors = false;
            pb.AllowDrop = true;
            pb.ContextMenuStrip = mnu;
        }

        CB_Team.Items.Clear();
        for (int i = 0; i < teams.TeamCount; i++)
            CB_Team.Items.Add(teams.GetTeamName(i));
        CB_Team.Visible = teams.TeamCount > 1;
        CB_Team.Width = Math.Max(TeamPokeGrid.Width, 100);
        CB_Team.SelectedIndex = 0;

        if (Application.IsDarkModeEnabled)
            WinFormsTranslator.ReformatDark(CB_Team);
    }

    public int CurrentTeam => Math.Max(0, CB_Team.SelectedIndex);

    private void ChangeTeam()
    {
        M?.Hover.Stop();
        ResetSlots();
    }

    private void ToggleLock()
    {
        if (_updatingLock || Teams is null)
            return;
        Teams.SetIsLocked(CurrentTeam, CHK_Locked.Checked);
        M?.SE.UpdateBoxViewers(all: true); // refresh padlock overlays on box slots
        ResetSlots();
    }

    public ISlotInfo GetSlotData(PictureBox view)
    {
        int i = TeamPokeGrid.Entries.IndexOf(view);
        var info = Teams?.GetSlotInfo(CurrentTeam, i, SlotInfoBox.AllowBattleTeamWrites);
        return info ?? GetBlankInfo(i);
    }

    private ISlotInfo GetBlankInfo(int i) => new SlotInfoMisc(new byte[SAV.SIZE_STORED], i) { Type = StorageSlotType.BattleBox }; // unassigned: not writable

    public int GetViewIndex(ISlotInfo slot)
    {
        var teams = Teams;
        if (teams is null)
            return -1;
        if (teams.HasIndexedTeams)
        {
            if (slot is not SlotInfoBox b)
                return -1;
            int index = (b.Box * SAV.BoxSlotCount) + b.Slot;
            for (int i = 0; i < BattleTeamEditor.SlotsPerTeam; i++)
            {
                if (teams.GetTeamSlotIndex(CurrentTeam, i) == index)
                    return i;
            }
            return -1;
        }
        if (slot is SlotInfoMisc { Type: StorageSlotType.BattleBox } m && (uint)m.Slot < BattleTeamEditor.SlotsPerTeam)
            return m.Slot;
        return -1;
    }

    public void NotifySlotOld(ISlotInfo previous)
    {
        int index = GetViewIndex(previous);
        if (index < 0)
            return;
        SlotPictureBoxes[index].BackgroundImage = null;
    }

    public void NotifySlotChanged(ISlotInfo slot, SlotTouchType type, PKM pk)
    {
        int index = GetViewIndex(slot);
        if (index < 0)
            return;
        var pb = SlotPictureBoxes[index];
        SlotUtil.UpdateSlot(pb, slot, pk, SAV, GetFlags(pk), type);
    }

    public void ApplyNewFilter(Func<PKM, bool>? filter, bool reload = true)
    {
        if (filter == _searchFilter)
            return;
        _searchFilter = filter;
        if (reload)
            ResetSlots();
    }

    private SlotVisibilityType GetFlags(PKM pk)
    {
        var result = SlotVisibilityType.None;
        if (M?.SE.FlagIllegal == true)
            result |= SlotVisibilityType.CheckLegalityIndicate;
        if (_searchFilter != null && !_searchFilter(pk))
            result |= SlotVisibilityType.FilterMismatch;
        return result;
    }

    public void ResetSlots()
    {
        var teams = Teams;
        if (teams is null)
            return;

        _updatingLock = true;
        CHK_Locked.Checked = teams.GetIsLocked(CurrentTeam);
        _updatingLock = false;

        M?.Hover.Stop();
        for (int i = 0; i < TeamPokeGrid.Entries.Count; i++)
        {
            var pb = TeamPokeGrid.Entries[i];
            var info = teams.GetSlotInfo(CurrentTeam, i, SlotInfoBox.AllowBattleTeamWrites) ?? GetBlankInfo(i);
            var pk = info.Read(SAV);
            SlotUtil.UpdateSlot(pb, info, pk, SAV, GetFlags(pk));
        }
    }
}
