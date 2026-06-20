using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

/// <summary>
/// PKHaX: Gen-1/2 hackmons editor. For Gen 1 (PK1) it surfaces the PikaSav-style desynced "sprite"
/// species (the box/party list header byte, stored separately from the data-structure species) and
/// arbitrary <see cref="PK1.Type1"/>/<see cref="PK1.Type2"/> (mono = equal, dual = different).
/// For both Gen 1 and Gen 2 it exposes a Status Condition dropdown (Gen 2 has no disguise/custom typing).
/// Built entirely in code so it needs no .Designer wiring.
/// </summary>
public sealed class G1Editor : UserControl
{
    // Gen-1 type byte values are NOT PKHeX's modern type indices, so enumerate them explicitly.
    private static readonly (byte Value, string Name)[] G1Types =
    [
        (0, "Normal"), (1, "Fighting"), (2, "Flying"), (3, "Poison"), (4, "Ground"), (5, "Rock"),
        (7, "Bug"), (8, "Ghost"), (20, "Fire"), (21, "Water"), (22, "Grass"), (23, "Electric"),
        (24, "Psychic"), (25, "Ice"), (26, "Dragon"),
    ];

    // Status Condition byte values (shared Gen 1-4 layout). Sleep uses a non-zero counter; the game just needs "asleep".
    private static readonly (byte Value, string Name)[] Statuses =
    [
        (0, "None"),
        ((byte)StatusCondition.Sleep2, "Sleep"),
        ((byte)StatusCondition.Poison, "Poison"),
        ((byte)StatusCondition.Burn, "Burn"),
        ((byte)StatusCondition.Freeze, "Freeze"),
        ((byte)StatusCondition.Paralysis, "Paralysis"),
    ];

    private readonly ComboBox CB_Sprite = new();
    private readonly ComboBox CB_Type1 = new();
    private readonly ComboBox CB_Type2 = new();
    private readonly ComboBox CB_Status = new();
    private readonly Label L_Sprite = new();
    private readonly Label L_Type1 = new();
    private readonly Label L_Type2 = new();
    private readonly Label L_Status = new();
    private readonly Label L_Mono = new();
    private GBPKM? Entity;
    private bool Loading;

    public G1Editor()
    {
        var tlp = new TableLayoutPanel
        {
            ColumnCount = 2, RowCount = 5,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill, Margin = Padding.Empty,
        };
        foreach (var cb in new[] { CB_Sprite, CB_Type1, CB_Type2, CB_Status })
        {
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.Width = 150; cb.DropDownWidth = 210;
            cb.Margin = new Padding(0, 1, 0, 1);
            cb.IntegralHeight = false;
        }
        AddRow(tlp, 0, L_Sprite, "Sprite:", CB_Sprite);
        AddRow(tlp, 1, L_Type1, "Type 1:", CB_Type1);
        AddRow(tlp, 2, L_Type2, "Type 2:", CB_Type2);
        L_Mono.AutoSize = true; L_Mono.Margin = new Padding(0, 3, 0, 0); L_Mono.ForeColor = Color.Gray;
        tlp.Controls.Add(L_Mono, 1, 3);
        AddRow(tlp, 4, L_Status, "Status:", CB_Status);

        Controls.Add(tlp);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink; Margin = Padding.Empty;

        PopulateSprite();
        PopulateType(CB_Type1);
        PopulateType(CB_Type2);
        PopulateStatus();

        CB_Sprite.SelectedIndexChanged += Changed;
        CB_Type1.SelectedIndexChanged += Changed;
        CB_Type2.SelectedIndexChanged += Changed;
        CB_Status.SelectedIndexChanged += Changed;
    }

    private static void AddRow(TableLayoutPanel tlp, int row, Label lbl, string text, Control c)
    {
        lbl.Text = text; lbl.AutoSize = true; lbl.Anchor = AnchorStyles.Left; lbl.Margin = new Padding(0, 5, 4, 0);
        tlp.Controls.Add(lbl, 0, row);
        tlp.Controls.Add(c, 1, row);
    }

