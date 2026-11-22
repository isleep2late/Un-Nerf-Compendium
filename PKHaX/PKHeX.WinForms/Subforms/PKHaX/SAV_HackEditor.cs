using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Intake;
using PKHeX.Core;

namespace PKHeX.WinForms;

public sealed class SAV_HackEditor : Form
{
    private static readonly string[] StatLabels = ["HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed"];
    private static readonly Regex IdAtEnd = new(@"\(?#(\d+)\)?\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex IdAtStart = new(@"^#(\d+)", RegexOptions.CultureInvariant);

    private sealed record SlotRow(HackSlotRef Where, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record FieldRow(string Label, string Field, Control View, string Initial, Func<string> Current, Action<HackMonEditModel, string> Write);

    private readonly OpenResult result;
    private readonly HackSaveModel model;
    private readonly string savePath;
    private ReadOnlyMemory<byte> opened;
    private bool written;
    private bool refreshing;

    private readonly Label header;
    private readonly Label banner;
    private readonly ListBox slots;
    private readonly TextBox detail;
    private readonly TableLayoutPanel grid;
    private readonly Button apply;
    private readonly Panel editPanel;
    private readonly TextBox money;
    private readonly Button setMoney;
    private readonly Button save;
    private readonly Button gate;
    private readonly Button close;
    private readonly TextBox status;
    private readonly List<FieldRow> rows = [];

    public SAV_HackEditor(OpenResult result, string path, ReadOnlyMemory<byte> opened)
    {
        ArgumentNullException.ThrowIfNull(result);
        this.result = result;
        model = result.Native ?? throw new ArgumentException("the open result holds no hack save", nameof(result));
        savePath = path ?? "";
        this.opened = opened.ToArray();

        var kitName = result.Kit?.Kit.Name ?? model.Variant;
        Text = $"{kitName}: {model.Variant} (hack save)";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ClientSize = new Size(960, 680);
        MinimumSize = new Size(720, 480);

        header = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), MaximumSize = new Size(940, 0) };
        banner = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(6), MaximumSize = new Size(940, 0), ForeColor = Color.DarkRed };

        slots = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        detail = new TextBox { Dock = DockStyle.Top, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 210, Text = "Select a Pokemon on the left." };
        grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoScroll = true, Padding = new Padding(0, 6, 0, 0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        apply = new Button { Text = "Apply to this slot", Dock = DockStyle.Bottom, Height = 30 };
        editPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
        editPanel.Controls.Add(grid);
        editPanel.Controls.Add(apply);

        var right = new Panel { Dock = DockStyle.Fill };
        right.Controls.Add(editPanel);
        right.Controls.Add(detail);

        var split = new SplitContainer { Dock = DockStyle.Fill };
        split.Panel1.Controls.Add(slots);
        split.Panel2.Controls.Add(right);

        money = new TextBox { Width = 110 };
        setMoney = new Button { Text = "Set money", AutoSize = true };
        save = new Button { Text = "Save to file...", AutoSize = true };
        gate = new Button { Text = "Why read-only / write gate...", AutoSize = true };
        close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(4) };
        bottom.Controls.AddRange([new Label { Text = "Money:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, money, setMoney, save, gate, close]);

        status = new TextBox { Dock = DockStyle.Bottom, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 64 };

        Controls.Add(split);
        Controls.Add(banner);
        Controls.Add(header);
        Controls.Add(status);
        Controls.Add(bottom);
        CancelButton = close;

        slots.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshing && slots.SelectedItem is SlotRow row)
                ShowSlot(row.Where, null);
        };
        Shown += (_, _) => split.SplitterDistance = Math.Max(split.Panel1MinSize, Math.Min(360, split.Width / 2));
        apply.Click += (_, _) => ApplyEdit();
        setMoney.Click += (_, _) => SetMoney();
        save.Click += (_, _) => SaveToFile();
        gate.Click += (_, _) => ShowGate();
        FormClosing += (_, e) =>
        {
            if (!written && (model.Edits.Count > 0 || model.MoneyEdit is not null)
                && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "The applied changes are not written to the file.", "Close anyway?") != DialogResult.Yes)
                e.Cancel = true;
        };

