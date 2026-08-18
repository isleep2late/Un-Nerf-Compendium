using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms;

/// PKHaX: party editor for Space World '97 emulator save states and battery files.
public sealed class SAV_SpaceWorld : Form
{
    private readonly SW97Save Save;
    private int CurrentSlot = -1;
    private bool Loading;

    private readonly ListBox LB_Party = new();
    private readonly ComboBox CB_Species = new();
    private readonly CheckBox CHK_Disguise = new();
    private readonly NumericUpDown NUD_Level = new();
    private readonly TextBox TB_DVs = new();
    private readonly TextBox TB_Nickname = new();
    private readonly TextBox TB_OT = new();
    private readonly NumericUpDown NUD_PPUps = new();
    private readonly ComboBox[] CB_Moves = [new(), new(), new(), new()];
    private readonly Label L_Stats = new();
    private readonly Label L_Info = new();
    private readonly GroupBox GB_Battle = new();
    private readonly ComboBox CB_Type1 = new();
    private readonly ComboBox CB_Type2 = new();
    private readonly ComboBox CB_EnemyType1 = new();
    private readonly ComboBox CB_EnemyType2 = new();

    public SAV_SpaceWorld(SW97Save save)
    {
        Save = save;
        BuildLayout();
        PopulateSources();
        RefreshPartyList();
        if (Save.PartyCount != 0)
            LB_Party.SelectedIndex = 0;
    }

    private void BuildLayout()
    {
        Text = $"SpaceWorld '97 Editor — {System.IO.Path.GetFileName(Save.FilePath)}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(620, 470);

        LB_Party.SetBounds(12, 12, 170, 120);
        LB_Party.SelectedIndexChanged += (_, _) => ChangeSlot(LB_Party.SelectedIndex);
        Controls.Add(LB_Party);

        L_Info.SetBounds(12, 138, 170, 90);
        L_Info.AutoSize = false;
        Controls.Add(L_Info);

        var addSlot = new Button { Text = "Add slot" };
        addSlot.SetBounds(12, 232, 80, 26);
        addSlot.Click += (_, _) => AddSlot();
        Controls.Add(addSlot);

        var recalc = new Button { Text = "Recalc stats" };
        recalc.SetBounds(98, 232, 84, 26);
        recalc.Click += (_, _) => RecalculateStats();
        Controls.Add(recalc);

        int x = 200, y = 12;
        Controls.Add(MakeLabel("Species", x, y + 4));
        CB_Species.SetBounds(x + 70, y, 180, 22);
        CB_Species.DropDownStyle = ComboBoxStyle.DropDownList;
        CB_Species.SelectedIndexChanged += (_, _) => SpeciesChanged();
        Controls.Add(CB_Species);

        CHK_Disguise.Text = "Disguise (keep stored stats)";
        CHK_Disguise.SetBounds(x + 70, y + 26, 220, 20);
        CHK_Disguise.Checked = true;
        Controls.Add(CHK_Disguise);

        y += 54;
        Controls.Add(MakeLabel("Level", x, y + 4));
        NUD_Level.SetBounds(x + 70, y, 60, 22);
        NUD_Level.Minimum = 1;
        NUD_Level.Maximum = 255;
        Controls.Add(NUD_Level);

        Controls.Add(MakeLabel("DVs", x + 145, y + 4));
        TB_DVs.SetBounds(x + 185, y, 60, 22);
        TB_DVs.MaxLength = 4;
        Controls.Add(TB_DVs);

        var maxAll = new Button { Text = "Max DVs + stat exp" };
        maxAll.SetBounds(x + 250, y - 1, 130, 24);
        maxAll.Click += (_, _) => MaxOut();
        Controls.Add(maxAll);

        y += 30;
        Controls.Add(MakeLabel("Nickname", x, y + 4));
        TB_Nickname.SetBounds(x + 70, y, 100, 22);
        Controls.Add(TB_Nickname);
        Controls.Add(MakeLabel("OT", x + 180, y + 4));
        TB_OT.SetBounds(x + 210, y, 100, 22);
        Controls.Add(TB_OT);

        y += 32;
        Controls.Add(MakeLabel("Moves", x, y + 4));
        for (int i = 0; i < CB_Moves.Length; i++)
        {
            CB_Moves[i].SetBounds(x + 70, y + (i * 26), 180, 22);
            CB_Moves[i].DropDownStyle = ComboBoxStyle.DropDownList;
            Controls.Add(CB_Moves[i]);
        }
        Controls.Add(MakeLabel("PP Ups", x + 260, y + 4));
        NUD_PPUps.SetBounds(x + 320, y, 50, 22);
        NUD_PPUps.Maximum = 3;
        Controls.Add(NUD_PPUps);

        y += 4 * 26 + 8;
        L_Stats.SetBounds(x, y, 400, 20);
        Controls.Add(L_Stats);

        GB_Battle.Text = "Battle types (volatile — re-derived on every send-out)";
        GB_Battle.SetBounds(12, 300, 596, 90);
        Controls.Add(GB_Battle);

        GB_Battle.Controls.Add(MakeLabel("Your active", 12, 26));
        CB_Type1.SetBounds(90, 22, 120, 22);
        CB_Type2.SetBounds(216, 22, 120, 22);
        GB_Battle.Controls.Add(MakeLabel("Opponent", 12, 56));
        CB_EnemyType1.SetBounds(90, 52, 120, 22);
        CB_EnemyType2.SetBounds(216, 52, 120, 22);
        foreach (var cb in new[] { CB_Type1, CB_Type2, CB_EnemyType1, CB_EnemyType2 })
        {
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            GB_Battle.Controls.Add(cb);
        }

        var save = new Button { Text = "Save", DialogResult = DialogResult.OK };
        save.SetBounds(430, 410, 84, 30);
        save.Click += (_, _) => Persist();
        Controls.Add(save);

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(524, 410, 84, 30);
        Controls.Add(cancel);
        CancelButton = cancel;
    }

