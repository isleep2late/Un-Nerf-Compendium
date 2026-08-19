using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms;

/// PKHaX: edits the battle-volatile type bytes of a Gen 3/4/5 battle inside a save state.
public sealed class SAV_RamBattleTypes : Form
{
    private readonly SaveStateAnalysis Analysis;
    private readonly RamBattle Battle;
    private readonly ComboBox[,] Boxes;

    public SAV_RamBattleTypes(SaveStateAnalysis analysis, RamBattle battle)
    {
        Analysis = analysis;
        Battle = battle;
        Text = $"Battle Typing — Gen {battle.Generation}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;

        int rows = 0;
        Boxes = new ComboBox[battle.Battlers, 2];
        for (int battler = 0; battler < battle.Battlers; battler++)
        {
            if (!battle.IsPresent(battler))
                continue;
            string who = battler switch
            {
                0 => "You",
                1 => "Foe",
                2 => "Ally",
                _ => "Foe 2",
            };
            var (t1, t2) = battle.GetTypes(battler);
            var label = new Label { Text = $"{who} (#{battle.GetSpecies(battler)})", AutoSize = true };
            label.SetBounds(12, 16 + (rows * 30), 110, 20);
            Controls.Add(label);
            for (int half = 0; half < 2; half++)
            {
                var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
                cb.SetBounds(130 + (half * 130), 12 + (rows * 30), 122, 22);
                foreach (var (value, name) in RamBattle.TypeNames)
                    cb.Items.Add(new TypeItem(value, name));
                Select(cb, half == 0 ? t1 : t2);
                Controls.Add(cb);
                Boxes[battler, half] = cb;
            }
            rows++;
        }

        ClientSize = new Size(400, 56 + (rows * 30));
        var save = new Button { Text = "Save" };
        save.SetBounds(216, ClientSize.Height - 36, 80, 26);
        save.Click += (_, _) => SaveChanges();
        Controls.Add(save);
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(304, ClientSize.Height - 36, 80, 26);
        Controls.Add(cancel);
        CancelButton = cancel;
    }

    private static void Select(ComboBox cb, byte value)
    {
        for (int i = 0; i < cb.Items.Count; i++)
        {
            if (((TypeItem)cb.Items[i]!).Value == value)
            {
                cb.SelectedIndex = i;
                return;
            }
        }
        cb.Items.Add(new TypeItem(value, $"0x{value:X2}"));
        cb.SelectedIndex = cb.Items.Count - 1;
    }

    private void SaveChanges()
    {
        for (int battler = 0; battler < Battle.Battlers; battler++)
        {
            if (Boxes[battler, 0] is not { } cb1 || Boxes[battler, 1] is not { } cb2)
                continue;
            Battle.SetTypes(battler, ((TypeItem)cb1.SelectedItem!).Value, ((TypeItem)cb2.SelectedItem!).Value);
        }
        var output = Analysis.Serialize();
        var target = Analysis.Container.FilePath;
        if (File.Exists(target) && !File.Exists(target + ".bak"))
            File.Copy(target, target + ".bak");
        File.WriteAllBytes(target, output);
        WinFormsUtil.Alert("Save state updated.", target);
        DialogResult = DialogResult.OK;
        Close();
    }

    private sealed record TypeItem(byte Value, string Name)
    {
        public override string ToString() => Name;
    }
}