    private void PopulateSprite()
    {
        var names = GameInfo.Strings.specieslist;
        var list = new List<ComboItem>(256);
        for (int i = 0; i < 256; i++)
        {
            var nat = SpeciesConverter.GetNational1((byte)i);
            string label = nat is >= 1 and <= 151 && nat < names.Length
                ? $"{i:000}  {names[nat]}"
                : $"{i:000}  (glitch)";
            list.Add(new ComboItem(label, i));
        }
        Bind(CB_Sprite, list);
    }

    private static void PopulateType(ComboBox cb)
    {
        var list = new List<ComboItem>(G1Types.Length);
        foreach (var (v, n) in G1Types)
            list.Add(new ComboItem(n, v));
        Bind(cb, list);
    }

    private void PopulateStatus()
    {
        var list = new List<ComboItem>(Statuses.Length);
        foreach (var (v, n) in Statuses)
            list.Add(new ComboItem(n, v));
        Bind(CB_Status, list);
    }

    private static void Bind(ComboBox cb, List<ComboItem> list)
    {
        cb.DataSource = null;
        cb.DisplayMember = nameof(ComboItem.Text);
        cb.ValueMember = nameof(ComboItem.Value);
        cb.DataSource = list;
    }

    private static void SelectValue(ComboBox cb, int value)
    {
        if (cb.DataSource is not List<ComboItem> list)
            return;
        int idx = list.FindIndex(z => z.Value == value);
        if (idx < 0) // unknown/glitch byte: append so it still round-trips faithfully
        {
            list.Add(new ComboItem($"0x{value:X2} (raw)", value));
            Bind(cb, list);
            idx = list.Count - 1;
        }
        cb.SelectedIndex = idx;
    }

    private static int GetValue(ComboBox cb) => (cb.SelectedItem as ComboItem)?.Value ?? 0;

    /// <summary>Shows the Gen-1-only sprite/type rows (Gen 2 keeps just the Status row).</summary>
    public void SetGen1Mode(bool isGen1)
    {
        L_Sprite.Visible = CB_Sprite.Visible = isGen1;
        L_Type1.Visible = CB_Type1.Visible = isGen1;
        L_Type2.Visible = CB_Type2.Visible = isGen1;
        L_Mono.Visible = isGen1;
    }

    public void LoadEntity(GBPKM pk)
    {
        Loading = true;
        Entity = pk;
        if (pk is PK1 p1)
        {
            SelectValue(CB_Sprite, p1.SpriteSpeciesInternal);
            SelectValue(CB_Type1, p1.Type1);
            SelectValue(CB_Type2, p1.Type2);
        }
        SelectStatus(pk.Status_Condition);
        Loading = false;
        UpdateMono();
    }

    public void SaveEntity(GBPKM pk)
    {
        if (pk is PK1 p1)
        {
            var sprite = (byte)GetValue(CB_Sprite);
            // Only record a header override when it actually differs from the data species.
            p1.HeaderSpeciesInternal = sprite == p1.SpeciesInternal ? (byte)0 : sprite;
            p1.Type1 = (byte)GetValue(CB_Type1);
            p1.Type2 = (byte)GetValue(CB_Type2);
        }
        pk.Status_Condition = (byte)GetValue(CB_Status);
    }

    private void SelectStatus(int statusByte)
    {
        var word = GBHaxFormat.GetStatusWord(statusByte);
        byte value = 0;
        if (word.Length != 0)
        {
            foreach (var (v, n) in Statuses)
            {
                if (n == word)
                {
                    value = v;
                    break;
                }
            }
        }
        SelectValue(CB_Status, value);
    }

    private void Changed(object? sender, EventArgs e)
    {
        UpdateMono();
        if (Loading || Entity is null)
            return;
        SaveEntity(Entity); // live-apply so edits stick even without re-saving the slot
    }

    private void UpdateMono()
    {
        L_Mono.Text = GetValue(CB_Type1) == GetValue(CB_Type2) ? "mono-type" : "dual-type";
    }
}