    private static Label MakeLabel(string text, int x, int y)
    {
        var label = new Label { Text = text, AutoSize = true };
        label.SetBounds(x, y, 60, 18);
        return label;
    }

    private void PopulateSources()
    {
        var species = new List<ComboItem>();
        for (int i = 1; i <= SW97Data.MaxSpeciesID; i++)
            species.Add(new ComboItem($"{i:000} {SW97Data.GetSpeciesName(i)}", i));
        CB_Species.DisplayMember = nameof(ComboItem.Text);
        CB_Species.ValueMember = nameof(ComboItem.Value);
        CB_Species.DataSource = species;

        for (int i = 0; i < CB_Moves.Length; i++)
        {
            var moves = new List<ComboItem> { new("(none)", 0) };
            for (int m = 1; m <= SW97Data.MaxMoveID; m++)
                moves.Add(new ComboItem(SW97Data.GetMoveName(m), m));
            CB_Moves[i].DisplayMember = nameof(ComboItem.Text);
            CB_Moves[i].ValueMember = nameof(ComboItem.Value);
            CB_Moves[i].DataSource = moves;
        }

        foreach (var cb in new[] { CB_Type1, CB_Type2, CB_EnemyType1, CB_EnemyType2 })
        {
            var types = new List<ComboItem>();
            for (int t = 0; t < SW97Data.TypeNames.Length; t++)
                types.Add(new ComboItem($"{t:X2} {SW97Data.GetTypeName(t)}", t));
            cb.DisplayMember = nameof(ComboItem.Text);
            cb.ValueMember = nameof(ComboItem.Value);
            cb.DataSource = types;
        }

        GB_Battle.Enabled = Save.IsBattleActive;
        if (Save.IsBattleActive)
        {
            var player = Save.GetBattleMon(false);
            var enemy = Save.GetBattleMon(true);
            CB_Type1.SelectedValue = (int)player[SW97Save.BattleType1];
            CB_Type2.SelectedValue = (int)player[SW97Save.BattleType2];
            CB_EnemyType1.SelectedValue = (int)enemy[SW97Save.BattleType1];
            CB_EnemyType2.SelectedValue = (int)enemy[SW97Save.BattleType2];
        }
        else
        {
            GB_Battle.Text = "Battle types — take the state during a battle to edit these";
        }
    }

    private void RefreshPartyList()
    {
        int selected = LB_Party.SelectedIndex;
        LB_Party.BeginUpdate();
        LB_Party.Items.Clear();
        for (int i = 0; i < Save.PartyCount; i++)
        {
            var mon = Save.GetMon(i);
            LB_Party.Items.Add($"{i + 1}. {SW97Data.GetSpeciesName(mon.Species)} L{mon.Level}");
        }
        LB_Party.EndUpdate();
        if (selected >= 0 && selected < LB_Party.Items.Count)
            LB_Party.SelectedIndex = selected;

        L_Info.Text = Save.IsBattery
            ? $"Battery file\nParty at 0x{Save.PartyOffset:X}\nChecksums rewritten on save.\nThe stock ROM never reads\nthis back — Continue is dead code."
            : $"Save state\nParty at 0x{Save.PartyOffset:X}\nWRAM $C000 at 0x{Save.WramBase:X}\nPlayer: {Save.PlayerName}";
    }

    private void ChangeSlot(int slot)
    {
        if (slot < 0 || slot >= Save.PartyCount)
            return;
        StoreCurrentSlot();
        CurrentSlot = slot;
        Loading = true;
        var mon = Save.GetMon(slot);
        CB_Species.SelectedValue = mon.Species;
        NUD_Level.Value = Math.Clamp(mon.Level, 1, 255);
        TB_DVs.Text = mon.DVs.ToString("X4");
        TB_Nickname.Text = mon.Nickname;
        TB_OT.Text = mon.OTName;
        NUD_PPUps.Value = mon.GetPPUps(0);
        for (int i = 0; i < CB_Moves.Length; i++)
            CB_Moves[i].SelectedValue = mon.GetMove(i);
        Loading = false;
        ShowStats(mon);
    }

