// PKHaX: pop-out viewer showing Party + Battle Teams + PC side by side with cross-window drag & drop.
using System;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;
using PKHeX.WinForms.Controls;

namespace PKHeX.WinForms;

/// <summary>
/// Pop-out window displaying the Party, the Battle Teams (Gen 6/7), and a PC Box editor side by side.
/// All three regions participate in the shared <see cref="SlotChangeManager"/> so slots can be dragged between
/// this window, the main window, and any other pop-out viewers.
/// </summary>
public sealed class SAV_TeamPCViewer : Form
{
    private readonly SAVEditor parent;
    private readonly SlotChangeManager Manager;
    private readonly BoxEditor Box;
    private readonly PartyEditor Party;
    private readonly TeamSlotsViewer TeamView;
    private readonly BattleTeamEditor? Teams;
    private SAV_BattleTeamManager? TeamManager;

    public SAV_TeamPCViewer(SAVEditor p, SlotChangeManager m)
    {
        parent = p;
        Manager = m;
        var sav = p.SAV;
        Teams = BattleTeamEditor.TryCreate(sav);
        var mnu = p.SlotPictureBoxes[0].ContextMenuStrip;

        SuspendLayout();
        Text = "Team + PC Viewer";
        Icon = Properties.Resources.Icon;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoScroll = true;

        const int pad = 8;

        // Left column: Party (+ Battle Teams when supported)
        var lParty = new Label { Text = "Party", AutoSize = true, Location = new Point(pad, pad), Name = "L_Party" };
        Party = new PartyEditor { Name = "TPV_Party", Location = new Point(pad, lParty.Bottom + 2) };
        Party.Setup(m);
        Party.InitializeFromSAV(sav);
        foreach (var pb in Party.SlotPictureBoxes)
            pb.ContextMenuStrip = mnu;
        Party.Size = Party.PreferredSize;
        lParty.Visible = Party.Visible;

        var lTeams = new Label { Text = "Battle Teams", AutoSize = true, Location = new Point(pad, Party.Bottom + 10), Name = "L_Teams" };
        TeamView = new TeamSlotsViewer { Name = "TPV_Teams", Location = new Point(pad, lTeams.Bottom + 2) };
        var bManage = new Button { Text = "Manage Teams...", AutoSize = true, Name = "B_ManageTeams" };
        if (Teams is not null)
        {
            if (Teams.TeamCount == 1)
                lTeams.Text = "Battle Box";
            TeamView.Setup(m, Teams, mnu);
            TeamView.Size = TeamView.PreferredSize;
            bManage.Location = new Point(pad, TeamView.Bottom + 6);
            bManage.Click += (_, _) => OpenTeamManager();
        }
        else
        {
            lTeams.Visible = TeamView.Visible = bManage.Visible = false;
        }

        int leftWidth = Math.Max(Party.Width, Teams is not null ? Math.Max(TeamView.Width, bManage.Width) : 0);
        leftWidth = Math.Max(leftWidth, 140);
        int leftBottom = Teams is not null ? bManage.Bottom : Party.Bottom;

        // Right column: PC Box
        Box = new BoxEditor { Name = "TPV_Box", Editor = new BoxEdit(sav) };
        Box.Setup(m);
        Box.InitializeGrid();
        Box.Location = new Point(leftWidth + (2 * pad), pad);
        Box.Size = Box.PreferredSize;
        Box.RecenterControls();
        foreach (var pb in Box.SlotPictureBoxes)
            pb.ContextMenuStrip = mnu;

        Controls.Add(lParty);
        Controls.Add(Party);
        Controls.Add(lTeams);
        Controls.Add(TeamView);
        Controls.Add(bManage);
        Controls.Add(Box);

        if (Application.IsDarkModeEnabled)
        {
            WinFormsTranslator.ReformatDark(Box.B_BoxLeft);
            WinFormsTranslator.ReformatDark(Box.B_BoxRight);
            WinFormsTranslator.ReformatDark(Box.CB_BoxSelect);
            WinFormsTranslator.ReformatDark(bManage);
        }

        ClientSize = new Size(Box.Right + pad, Math.Max(Box.Bottom, leftBottom) + pad);
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = SizeFromClientSize(ClientSize);
        ResumeLayout(false);

        // Populate views
        Box.ResetBoxNames(p.CurrentBox);
        Box.ResetSlots();
        Party.ResetSlots();
        TeamView.ResetSlots();

        // Drag & drop plumbing (mirrors SAV_BoxViewer)
        AllowDrop = true;
        GiveFeedback += (_, e) => e.UseDefaultCursors = false;
        DragEnter += Main_DragEnter;
        DragDrop += (_, _) =>
        {
            Cursor = DefaultCursor;
            WinFormsUtil.Asterisk();
        };
        MouseWheel += (_, e) =>
        {
            if (parent.menu.mnuVSD.Visible)
                return;
            Box.CurrentBox = e.Delta > 1 ? Box.Editor.MoveLeft() : Box.Editor.MoveRight();
            m.MouseRestart();
        };

        Owner = p.ParentForm;
        CenterToParent();

        var publisher = p.EditEnv.Slots.Publisher;
        publisher.Subscribe(Box);
        publisher.Subscribe(Party);
        publisher.Subscribe(TeamView);
        FormClosing += (_, _) =>
        {
            Box.M?.Boxes.Remove(Box);
            publisher.Unsubscribe(Box);
            publisher.Unsubscribe(Party);
            publisher.Unsubscribe(TeamView);
        };
    }

    private void OpenTeamManager()
    {
        if (Teams is null)
            return;
        if (TeamManager is { IsDisposed: false })
        {
            TeamManager.BringToFront();
            return;
        }
        var form = new SAV_BattleTeamManager(parent, Manager, Teams) { Owner = this };
        form.TeamsChanged += (_, _) => TeamView.ResetSlots();
        form.FormClosed += (_, _) => TeamManager = null;
        TeamManager = form;
        form.Show();
    }

    private static void Main_DragEnter(object? sender, DragEventArgs? e)
    {
        if (e is null)
            return;
        if (e.AllowedEffect == (DragDropEffects.Copy | DragDropEffects.Link)) // external file
            e.Effect = DragDropEffects.Copy;
        else if (e.Data is not null) // within
            e.Effect = DragDropEffects.Move;
    }
}
