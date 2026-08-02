// PKHaX: manager window for Battle Teams (Gen 7) / the Battle Box (Gen 6): assign, clear, and lock/unlock.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using PKHeX.WinForms.Controls;

namespace PKHeX.WinForms;

/// <summary>
/// Lists every battle team with its six slots (sprite + name) and supports assigning the last viewed
/// Box Pokémon to a slot, clearing slots/teams, and toggling the team lock.
/// </summary>
public sealed class SAV_BattleTeamManager : Form, ISlotViewer<PictureBox>
{
    private readonly SAVEditor SE;
    private readonly SlotChangeManager Manager;
    private readonly BattleTeamEditor Teams;

    private readonly List<PictureBox> Slots = [];
    private readonly List<Label> Names = [];
    private readonly List<CheckBox> Locks = [];
    private readonly ContextMenuStrip mnuSlot = new();
    private PictureBox? _menuTarget; // captured at menu open; SourceControl can be null once a click handler runs
    private bool _updatingLocks;

    /// <summary> Raised after any team composition or lock change so other views can refresh. </summary>
    public event EventHandler? TeamsChanged;

    public SaveFile SAV => SE.SAV;
    public IList<PictureBox> SlotPictureBoxes => Slots;
    public int ViewIndex => -5;

    public SAV_BattleTeamManager(SAVEditor se, SlotChangeManager m, BattleTeamEditor teams)
    {
        SE = se;
        Manager = m;
        Teams = teams;

        SuspendLayout();
        Text = teams.HasIndexedTeams ? "Battle Team Manager" : "Battle Box Manager";
        Icon = Properties.Resources.Icon;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        var mnuAssign = new ToolStripMenuItem(teams.HasIndexedTeams
            ? "Assign Last Viewed Box Pokémon"
            : "Assign Last Viewed Pokémon (copy)");
        mnuAssign.Click += (_, _) => AssignFromMenu();
        var mnuClear = new ToolStripMenuItem("Clear Slot");
        mnuClear.Click += (_, _) => ClearFromMenu();
        mnuSlot.Items.Add(mnuAssign);
        mnuSlot.Items.Add(mnuClear);
        mnuSlot.Opening += (_, _) => _menuTarget = mnuSlot.SourceControl as PictureBox;

        var flp = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8),
            Name = "FLP_Teams",
        };

        int spriteW = SpriteUtil.Spriter.Width;
        int spriteH = SpriteUtil.Spriter.Height;
        var nameFont = new Font(Font.FontFamily, 7f);

        for (int t = 0; t < teams.TeamCount; t++)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Location = new Point(8, 18),
                Name = $"FLP_Team{t}",
            };

            for (int s = 0; s < BattleTeamEditor.SlotsPerTeam; s++)
            {
                var pb = PokeGrid.GetControl(spriteW, spriteH, $"Team {t + 1} Slot {s + 1}");
                pb.Location = new Point(0, 0);
                pb.Tag = (t, s);
                pb.ContextMenuStrip = mnuSlot;
                Slots.Add(pb);

                var lbl = new Label
                {
                    Location = new Point(0, pb.Bottom),
                    Size = new Size(pb.Width, 15),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = nameFont,
                    AutoEllipsis = true,
                    Name = $"L_Team{t}Slot{s}",
                };
                Names.Add(lbl);

                var cell = new Panel { Size = new Size(pb.Width, pb.Height + 16), Margin = new Padding(2) };
                cell.Controls.Add(pb);
                cell.Controls.Add(lbl);
                row.Controls.Add(cell);
            }

            var side = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(8, 2, 2, 2),
                Name = $"FLP_Team{t}Side",
            };
            var chk = new CheckBox { Text = "Locked", AutoSize = true, Tag = t, Name = $"CHK_Team{t}Lock" };
            chk.CheckedChanged += (sender, _) => ToggleLock((CheckBox)sender!);
            Locks.Add(chk);
            var clear = new Button { Text = "Clear Team", AutoSize = true, Tag = t, Name = $"B_Team{t}Clear" };
            clear.Click += (sender, _) => ClearTeam((int)((Button)sender!).Tag!);
            if (Application.IsDarkModeEnabled)
                WinFormsTranslator.ReformatDark(clear);
            side.Controls.Add(chk);
            side.Controls.Add(clear);
            row.Controls.Add(side);

            var gb = new GroupBox
            {
                Text = teams.GetTeamName(t),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(2),
                Name = $"GB_Team{t}",
            };
            gb.Controls.Add(row);
            flp.Controls.Add(gb);
        }

        var hint = new Label
        {
            Text = "View a Pokémon (Ctrl+Click it in a Box), then right-click a team slot to assign it.",
            AutoSize = true,
            Margin = new Padding(4),
            Name = "L_Hint",
        };
        flp.Controls.Add(hint);

        Controls.Add(flp);
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(flp.PreferredSize.Width + 24, Math.Min(flp.PreferredSize.Height + 24, 640));
        MinimumSize = new Size(Width, Math.Min(Height, 300));
        ResumeLayout(false);

        RefreshCells();

        SE.EditEnv.Slots.Publisher.Subscribe(this);
        Activated += (_, _) => RefreshCells(); // stay current when edits happen in other windows
        FormClosing += (_, _) => SE.EditEnv.Slots.Publisher.Unsubscribe(this);
        FormClosed += (_, _) =>
        {
            mnuSlot.Dispose();
            nameFont.Dispose();
        };
    }

    private (int Team, int Slot) GetMenuTarget()
    {
        if (_menuTarget is { Tag: (int t, int s) })
            return (t, s);
        return (-1, -1);
    }

    private void AssignFromMenu()
    {
        var (team, slot) = GetMenuTarget();
        if (team < 0)
            return;

        var prev = SE.EditEnv.Slots.Publisher.Previous;
        if (Teams.HasIndexedTeams)
        {
            if (prev is not SlotInfoBox b)
            {
                WinFormsUtil.Alert("No Box Pokémon selected.", "View one first (Ctrl+Click it in a Box).");
                return;
            }
            var pk = b.Read(SAV);
            if (pk.Species == 0)
            {
                WinFormsUtil.Alert("The last viewed Box slot is empty.");
                return;
            }
            int index = (b.Box * SAV.BoxSlotCount) + b.Slot;
            if (!Teams.SetTeamSlotIndex(team, slot, index))
            {
                WinFormsUtil.Alert("That Box slot is already used by this team.");
                return;
            }
        }
        else
        {
            if (prev is null)
            {
                WinFormsUtil.Alert("No Pokémon selected.", "View one first (Ctrl+Click it in a Box or the Party).");
                return;
            }
            var pk = prev.Read(SAV);
            if (pk.Species == 0)
            {
                WinFormsUtil.Alert("The last viewed slot is empty.");
                return;
            }
            var info = Teams.GetSlotInfo(team, slot, writable: true);
            if (info is null || SE.EditEnv.Slots.Set(info, pk) != SlotTouchResult.Success)
            {
                WinFormsUtil.Alert("Unable to write to the Battle Box slot.");
                return;
            }
        }
        OnTeamsChanged();
    }

    private void ClearFromMenu()
    {
        var (team, slot) = GetMenuTarget();
        if (team < 0)
            return;
        ClearSlot(team, slot);
        OnTeamsChanged();
    }

    private void ClearSlot(int team, int slot)
    {
        if (Teams.HasIndexedTeams)
        {
            Teams.ClearTeamSlot(team, slot);
            return;
        }
        var info = Teams.GetSlotInfo(team, slot, writable: true);
        if (info is not null)
            SE.EditEnv.Slots.Delete(info);
    }

    private void ClearTeam(int team)
    {
        for (int s = 0; s < BattleTeamEditor.SlotsPerTeam; s++)
            ClearSlot(team, s);
        OnTeamsChanged();
    }

    private void ToggleLock(CheckBox chk)
    {
        if (_updatingLocks)
            return;
        Teams.SetIsLocked((int)chk.Tag!, chk.Checked);
        OnTeamsChanged();
    }

    private void OnTeamsChanged()
    {
        RefreshCells();
        SE.UpdateBoxViewers(all: true); // refresh team/padlock overlays on box slots
        TeamsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshCells()
    {
        _updatingLocks = true;
        for (int t = 0; t < Teams.TeamCount; t++)
            Locks[t].Checked = Teams.GetIsLocked(t);
        _updatingLocks = false;

        for (int t = 0; t < Teams.TeamCount; t++)
        {
            for (int s = 0; s < BattleTeamEditor.SlotsPerTeam; s++)
                RefreshCell(t, s);
        }
    }

    private void RefreshCell(int team, int slot)
    {
        int flat = (team * BattleTeamEditor.SlotsPerTeam) + slot;
        var pb = Slots[flat];
        var lbl = Names[flat];
        var info = Teams.GetSlotInfo(team, slot, writable: false);
        if (info is null) // unassigned team slot
        {
            pb.Image = null;
            pb.BackColor = SlotUtil.GoodDataColor;
            lbl.Text = "—";
            return;
        }

        var pk = info.Read(SAV);
        SlotUtil.UpdateSlot(pb, info, pk, SAV, SlotVisibilityType.None);
        if (pk.Species == 0)
        {
            lbl.Text = info is SlotInfoBox bEmpty ? $"{bEmpty.Box + 1}/{bEmpty.Slot + 1}: —" : "—";
            return;
        }
        lbl.Text = info is SlotInfoBox b ? $"{b.Box + 1}/{b.Slot + 1}: {pk.Nickname}" : pk.Nickname;
    }

    // ISlotViewer: keep cells in sync when the underlying slots change in any other view.
    public ISlotInfo GetSlotData(PictureBox view)
    {
        if (view.Tag is (int t, int s) && Teams.GetSlotInfo(t, s, writable: false) is { } info)
            return info;
        return new SlotInfoMisc(new byte[SAV.SIZE_STORED], 0) { Type = StorageSlotType.BattleBox };
    }

    public int GetViewIndex(ISlotInfo slot)
    {
        if (Teams.HasIndexedTeams)
        {
            if (slot is not SlotInfoBox b)
                return -1;
            int index = (b.Box * SAV.BoxSlotCount) + b.Slot;
            for (int t = 0; t < Teams.TeamCount; t++)
            {
                for (int s = 0; s < BattleTeamEditor.SlotsPerTeam; s++)
                {
                    if (Teams.GetTeamSlotIndex(t, s) == index)
                        return (t * BattleTeamEditor.SlotsPerTeam) + s;
                }
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
        Slots[index].BackgroundImage = null;
    }

    public void NotifySlotChanged(ISlotInfo slot, SlotTouchType type, PKM pk)
    {
        if (GetViewIndex(slot) < 0)
            return;
        RefreshCells(); // a referenced slot changed; repaint all cells (handles duplicates across teams)
    }

    public void ApplyNewFilter(Func<PKM, bool>? filter, bool reload = true)
    {
        // Filters are not applicable to this manager view.
    }
}