    private void ShowStats(SW97Mon mon)
    {
        L_Stats.Text = $"HP {mon.MaxHP}   Atk {mon.GetStat(0)}   Def {mon.GetStat(1)}   " +
                       $"Spd {mon.GetStat(2)}   SpA {mon.GetStat(3)}   SpD {mon.GetStat(4)}   " +
                       $"[{SW97Data.GetTypeName(mon.Type1)}" +
                       (mon.Type1 == mon.Type2 ? "]" : $"/{SW97Data.GetTypeName(mon.Type2)}]");
    }

    private void SpeciesChanged()
    {
        if (Loading || CurrentSlot < 0)
            return;
        var mon = Save.GetMon(CurrentSlot);
        int species = (int)(CB_Species.SelectedValue ?? mon.Species);
        if (CHK_Disguise.Checked)
        {
            mon.ApplyDisguise(species, string.IsNullOrEmpty(TB_Nickname.Text) || TB_Nickname.Text == mon.Nickname);
            TB_Nickname.Text = mon.Nickname;
        }
        else
        {
            mon.Species = species;
            mon.ApplyCalculatedStats();
        }
        Save.SyncSpeciesList();
        RefreshPartyList();
        ShowStats(mon);
    }

    private void StoreCurrentSlot()
    {
        if (CurrentSlot < 0 || CurrentSlot >= Save.PartyCount)
            return;
        var mon = Save.GetMon(CurrentSlot);
        mon.Level = (int)NUD_Level.Value;
        if (int.TryParse(TB_DVs.Text, System.Globalization.NumberStyles.HexNumber, null, out int dv))
            mon.DVs = dv;
        int ppUps = (int)NUD_PPUps.Value;
        for (int i = 0; i < CB_Moves.Length; i++)
            mon.SetMove(i, (int)(CB_Moves[i].SelectedValue ?? 0), ppUps);
        if (TB_Nickname.Text.Length != 0 && !SW97Data.TryEncodeName(TB_Nickname.Text, mon.NicknameRaw))
            WinFormsUtil.Alert($"Nickname has characters the SpaceWorld charmap does not contain: {TB_Nickname.Text}");
        if (TB_OT.Text.Length != 0 && !SW97Data.TryEncodeName(TB_OT.Text, mon.OTNameRaw))
            WinFormsUtil.Alert($"OT name has characters the SpaceWorld charmap does not contain: {TB_OT.Text}");
    }

    private void RecalculateStats()
    {
        if (CurrentSlot < 0)
            return;
        StoreCurrentSlot();
        var mon = Save.GetMon(CurrentSlot);
        mon.ApplyCalculatedStats();
        ShowStats(mon);
        RefreshPartyList();
    }

    private void MaxOut()
    {
        if (CurrentSlot < 0)
            return;
        StoreCurrentSlot();
        var mon = Save.GetMon(CurrentSlot);
        mon.DVs = 0xFFFF;
        for (int i = 0; i < 5; i++)
            mon.SetStatExp(i, 0xFFFF);
        mon.ApplyCalculatedStats();
        TB_DVs.Text = "FFFF";
        ShowStats(mon);
        RefreshPartyList();
    }

    private void AddSlot()
    {
        if (Save.PartyCount >= SW97Save.PartyLength)
            return;
        StoreCurrentSlot();
        int slot = Save.PartyCount;
        Save.PartyCount = slot + 1;
        Save.InitializeSlot(slot);
        var mon = Save.GetMon(slot);
        mon.ApplyDisguise(mon.Species, true);
        mon.ApplyCalculatedStats();
        Save.SyncSpeciesList();
        RefreshPartyList();
        LB_Party.SelectedIndex = slot;
    }

    private void Persist()
    {
        StoreCurrentSlot();
        Save.SyncSpeciesList();
        if (GB_Battle.Enabled)
        {
            var player = Save.GetBattleMon(false);
            var enemy = Save.GetBattleMon(true);
            player[SW97Save.BattleType1] = (byte)(int)(CB_Type1.SelectedValue ?? 0);
            player[SW97Save.BattleType2] = (byte)(int)(CB_Type2.SelectedValue ?? 0);
            enemy[SW97Save.BattleType1] = (byte)(int)(CB_EnemyType1.SelectedValue ?? 0);
            enemy[SW97Save.BattleType2] = (byte)(int)(CB_EnemyType2.SelectedValue ?? 0);
        }
        try
        {
            Save.Export(Save.FilePath);
            WinFormsUtil.Alert($"Saved {System.IO.Path.GetFileName(Save.FilePath)}." +
                               (System.IO.File.Exists(Save.FilePath + ".bak") ? " Original kept as .bak." : string.Empty));
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Could not write the file.", ex.Message);
        }
    }
}
