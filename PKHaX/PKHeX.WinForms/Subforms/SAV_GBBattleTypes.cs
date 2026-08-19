using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms;

/// PKHaX: edits the battle-volatile type bytes of the active Gen 1/2 battle inside a save state.
public sealed class SAV_GBBattleTypes : Form
{
    private static readonly (byte Value, string Name)[] Types =
    [
        (0, "Normal"), (1, "Fighting"), (2, "Flying"), (3, "Poison"), (4, "Ground"), (5, "Rock"),
        (7, "Bug"), (8, "Ghost"), (9, "Steel"), (20, "Fire"), (21, "Water"), (22, "Grass"),
        (23, "Electric"), (24, "Psychic"), (25, "Ice"), (26, "Dragon"), (27, "Dark"),
    ];

    private readonly GBRamGame Game;
    private readonly SaveStateAnalysis Analysis;
    private readonly ComboBox[] Boxes = [new(), new(), new(), new()];

    public SAV_GBBattleTypes(SaveStateAnalysis analysis, GBRamGame game)
    {
        Analysis = analysis;
        Game = game;
        Text = $"Battle Typing — {game.Layout.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(340, 170);

        string[] rows = ["Your Type 1", "Your Type 2", "Foe Type 1", "Foe Type 2"];
        for (int i = 0; i < 4; i++)
        {
            var label = new Label { Text = rows[i], AutoSize = true };
            label.SetBounds(12, 16 + (i * 30), 90, 20);
            Controls.Add(label);
            var cb = Boxes[i];
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.SetBounds(110, 12 + (i * 30), 150, 22);
            foreach (var (value, name) in Types)
                cb.Items.Add(new TypeItem(value, name));
            Controls.Add(cb);
        }
        var (p1, p2) = game.GetBattleTypes(false);
        var (e1, e2) = game.GetBattleTypes(true);
        Select(Boxes[0], p1);
        Select(Boxes[1], p2);
        Select(Boxes[2], e1);
        Select(Boxes[3], e2);

        var save = new Button { Text = "Save" };
        save.SetBounds(160, 134, 80, 26);
        save.Click += (_, _) => SaveChanges();
        Controls.Add(save);
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(248, 134, 80, 26);
        Controls.Add(cancel);
        CancelButton = cancel;
    }

    private static void Select(ComboBox cb, int value)
    {
        for (int i = 0; i < cb.Items.Count; i++)
        {
            if (((TypeItem)cb.Items[i]!).Value == value)
            {
                cb.SelectedIndex = i;
                return;
            }
        }
        cb.Items.Add(new TypeItem((byte)value, $"0x{value:X2}"));
        cb.SelectedIndex = cb.Items.Count - 1;
    }

    private void SaveChanges()
    {
        Game.SetBattleTypes(false, Value(0), Value(1));
        Game.SetBattleTypes(true, Value(2), Value(3));
        var output = Analysis.Serialize();
        var target = Analysis.Container.FilePath;
        if (File.Exists(target) && !File.Exists(target + ".bak"))
            File.Copy(target, target + ".bak");
        File.WriteAllBytes(target, output);
        WinFormsUtil.Alert("Save state updated.", target);
        DialogResult = DialogResult.OK;
        Close();
    }

    private byte Value(int index) => ((TypeItem)Boxes[index].SelectedItem!).Value;

    private sealed record TypeItem(byte Value, string Name)
    {
        public override string ToString() => Name;
    }
}
