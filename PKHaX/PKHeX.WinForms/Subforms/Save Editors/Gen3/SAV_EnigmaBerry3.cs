using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using PKHaX.Hacks.Intake;
using PKHeX.Core;

namespace PKHeX.WinForms;

public partial class SAV_EnigmaBerry3 : Form
{
    public const string NoRecordReason = "This save has no e-Reader berry record. Only Ruby, Sapphire, Emerald, FireRed and LeafGreen keep one.";

    public const string TagNoSourceWarning =
        "The berry's CHECK TAG text has no source without the game's ROM. The berry works in battle; do not open its tag screen.";

    public const string ItemSourceNote =
        "This record only defines what item 175, the Enigma Berry, does. The item itself is added in the bag editor "
        + "(Berries pocket) or set as a held item.";

    public const string LegalityNote =
        "A Pokemon holding the berry passes the legality check only when the name is one of the distributed e-Reader "
        + "berry names (PUMKIN, DRASH, EGGANT, STRIB, CHILAN, NUTPEA; a Japanese save has its own distributed names). "
        + "Any other name is flagged as an invalid e-Reader berry.";

    public const string ActiveSlotNote = "Only the active save slot is edited. The backup slot keeps the old record.";

    public const string CommitNote = "Apply writes into the Misc editor's copy of the save. Press Save there, then export the save, to put it on disk.";

    public const string RomLanguageRefusal =
        "That Emerald ROM is not the same language family as this save, so its text addresses do not fit.";

    public const string NothingChanged = "Nothing is changed, so nothing was written.";

    public const string NoEffectWhenUsed = "00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00";

    private const long RomLimit = 64L * 1024 * 1024;
    private const int GameCodeOffset = 0xAC;

    private readonly SAV3 SAV;
    private readonly EnigmaBerry3 Berry;
    private readonly bool OpenedValid;
    private readonly string? OpenedProblem;
    private readonly byte[] ItemEffectAsFound;
    private readonly string? ReadOnlyReason;
    private readonly string? WriteRefusal;
    private bool loading;
    private string? notice;

    public bool Written { get; private set; }

    public static string HackKitReason(string kitName) =>
        $"This save is open with the ROM hack kit {kitName}. A hack can keep the retail layout while it uses the berry's "
        + "bytes for something else, so the Enigma Berry record is shown read-only for hack saves in this version.";

    public static string BridgedPartyReason(SaveStateFacet facet) =>
        $"This is a live-RAM party bridged from a save state ({facet}); it has no e-Reader berry record. "
        + "The bridge copies only the six party slots in and out of the state, so a record shown here would not be the "
        + "game's and an edit would go nowhere. Open the battery save (.sav/.srm) to edit the berry.";

    public static string? Unavailable(SAV3 sav, SaveStateFacet? facet = null)
    {
        if (facet is { } f && f != SaveStateFacet.EmbeddedSave)
            return BridgedPartyReason(f);
        return EnigmaBerry3.TryKind(sav.LargeBlock.EReaderBerry.Length, out _) ? null : NoRecordReason;
    }