        RefreshAll();
    }

    private string? FileRefusal => savePath.Length == 0 ? "this save was not opened from a file, so it has nowhere to be written" : null;

    private bool Writable => !written && FileRefusal is null && result.Gate is { Passed: true } && model.CanWrite && result.WriteRefusal is null;

    private string ReadOnlySentence => written
        ? "This save was written. Open it again to edit it further."
        : result.WriteRefusal ?? model.Banner ?? FileRefusal ?? (result.Gate is { Passed: true } ? "this save is read-only" : "this save was not admitted through the write gate");

    private void RefreshAll()
    {
        var lines = new List<string>
        {
            $"{model.Variant} (hack save){(result.Kit is { } kit ? $" of {kit.Kit.Name}" : "")}",
            $"Trainer: {model.TrainerLine}",
            $"Money: {model.MoneyLine}",
            $"Party: {model.Party.Count(s => s.Mon is not null)} | Boxes: {model.Boxes.Count} ({model.Boxes.Sum(b => b.Count)} Pokemon)",
        };
        if (model.BoxesReason is { } boxes)
            lines.Add("Boxes: " + boxes);
        if (model.NamesBanner is { } names)
            lines.Add(names);
        header.Text = string.Join(Environment.NewLine, lines);
        banner.Text = Writable
            ? "This save can be edited. Save to file writes it after a dated backup of the file beside it, and only if the self-check passes."
            : "Read-only: " + ReadOnlySentence;

        var keep = slots.SelectedIndex;
        refreshing = true;
        slots.BeginUpdate();
        slots.Items.Clear();
        foreach (var row in Rows())
            slots.Items.Add(row);
        slots.EndUpdate();
        slots.SelectedIndex = keep >= 0 && keep < slots.Items.Count ? keep : -1;
        refreshing = false;

        money.Enabled = setMoney.Enabled = Writable;
        save.Enabled = Writable;
        if (slots.SelectedItem is SlotRow selected)
            ShowSlot(selected.Where, null);
        else
            editPanel.Visible = false;
    }

    private List<SlotRow> Rows()
    {
        var list = new List<SlotRow>();
        foreach (var slot in model.Party.Where(s => !s.IsEmpty))
            list.Add(new SlotRow(slot.Where, $"Party {slot.Where.Slot + 1}: {slot.Title}"));
        foreach (var box in model.Boxes)
        {
            foreach (var slot in box.Slots.Where(s => s.Mon is not null))
                list.Add(new SlotRow(slot.Where, $"Box {box.Number}, slot {slot.Where.Slot + 1}: {slot.Title}"));
            var refused = box.Slots.Where(s => s.Mon is null && s.Refused is not null).ToList();
            if (refused.Count > 0)
                list.Add(new SlotRow(refused[0].Where, $"Box {box.Number}: {refused.Count} unreadable record{(refused.Count == 1 ? "" : "s")} (slot{(refused.Count == 1 ? "" : "s")} {string.Join(", ", refused.Select(s => s.Where.Slot + 1))})"));
        }
        return list;
    }

    private string Named(HackNameKind kind, int id)
    {
        var names = model.Names;
        if (!names.Has(kind))
            return id.ToString(CultureInfo.InvariantCulture);
        if (id == 0 && kind is HackNameKind.Move or HackNameKind.Item)
            return "none";
        return names.Shown(kind, id);
    }

    private static string NatureName(int nature)
    {
        var list = GameInfo.Strings.Natures;
        return nature >= 0 && nature < list.Count ? list[nature] : $"#{nature}";
    }

    private string Detail(HackSlotRef where)
    {
        if (model.Slot(where) is not { } slot)
            return $"{where}: no such slot in this save.";
        if (slot.Mon is not { } mon)
            return slot.Refused is { } why ? $"{where}: this record does not read ({why}), so it is shown and never written." : $"{where}: empty.";
        var names = model.Names;
        var national = mon.National is { } n && n != mon.Species ? $" (National #{n})" : "";
        var lines = new List<string>
        {
            $"{where}: {slot.Title}",
            $"Record: {mon.Codec}",
            $"Species: {Named(HackNameKind.Species, mon.Species)}{national}{(mon.IsEgg ? ", egg" : "")}",
            $"Held item: {Named(HackNameKind.Item, mon.HeldItem)}",
            $"EXP {mon.Exp}{((mon.Party?.Level ?? mon.Level) is { } level ? $", level {level}" : "")}",
            $"Moves: {string.Join(", ", mon.Moves.Select(m => m == 0 ? "-" : Named(HackNameKind.Move, m)))}",
            $"EVs (HP/Atk/Def/SpA/SpD/Spe): {mon.Evs}",
            $"IVs: {(mon.Ivs is { } ivs ? ivs.ToString() : "not in this record")}",
        };
        if (mon.Nature is { } nature)
            lines.Add($"Nature: {NatureName(nature)}");
        if (mon.AbilitySlot is { } ability)
        {
            var labels = names.AbilitySlots(mon.Species);
            lines.Add($"Ability: {(ability < labels.Count ? labels[ability] : $"slot {ability + 1}")}");
        }
        if (mon.Ball is { } ball)
            lines.Add($"Ball #{ball}");
        if (mon.Friendship is { } friendship)
            lines.Add($"Friendship {friendship}");
        if (mon.HiddenAbility is { } hidden)
            lines.Add($"Hidden ability: {(hidden ? "yes" : "no")}");
        lines.Add(EditableFields.WhyNone(mon.Codec.Id) ?? $"Fields an editable save of this format can change: {string.Join(", ", EditableFields.Writable(mon.Codec.Id, mon.Shape))}");
        if (!Writable)
            lines.Add("Read-only: " + ReadOnlySentence);
        else if (model.WhyNotEditable(where) is { } locked)
            lines.Add("Not editable: " + locked);
        return string.Join(Environment.NewLine, lines);
    }

    private void ShowSlot(HackSlotRef where, string? said)
    {
        detail.Text = Detail(where);
        BuildEditor(where);
        if (said is not null)
            status.Text = said;
    }

    private void BuildEditor(HackSlotRef where)
    {
        grid.SuspendLayout();
        var old = grid.Controls.Cast<Control>().ToList();
        grid.Controls.Clear();
        foreach (var control in old)
            control.Dispose();
        grid.RowStyles.Clear();
        grid.RowCount = 0;
        rows.Clear();
        editPanel.Visible = false;
        if (!Writable || model.WhyNotEditable(where) is not null || model.Edit(where) is not { } edit)
        {
            grid.ResumeLayout();
            return;
        }
        foreach (var spec in edit.Specs)
            AddRows(edit, spec);
        foreach (var row in rows)
        {
            var at = grid.RowCount++;
            grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label { Text = row.Label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 4, 0, 0) };
            row.View.Dock = DockStyle.Fill;
            row.View.Enabled = edit.CanEdit(row.Field);
            if (!row.View.Enabled && edit.LockedReason(row.Field) is { } why)
            {
                var tip = new ToolTip();
                tip.SetToolTip(row.View, why);
                tip.SetToolTip(label, why);
                row.View.Disposed += (_, _) => tip.Dispose();
            }
            grid.Controls.Add(label, 0, at);
            grid.Controls.Add(row.View, 1, at);
        }
        grid.ResumeLayout();
        editPanel.Visible = rows.Count > 0;
    }

    private void AddText(string label, string field, string value, int max, Action<HackMonEditModel, string> write)
    {
        var box = new TextBox { Text = value, MaxLength = Math.Max(1, max) };
        rows.Add(new FieldRow(label, field, box, value, () => box.Text, write));
    }

    private void AddPicker(string label, string field, HackNameKind kind, int value, bool none, Action<HackMonEditModel, int> write)
    {
        var shown = Named(kind, value);
        var box = new TextBox { Text = shown, PlaceholderText = model.Names.Has(kind) ? "type a name, or #id" : "a number" };
        rows.Add(new FieldRow(label, field, box, shown, () => box.Text, (e, t) => write(e, Pick(kind, t, none))));
    }

    private void AddNumber(string label, string field, string value, Action<HackMonEditModel, string> write)
    {
        var box = new TextBox { Text = value };
        rows.Add(new FieldRow(label, field, box, value, () => box.Text, write));
    }

    private void AddChoice(string label, string field, IReadOnlyList<string> choices, int? value, Action<HackMonEditModel, int> write)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var choice in choices)
            combo.Items.Add(choice);
        if (value is { } v && v >= choices.Count)
        {
            for (var i = choices.Count; i <= v; i++)
                combo.Items.Add($"#{i}");
        }
        combo.SelectedIndex = value is { } at && at < combo.Items.Count ? at : -1;
        var initial = combo.SelectedIndex.ToString(CultureInfo.InvariantCulture);
        rows.Add(new FieldRow(label, field, combo, initial, () => combo.SelectedIndex.ToString(CultureInfo.InvariantCulture), (e, t) => write(e, int.Parse(t, NumberStyles.Integer, CultureInfo.InvariantCulture))));
    }

    private void AddFlag(string label, string field, bool value, Action<HackMonEditModel, bool> write)
    {
        var check = new CheckBox { Checked = value, Text = "yes", AutoSize = true };
        var initial = value ? "yes" : "no";
        rows.Add(new FieldRow(label, field, check, initial, () => check.Checked ? "yes" : "no", (e, t) => write(e, t == "yes")));
    }

    private static uint ParseUInt(string text) => uint.Parse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);

    private static byte ParseByte(string text) => byte.Parse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);

    private static int ParseInt(string text) => int.Parse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);

    private void AddRows(HackMonEditModel edit, EditFieldSpec spec)
    {
        var field = spec.Field;
        switch (field)
        {
            case "Species":
                AddPicker("Species", field, HackNameKind.Species, edit.Species, false, (e, v) => e.Species = checked((ushort)v));
                break;
            case EditFieldSpecs.Nickname:
                AddText("Nickname", field, edit.Nickname ?? "", spec.Max, (e, t) => e.Nickname = t);
                break;
            case "HeldItem":
                AddPicker("Held item", field, HackNameKind.Item, edit.HeldItem, true, (e, v) => e.HeldItem = checked((ushort)v));
                break;
            case "Moves":
                for (var i = 0; i < 4; i++)
                {
                    var slot = i;
                    AddPicker($"Move {slot + 1}", field, HackNameKind.Move, slot < edit.Moves.Count ? edit.Moves[slot] : 0, true, (e, v) => e.SetMove(slot, checked((ushort)v)));
                }
                break;
            case EditableFields.Level:
                AddNumber("Level", field, edit.Level?.ToString(CultureInfo.InvariantCulture) ?? "", (e, t) => e.Level = ParseByte(t));
                break;
            case "Exp":
                AddNumber("EXP", field, edit.Exp.ToString(CultureInfo.InvariantCulture), (e, t) => e.Exp = ParseUInt(t));
                break;
            case EditFieldSpecs.Nature:
                AddChoice("Nature", field, Enumerable.Range(0, 25).Select(NatureName).ToList(), edit.Nature, (e, v) => e.Nature = checked((byte)v));
                break;
            case EditFieldSpecs.AbilitySlot:
                AddChoice("Ability", field, model.Names.AbilitySlots(edit.Species).Take(Math.Max(0, spec.Max) + 1).ToList(), edit.AbilitySlot, (e, v) => e.AbilitySlot = checked((byte)v));
                break;
            case "HiddenAbility":
                AddFlag("Hidden ability", field, edit.HiddenAbility == true, (e, on) => e.HiddenAbility = on);
                break;
            case "Ivs":
                for (var k = 0; k < StatLabels.Length; k++)
                {
                    var stat = k;
                    AddNumber($"{StatLabels[stat]} IV", field, edit.Ivs is { } now ? now[stat].ToString(CultureInfo.InvariantCulture) : "", (e, t) =>
                    {
                        var v = ParseInt(t);
                        if (e.Ivs is not { } s)
                            throw new InvalidOperationException("this record holds no IVs");
                        e.Ivs = stat switch
                        {
                            0 => s with { Hp = v },
                            1 => s with { Atk = v },
                            2 => s with { Def = v },
                            3 => s with { SpA = v },
                            4 => s with { SpD = v },
                            _ => s with { Spe = v },
                        };
                    });
                }
                break;
            case "Evs":
                for (var k = 0; k < StatLabels.Length; k++)
                {
                    var stat = k;
                    AddNumber($"{StatLabels[stat]} EVs", field, edit.Evs[stat].ToString(CultureInfo.InvariantCulture), (e, t) =>
                    {
                        var v = ParseInt(t);
                        var s = e.Evs;
                        e.Evs = stat switch
                        {
                            0 => s with { Hp = v },
                            1 => s with { Atk = v },
                            2 => s with { Def = v },
                            3 => s with { SpA = v },
                            4 => s with { SpD = v },
                            _ => s with { Spe = v },
                        };
                    });
                }
                break;
            case "Friendship":
                AddNumber("Friendship", field, edit.Friendship?.ToString(CultureInfo.InvariantCulture) ?? "", (e, t) => e.Friendship = ParseByte(t));
                break;
            case "Ball":
                AddNumber("Ball", field, edit.Ball?.ToString(CultureInfo.InvariantCulture) ?? "", (e, t) => e.Ball = ParseByte(t));
                break;
            case "IsEgg":
                AddFlag("Egg", field, edit.IsEgg, (e, on) => e.IsEgg = on);
                break;
        }
    }

    private int Pick(HackNameKind kind, string text, bool none)
    {
        var names = model.Names;
        var noun = kind switch
        {
            HackNameKind.Species => "species",
            HackNameKind.Move => "move",
            HackNameKind.Item => "item",
            _ => "ability",
        };
        var t = text.Trim();
        int id;
        if (t.Length == 0 || t == "-" || string.Equals(t, "none", StringComparison.OrdinalIgnoreCase))
        {
            if (!none)
                throw new FormatException($"give a {noun}");
            id = 0;
        }
        else if (int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            id = number;
        }
        else if ((IdAtEnd.Match(t) is { Success: true } end ? end : IdAtStart.Match(t)) is { Success: true } match
            && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var tagged))
        {
            id = tagged;
        }
        else if (!names.Has(kind))
        {
            throw new FormatException($"\"{t}\" is not a number of zero or more; {HackNames.Banner}");
        }
        else
        {
            id = names.Resolve(kind, t) ?? throw new FormatException(names.Search(kind, t).Count == 0
                ? $"no {noun} in this hack's table is called \"{t}\""
                : $"\"{t}\" fits more than one {noun} name; type #id");
        }
        if (names.WhyRefused(kind, id) is { } why)
            throw new FormatException(why);
        return id;
    }

    private void ApplyEdit()
    {
        if (!Writable || slots.SelectedItem is not SlotRow row)
            return;
        if (model.Edit(row.Where) is not { } fresh)
        {
            status.Text = $"{row.Where} no longer holds a Pokemon.";
            return;
        }
        foreach (var field in rows)
        {
            var text = field.Current();
            if (text == field.Initial)
                continue;
            try
            {
                field.Write(fresh, text);
            }
            catch (Exception ex) when (ex is InvalidOperationException or OverflowException or ArgumentOutOfRangeException or FormatException)
            {
                status.Text = $"{field.Label}: {ex.Message}";
                return;
            }
        }
        if (fresh.Changed.Count == 0)
        {
            status.Text = "Nothing changed.";
            return;
        }
        var changed = string.Join(", ", fresh.Changed);
        if (model.Apply(fresh) is { } refused)
        {
            status.Text = refused;
            return;
        }
        var notes = fresh.Notes.Count == 0 ? "" : " " + string.Join(" ", fresh.Notes);
        RefreshAll();
        status.Text = $"Applied to {row.Where}: {changed}. Save to file writes it.{notes}";
    }

    private void SetMoney()
    {
        if (!Writable)
            return;
        if (!uint.TryParse(money.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            status.Text = $"Money: \"{money.Text}\" is not a number of zero or more.";
            return;
        }
        status.Text = model.SetMoney(value) ?? $"Money set to {value:N0} in memory. Save to file writes it.";
        RefreshAll();
    }

    private void SaveToFile()
    {
        if (!Writable)
        {
            status.Text = "Nothing was written. " + ReadOnlySentence;
            return;
        }
        if (model.Edits.Count == 0 && model.MoneyEdit is null)
        {
            status.Text = "Nothing to write: no change was applied.";
            return;
        }
        byte[] now;
        try
        {
            now = File.ReadAllBytes(savePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status.Text = $"Nothing was written: the file could not be read ({ex.Message}).";
            return;
        }
        if (!now.AsSpan().SequenceEqual(opened.Span))
        {
            status.Text = "Nothing was written: the file changed on disk since it was opened. Open it again.";
            return;
        }
        var check = model.CheckedWrite(result.Shape, now, out var bytes);
        if (!check.Passed || bytes is null)
        {
            status.Text = $"Nothing was written. {check}";
            return;
        }
        var what = model.Edits.Count switch
        {
            0 => "",
            1 => "1 edited slot",
            var n => $"{n} edited slots",
        };
        if (model.MoneyEdit is not null)
            what += what.Length == 0 ? "money" : " and money";
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Write {what} to {savePath}?", "A dated backup of the file is written beside it first. If the backup cannot be written, nothing is.") != DialogResult.Yes)
            return;
        string backup;
        byte[] kept;
        try
        {
            backup = HackSaveBackup.Write(savePath, now);
            kept = File.ReadAllBytes(backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            status.Text = $"Nothing was written: the backup could not be made ({ex.Message}).";
            return;
        }
        var commit = model.Commit(bytes, kept, now, b => HackSaveBackup.Replace(savePath, b), () => File.ReadAllBytes(savePath));
        if (commit.Passed)
        {
            written = true;
            opened = bytes;
            RefreshAll();
            status.Text = $"Written to {savePath}. Backup of the file as it was: {backup}. Open the save again to edit it further.";
            return;
        }
        status.Text = $"{commit} Backup of the file as it was: {backup}.";
    }

    private void ShowGate()
    {
        var lines = new List<string> { Writable ? "This save is admitted for writing." : "Read-only: " + ReadOnlySentence };
        foreach (var item in model.GateItems)
            lines.Add(item.ToString());
        lines.AddRange(result.Notes);
        WinFormsUtil.Alert(lines.ToArray());
    }
}