    public SAV_EnigmaBerry3(SAV3 sav, string? writeRefusal = null, StoredKit? kit = null)
    {
        InitializeComponent();
        BuildRows();
        WinFormsUtil.TranslateInterface(this, Main.CurrentLanguage);
        SAV = sav;
        WriteRefusal = writeRefusal;
        Berry = EnigmaBerry3.From(sav);
        OpenedValid = Berry.IsValid;
        OpenedProblem = Berry.Problem;
        ItemEffectAsFound = Berry.ItemEffect.ToArray();
        if (kit is not null)
            ReadOnlyReason = HackKitReason(kit.Kit.Name);
        if (!OpenedValid && ReadOnlyReason is null)
            UseStockValues();

        L_Heading.Text = $"{SAV.Version} · Gen 3 · e-Reader berry record, {Berry.Length} bytes";
        L_Legality.Text = LegalityNote;
        L_ItemSource.Text = ItemSourceNote;
        L_ActiveSlot.Text = $"{ActiveSlotNote} {CommitNote}";

        foreach (var effect in EnigmaBerryEffects.All)
            CB_Effect.Items.Add(effect.Label);
        foreach (var preset in EnigmaBerryEffects.Presets)
            AddPreset($"{preset.Name} ({preset.HoldEffect} / {preset.Parameter})", () => preset);
        AddPreset("Use the stock number of the picked effect", () => EnigmaBerryEffects.Stock(Berry.HoldEffect));

        foreach (var nud in Numbers())
            nud.ValueChanged += (_, _) => OnEdited();
        foreach (var box in Texts())
            box.TextChanged += (_, _) => OnEdited();
        NUD_Parameter.ValueChanged += (_, _) =>
        {
            if (!loading && NUD_RawParameter.Value != NUD_Parameter.Value)
                NUD_RawParameter.Value = NUD_Parameter.Value;
        };
        CB_Effect.SelectedIndexChanged += (_, _) =>
        {
            if (!loading && CB_Effect.SelectedIndex >= 0)
                NUD_RawEffect.Value = CB_Effect.SelectedIndex;
        };
        B_NoEffect.Click += (_, _) => TB_ItemEffect.Text = NoEffectWhenUsed;
        B_AsFound.Click += (_, _) => TB_ItemEffect.Text = FormatItemEffect(ItemEffectAsFound);
        B_Rom.Click += (_, _) => PickRom();
        B_Apply.Click += (_, _) => ApplyRecord();
        B_Revert.Click += (_, _) =>
        {
            Berry.Revert();
            if (!Berry.IsValid && ReadOnlyReason is null)
                UseStockValues();
            Push("Reverted to the record this copy of the save holds now.");
        };

        if (ReadOnlyReason is not null)
        {
            foreach (Control control in TLP_Fields.Controls)
            {
                if (control is not Label)
                    control.Enabled = false;
            }
            B_Revert.Enabled = false;
        }

        Push(null);
    }

    private void BuildRows()
    {
        TLP_Fields.SuspendLayout();
        Wide(L_Heading, "L_Heading");
        L_Heading.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold);
        Wide(L_Status, "L_Status");
        Wide(L_Tag, "L_Tag");
        L_Tag.ForeColor = System.Drawing.Color.Firebrick;

        Header(L_PresetsHeader, "L_PresetsHeader", "Presets");
        FLP_Presets.AutoSize = true;
        FLP_Presets.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        FLP_Presets.MaximumSize = new System.Drawing.Size(580, 0);
        FLP_Presets.Name = "FLP_Presets";
        AddWide(FLP_Presets);

        Header(L_EffectHeader, "L_EffectHeader", "Hold effect");
        CB_Effect.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
        CB_Effect.Name = "CB_Effect";
        CB_Effect.Width = 370;
        Row(L_Effect, "L_Effect", "Hold effect", CB_Effect);
        Number(NUD_Parameter, "NUD_Parameter", 255);
        Row(L_Parameter, "L_Parameter", "Parameter", NUD_Parameter);
        Wide(L_Sentence, "L_Sentence");
        Wide(L_Help, "L_Help");
        L_Help.ForeColor = System.Drawing.SystemColors.GrayText;
        Wide(L_Warning, "L_Warning");
        L_Warning.ForeColor = System.Drawing.Color.Firebrick;
        Number(NUD_RawEffect, "NUD_RawEffect", 255);
        Row(L_RawEffect, "L_RawEffect", "Raw effect number (0-255)", NUD_RawEffect);
        Number(NUD_RawParameter, "NUD_RawParameter", 255);
        Row(L_RawParameter, "L_RawParameter", "Raw parameter (0-255)", NUD_RawParameter);

        Header(L_NameHeader, "L_NameHeader", "Name");
        TB_Name.MaxLength = 6;
        TB_Name.Name = "TB_Name";
        TB_Name.Width = 120;
        Row(L_Name, "L_Name", "Up to 6 characters", TB_Name);
        Wide(L_Legality, "L_Legality");
        L_Legality.ForeColor = System.Drawing.SystemColors.GrayText;

        Header(L_StatsHeader, "L_StatsHeader", "Berry stats");
        Number(NUD_Firmness, "NUD_Firmness", 255);
        Row(L_Firmness, "L_Firmness", "Firmness", NUD_Firmness);
        Number(NUD_Size, "NUD_Size", 65535);
        Row(L_Size, "L_Size", "Size", NUD_Size);
        Number(NUD_MaxYield, "NUD_MaxYield", 255);
        Row(L_MaxYield, "L_MaxYield", "Largest harvest", NUD_MaxYield);
        Number(NUD_MinYield, "NUD_MinYield", 255);
        Row(L_MinYield, "L_MinYield", "Smallest harvest", NUD_MinYield);
        Number(NUD_Stage, "NUD_Stage", 255);
        Row(L_Stage, "L_Stage", "Growth time per stage", NUD_Stage);
        Number(NUD_Spicy, "NUD_Spicy", 255);
        Row(L_Spicy, "L_Spicy", "Spicy", NUD_Spicy);
        Number(NUD_Dry, "NUD_Dry", 255);
        Row(L_Dry, "L_Dry", "Dry", NUD_Dry);
        Number(NUD_Sweet, "NUD_Sweet", 255);
        Row(L_Sweet, "L_Sweet", "Sweet", NUD_Sweet);
        Number(NUD_Bitter, "NUD_Bitter", 255);
        Row(L_Bitter, "L_Bitter", "Bitter", NUD_Bitter);
        Number(NUD_Sour, "NUD_Sour", 255);
        Row(L_Sour, "L_Sour", "Sour", NUD_Sour);
        Number(NUD_Smoothness, "NUD_Smoothness", 255);
        Row(L_Smoothness, "L_Smoothness", "Smoothness", NUD_Smoothness);

        Header(L_ItemEffectHeader, "L_ItemEffectHeader", "Effect when used from the bag");
        TB_ItemEffect.Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, 9F);
        TB_ItemEffect.Name = "TB_ItemEffect";
        TB_ItemEffect.Width = 370;
        Row(L_ItemEffect, "L_ItemEffect", "18 bytes, two hex digits each", TB_ItemEffect);
        B_NoEffect.AutoSize = true;
        B_NoEffect.Name = "B_NoEffect";
        B_NoEffect.Text = "No effect when used";
        B_NoEffect.UseVisualStyleBackColor = true;
        B_AsFound.AutoSize = true;
        B_AsFound.Name = "B_AsFound";
        B_AsFound.Text = "As found";
        B_AsFound.UseVisualStyleBackColor = true;
        FLP_ItemEffect.AutoSize = true;
        FLP_ItemEffect.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        FLP_ItemEffect.Controls.Add(B_NoEffect);
        FLP_ItemEffect.Controls.Add(B_AsFound);
        FLP_ItemEffect.Name = "FLP_ItemEffect";
        AddWide(FLP_ItemEffect);

        Header(L_TagHeader, "L_TagHeader", "CHECK TAG text");
        TB_Desc1.MaxLength = 44;
        TB_Desc1.Name = "TB_Desc1";
        TB_Desc1.Width = 370;
        Row(L_Desc1, "L_Desc1", "Line 1", TB_Desc1);
        TB_Desc2.MaxLength = 44;
        TB_Desc2.Name = "TB_Desc2";
        TB_Desc2.Width = 370;
        Row(L_Desc2, "L_Desc2", "Line 2", TB_Desc2);
        Wide(L_DescNote, "L_DescNote");
        L_DescNote.ForeColor = System.Drawing.SystemColors.GrayText;
        Wide(L_Pointers, "L_Pointers");
        B_Rom.AutoSize = true;
        B_Rom.Name = "B_Rom";
        B_Rom.Text = "Fill the CHECK TAG text from the game's ROM...";
        B_Rom.UseVisualStyleBackColor = true;
        AddWide(B_Rom);

        Header(L_WriteHeader, "L_WriteHeader", "Write");
        Wide(L_ItemSource, "L_ItemSource");
        L_ItemSource.ForeColor = System.Drawing.SystemColors.GrayText;
        Wide(L_ActiveSlot, "L_ActiveSlot");
        L_ActiveSlot.ForeColor = System.Drawing.SystemColors.GrayText;
        Wide(L_Result, "L_Result");
        TLP_Fields.ResumeLayout(false);
        TLP_Fields.PerformLayout();
    }

    private void AddWide(System.Windows.Forms.Control control)
    {
        int row = TLP_Fields.RowCount++;
        TLP_Fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        control.Margin = new System.Windows.Forms.Padding(3);
        TLP_Fields.Controls.Add(control, 0, row);
        TLP_Fields.SetColumnSpan(control, 2);
    }

    private void Wide(System.Windows.Forms.Label label, string name)
    {
        label.AutoSize = true;
        label.MaximumSize = new System.Drawing.Size(570, 0);
        label.Name = name;
        AddWide(label);
    }

    private void Header(System.Windows.Forms.Label label, string name, string text)
    {
        Wide(label, name);
        label.Text = text;
        label.Font = new System.Drawing.Font(System.Drawing.SystemFonts.DefaultFont, System.Drawing.FontStyle.Bold);
        label.Margin = new System.Windows.Forms.Padding(3, 12, 3, 3);
    }

    private void Row(System.Windows.Forms.Label label, string name, string text, System.Windows.Forms.Control field)
    {
        int row = TLP_Fields.RowCount++;
        TLP_Fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        label.Anchor = System.Windows.Forms.AnchorStyles.Left;
        label.AutoSize = true;
        label.MaximumSize = new System.Drawing.Size(195, 0);
        label.Name = name;
        label.Text = text;
        field.Anchor = System.Windows.Forms.AnchorStyles.Left;
        TLP_Fields.Controls.Add(label, 0, row);
        TLP_Fields.Controls.Add(field, 1, row);
    }

    private static void Number(System.Windows.Forms.NumericUpDown nud, string name, int maximum)
    {
        nud.Maximum = maximum;
        nud.Minimum = 0;
        nud.Name = name;
        nud.Width = 90;
    }

    private void AddPreset(string text, Func<EnigmaBerryPreset> preset)
    {
        var button = new Button { AutoSize = true, Text = text, UseVisualStyleBackColor = true };
        button.Click += (_, _) =>
        {
            var picked = preset();
            Berry.Apply(picked);
            Push($"{picked.Name}: hold effect {picked.HoldEffect}, parameter {picked.Parameter}. {picked.Note} Nothing is written until Apply.");
        };
        FLP_Presets.Controls.Add(button);
    }

    private NumericUpDown[] Numbers() =>
    [
        NUD_RawEffect, NUD_RawParameter, NUD_Firmness, NUD_Size, NUD_MaxYield, NUD_MinYield, NUD_Stage,
        NUD_Spicy, NUD_Dry, NUD_Sweet, NUD_Bitter, NUD_Sour, NUD_Smoothness,
    ];

    private TextBox[] Texts() => [TB_Name, TB_ItemEffect, TB_Desc1, TB_Desc2];

    private void UseStockValues()
    {
        Berry.UseDefaults();
        if (SAV is SAV3FRLG)
            return;
        Berry.DescriptionPointer1 = 0;
        Berry.DescriptionPointer2 = 0;
    }

    private bool KeepsPointersAsFound => SAV is SAV3FRLG;

    private bool TagTextMissing => !KeepsPointersAsFound && (Berry.DescriptionPointer1 == 0 || Berry.DescriptionPointer2 == 0);

    private string? TagWarning => TagTextMissing ? TagNoSourceWarning : null;

    private bool RomCanFill => SAV is SAV3E && (!Berry.IsValid || Berry.DescriptionPointer1 == 0 || Berry.DescriptionPointer2 == 0);

    private string PointerLine => KeepsPointersAsFound
        ? $"Description pointers 0x{Berry.DescriptionPointer1:X8} and 0x{Berry.DescriptionPointer2:X8}: FireRed and LeafGreen never read them, so they are kept as found."
        : $"Description pointers 0x{Berry.DescriptionPointer1:X8} and 0x{Berry.DescriptionPointer2:X8}.";

    private string StatusLine => Berry.IsValid
        ? "The record is valid: the game uses it for item 175."
        : OpenedValid
            ? $"The record is not valid. {Berry.Problem}"
            : ReadOnlyReason is not null
            ? $"The record in this save is not valid. {OpenedProblem}"
            : $"The record in this save is not valid. {OpenedProblem} Stock values are filled in below; nothing is written until Apply.";

    private string EffectSentence => EnigmaBerryEffects.Describe(Berry.HoldEffect, Berry.HoldEffectParam);

    private static void SetNumber(NumericUpDown nud, int value) => nud.Value = Math.Clamp(value, (int)nud.Minimum, (int)nud.Maximum);

    private void Push(string? note)
    {
        notice = note;
        loading = true;
        try
        {
            TB_Name.Text = Berry.Name;
            SetNumber(NUD_Firmness, Berry.Firmness);
            SetNumber(NUD_Size, Berry.Size);
            SetNumber(NUD_MaxYield, Berry.MaxYield);
            SetNumber(NUD_MinYield, Berry.MinYield);
            SetNumber(NUD_Stage, Berry.StageDuration);
            SetNumber(NUD_Spicy, Berry.Spicy);
            SetNumber(NUD_Dry, Berry.Dry);
            SetNumber(NUD_Sweet, Berry.Sweet);
            SetNumber(NUD_Bitter, Berry.Bitter);
            SetNumber(NUD_Sour, Berry.Sour);
            SetNumber(NUD_Smoothness, Berry.Smoothness);
            SetNumber(NUD_RawEffect, Berry.HoldEffect);
            SetNumber(NUD_RawParameter, Berry.HoldEffectParam);
            TB_ItemEffect.Text = FormatItemEffect(Berry.ItemEffect);
            TB_Desc1.Text = Berry.Description1;
            TB_Desc2.Text = Berry.Description2;
        }
        finally
        {
            loading = false;
        }
        RefreshDerived(null);
    }

    private void OnEdited()
    {
        if (loading)
            return;
        notice = null;
        RefreshDerived(ReadControls());
    }

    private string? ReadControls()
    {
        if (!TryParseItemEffect(TB_ItemEffect.Text, out var itemEffect, out var refusal))
            return refusal;
        if (TB_Name.Text != Berry.Name && !Berry.TrySetName(TB_Name.Text.AsSpan(), out refusal))
            return refusal;
        if (Berry.HasOwnDescriptionText)
        {
            if (TB_Desc1.Text != Berry.Description1 && !Berry.TrySetDescription(1, TB_Desc1.Text.AsSpan(), out refusal))
                return refusal;
            if (TB_Desc2.Text != Berry.Description2 && !Berry.TrySetDescription(2, TB_Desc2.Text.AsSpan(), out refusal))
                return refusal;
        }

        Berry.Firmness = (byte)NUD_Firmness.Value;
        Berry.Size = (ushort)NUD_Size.Value;
        Berry.MaxYield = (byte)NUD_MaxYield.Value;
        Berry.MinYield = (byte)NUD_MinYield.Value;
        Berry.StageDuration = (byte)NUD_Stage.Value;
        Berry.Spicy = (byte)NUD_Spicy.Value;
        Berry.Dry = (byte)NUD_Dry.Value;
        Berry.Sweet = (byte)NUD_Sweet.Value;
        Berry.Bitter = (byte)NUD_Bitter.Value;
        Berry.Sour = (byte)NUD_Sour.Value;
        Berry.Smoothness = (byte)NUD_Smoothness.Value;
        Berry.HoldEffect = (byte)NUD_RawEffect.Value;
        Berry.HoldEffectParam = (byte)NUD_RawParameter.Value;
        if (!itemEffect.AsSpan().SequenceEqual(Berry.ItemEffect))
            Berry.SetItemEffect(itemEffect);
        return null;
    }

    private void RefreshDerived(string? refusal)
    {
        var effect = EnigmaBerryEffects.Get(Berry.HoldEffect);
        var editable = ReadOnlyReason is null;
        loading = true;
        try
        {
            CB_Effect.SelectedIndex = effect.Known ? effect.Id : -1;
            SetNumber(NUD_Parameter, Berry.HoldEffectParam);
        }
        finally
        {
            loading = false;
        }

        NUD_Parameter.Enabled = editable && effect.ParameterUsed;
        L_Parameter.Text = effect.ParameterLabel;
        L_Help.Text = effect.Help;
        L_Sentence.Text = EffectSentence;
        var warning = EnigmaBerryEffects.IsKnown(Berry.HoldEffect) ? null : EnigmaBerryEffects.UnknownWarning;
        L_Warning.Text = warning ?? string.Empty;
        L_Warning.Visible = warning is not null;

        var ownText = Berry.HasOwnDescriptionText;
        L_TagHeader.Visible = L_Desc1.Visible = TB_Desc1.Visible = L_Desc2.Visible = TB_Desc2.Visible = L_DescNote.Visible = ownText;
        TB_Desc1.Enabled = TB_Desc2.Enabled = editable && Berry.DescriptionTextEditable;
        L_DescNote.Text = Berry.DescriptionTextEditable
            ? "Two lines of up to 44 characters, kept in the save."
            : "The description pointers are zero, so the game does not read this text. It is left as found.";
        L_Pointers.Text = PointerLine;
        B_Rom.Visible = editable && RomCanFill;

        L_Status.Text = ReadOnlyReason is { } reason ? $"{StatusLine} {reason}" : StatusLine;
        L_Tag.Text = TagWarning ?? string.Empty;
        L_Tag.Visible = TagWarning is not null;

        var blocked = ReadOnlyReason ?? refusal ?? WriteRefusal ?? Berry.WriteRefusal;
        L_Result.Text = blocked ?? notice ?? (Berry.HasChanges
            ? "Changes are pending. Nothing is written until Apply."
            : "No pending changes.");
        B_Apply.Enabled = editable && WriteRefusal is null;
    }

    private void PickRom()
    {
        if (ReadOnlyReason is not null)
            return;
        using var dialog = new OpenFileDialog
        {
            Title = "Choose the Emerald ROM this save belongs to",
            Filter = "GBA ROMs (*.gba)|*.gba|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        byte[] rom;
        try
        {
            if (new FileInfo(dialog.FileName).Length > RomLimit)
            {
                Push($"{Path.GetFileName(dialog.FileName)} is too big to be a GBA ROM.");
                return;
            }
            rom = File.ReadAllBytes(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Push($"That file could not be read: {ex.Message}");
            return;
        }

        UseRom(rom, out var message);
        Push(message);
    }

    private bool UseRom(ReadOnlySpan<byte> rom, out string message)
    {
        if (SAV is SAV3FRLG)
        {
            message = "FireRed and LeafGreen never read the two description pointers, so they are kept as found.";
            return false;
        }
        if (SAV is not SAV3E)
        {
            message = "Ruby and Sapphire point the tag text at the record's own text in memory; a ROM cannot fill that in.";
            return false;
        }
        if (!RomCanFill)
        {
            message = "This record already has its own tag text pointers. They are kept as found.";
            return false;
        }
        if (rom.Length < GameCodeOffset + 4 || rom[GameCodeOffset] != (byte)'B' || rom[GameCodeOffset + 1] != (byte)'P' || rom[GameCodeOffset + 2] != (byte)'E')
        {
            message = "That file is not an Emerald ROM (its game code does not start with BPE), so its text addresses do not fit this save.";
            return false;
        }
        if ((rom[GameCodeOffset + 3] == (byte)'J') != SAV.Japanese)
        {
            message = RomLanguageRefusal;
            return false;
        }
        if (!EnigmaBerry3.TryFindStockDescriptions(rom, out var first, out var second, out var refusal))
        {
            message = refusal ?? "That ROM gives no tag text.";
            return false;
        }

        Berry.DescriptionPointer1 = first;
        Berry.DescriptionPointer2 = second;
        message = "The CHECK TAG text now points at the stock Enigma Berry text in that ROM. Nothing is written until Apply.";
        return true;
    }

    private void ApplyRecord()
    {
        if (ReadOnlyReason is not null)
        {
            RefreshDerived(null);
            return;
        }
        var refusal = ReadControls();
        if (refusal is not null)
        {
            RefreshDerived(refusal);
            return;
        }
        if (WriteRefusal is not null || Berry.WriteRefusal is not null || !Berry.HasChanges)
        {
            notice = Berry.HasChanges ? null : NothingChanged;
            RefreshDerived(null);
            return;
        }

        var effect = EnigmaBerryEffects.Get(Berry.HoldEffect);
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Write the Enigma Berry record into this save?",
                $"Hold effect {effect.Label}, parameter {Berry.HoldEffectParam}.", EffectSentence) != DialogResult.Yes)
            return;

        IReadOnlyList<EnigmaBerryRange> changed;
        try
        {
            changed = Berry.Write(SAV.LargeBlock.EReaderBerry);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Push($"The record was not written: {ex.Message}");
            return;
        }

        Written = true;
        var bytes = 0;
        foreach (var range in changed)
            bytes += range.Length;
        var tag = TagWarning is { } warning ? " " + warning : string.Empty;
        Push($"The Enigma Berry record is written into the Misc editor's copy of the save ({bytes} byte{(bytes == 1 ? "" : "s")} changed). "
            + "Press Save there, then export the save, to put it on disk." + tag);
    }

    public static string FormatItemEffect(ReadOnlySpan<byte> bytes)
    {
        var parts = new string[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
            parts[i] = bytes[i].ToString("X2", CultureInfo.InvariantCulture);
        return string.Join(" ", parts);
    }

    public static bool TryParseItemEffect(string? text, out byte[] bytes, out string? refusal)
    {
        bytes = new byte[EnigmaBerry3.ItemEffectLength];
        var tokens = (text ?? string.Empty).Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 1 && tokens[0].Length == EnigmaBerry3.ItemEffectLength * 2)
        {
            var joined = tokens[0];
            tokens = new string[EnigmaBerry3.ItemEffectLength];
            for (var i = 0; i < tokens.Length; i++)
                tokens[i] = joined.Substring(i * 2, 2);
        }
        if (tokens.Length != EnigmaBerry3.ItemEffectLength)
        {
            refusal = $"The item-use effect is {EnigmaBerry3.ItemEffectLength} bytes, written as two hex digits each; {tokens.Length} were given.";
            return false;
        }
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].Length > 2 || !byte.TryParse(tokens[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[i]))
            {
                refusal = $"\"{tokens[i]}\" is not a byte in hex (00 to FF).";
                return false;
            }
        }
        refusal = null;
        return true;
    }
}
