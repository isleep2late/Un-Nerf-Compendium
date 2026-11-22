using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Isleep2late.Files.Services.Pkhax;
using PKHaX.Hacks.Intake;

namespace PKHeX.WinForms;

public sealed class SAV_HackDrop : Form
{
    public const long MaxImageBytes = 1L << 30;
    private const long MaxSymBytes = 16L * 1024 * 1024;
    private const string ProfileSuffix = ".pkhaxprofile.json";

    private readonly HackKitStore store;
    private readonly HackIntake intake;
    private readonly string? firstFile;
    private readonly Panel dropArea;
    private readonly Label dropLabel;
    private readonly Button browse;
    private readonly ListView kits;
    private readonly Button openSave;
    private readonly Button export;
    private readonly Button forget;
    private readonly Button close;
    private readonly TextBox status;
    private bool busy;

    public string? SaveToOpen { get; private set; }
    public string? KitKey { get; private set; }

    public SAV_HackDrop(HackKitStore store, string? firstFile)
    {
        this.store = store;
        this.firstFile = firstFile;
        intake = new HackIntake(store, maxRomBytes: MaxImageBytes);

        Text = IntakeText.Title;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(560, 420);
        AllowDrop = true;

        dropLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Text = IntakeText.Target,
            Font = new Font(Font.FontFamily, Font.Size + 3, FontStyle.Bold),
            AllowDrop = true,
        };
        browse = new Button { Text = "Browse...", Dock = DockStyle.Right, Width = 110 };
        dropArea = new Panel { Dock = DockStyle.Top, Height = 96, BorderStyle = BorderStyle.FixedSingle, AllowDrop = true, Padding = new Padding(8) };
        dropArea.Controls.Add(dropLabel);
        dropArea.Controls.Add(browse);

        kits = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
        };
        kits.Columns.Add("Hack", 170);
        kits.Columns.Add("Base game", 90);
        kits.Columns.Add("Species", 60, HorizontalAlignment.Right);
        kits.Columns.Add("Moves", 60, HorizontalAlignment.Right);
        kits.Columns.Add("Items", 60, HorizontalAlignment.Right);
        kits.Columns.Add("Abilities", 60, HorizontalAlignment.Right);
        kits.Columns.Add("Save", 80);
        kits.Columns.Add("Added", 110);
        kits.Columns.Add("State", 200);

        openSave = new Button { Text = "Open a save for this hack...", AutoSize = true };
        export = new Button { Text = "Export profile...", AutoSize = true };
        forget = new Button { Text = "Forget", AutoSize = true };
        close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(4) };
        buttons.Controls.AddRange([openSave, export, forget, close]);

        status = new TextBox
        {
            Dock = DockStyle.Bottom,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Height = 120,
            Text = IntakeText.Stored,
        };

        Controls.Add(kits);
        Controls.Add(dropArea);
        Controls.Add(status);
        Controls.Add(buttons);
        CancelButton = close;

        foreach (var target in new Control[] { this, dropArea, dropLabel, kits })
        {
            target.AllowDrop = true;
            target.DragEnter += OnDragEnter;
            target.DragDrop += OnDragDrop;
        }
        browse.Click += (_, _) => Browse();
        kits.SelectedIndexChanged += (_, _) => UpdateButtons();
        kits.DoubleClick += (_, _) => OpenSaveForKit();
        openSave.Click += (_, _) => OpenSaveForKit();
        export.Click += (_, _) => ExportProfile();
        forget.Click += (_, _) => ForgetKit();
        FormClosing += (_, e) =>
        {
            if (busy && e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
        };
        Shown += async (_, _) =>
        {
            if (firstFile is not null)
                await TakeFile(firstFile);
        };

        ReloadKits(null);
    }

    private StoredKit? Selected => kits.SelectedItems.Count > 0 ? kits.SelectedItems[0].Tag as StoredKit : null;

    private void ReloadKits(string? select)
    {
        IReadOnlyList<StoredKit> list;
        try
        {
            list = store.List();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            list = [];
            Say($"The kept hacks could not be listed ({ex.Message}).");
        }
        kits.BeginUpdate();
        kits.Items.Clear();
        foreach (var kit in list)
        {
            var k = kit.Kit;
            var row = new ListViewItem(k.Name) { Tag = kit };
            row.SubItems.Add(k.BaseGame ?? k.Platform);
            row.SubItems.Add(k.Counts.Species.ToString(CultureInfo.InvariantCulture));
            row.SubItems.Add(k.Counts.Moves.ToString(CultureInfo.InvariantCulture));
            row.SubItems.Add(k.Counts.Items.ToString(CultureInfo.InvariantCulture));
            row.SubItems.Add(k.Counts.Abilities.ToString(CultureInfo.InvariantCulture));
            row.SubItems.Add(k.SaveFamily);
            row.SubItems.Add(k.At == default ? "" : k.At.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            row.SubItems.Add(kit.Usable ? "ready" : kit.Problem ?? "not usable");
            kits.Items.Add(row);
            if (select is not null && string.Equals(k.Key, select, StringComparison.Ordinal))
                row.Selected = true;
        }
        kits.EndUpdate();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var kit = Selected;
        var idle = !busy;
        browse.Enabled = idle;
        openSave.Enabled = idle && kit is { Usable: true };
        export.Enabled = idle && kit is { Usable: true, Profile: not null };
        forget.Enabled = idle && kit is not null;
        close.Enabled = idle;
        dropLabel.Text = busy ? "Reading..." : IntakeText.Target;
    }

    private void Say(string text) => status.Text = text;

    private void SetBusy(bool on)
    {
        busy = on;
        UseWaitCursor = on;
        UpdateButtons();
    }

    private static void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is not string[] { Length: not 0 } files)
            return;
        e.Effect = DragDropEffects.Copy;
        await TakeFile(files[0]);
    }

    private async void Browse()
    {
        using var dialog = new OpenFileDialog
        {
            Title = IntakeText.Target,
            Filter = "ROM hacks, patches and profiles|*.gba;*.gb;*.gbc;*.nds;*.ips;*.ups;*.bps;*.xdelta;*.vcdiff;*.json;*.is2lprofile|All files|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        await TakeFile(dialog.FileName);
    }

    private async Task TakeFile(string path)
    {
        if (busy)
            return;
        if (!File.Exists(path))
        {
            Say($"{path} is not a file.");
            return;
        }
        if (Main.ClassifyHackFile(path) is not { } verdict)
        {
            Say($"{Path.GetFileName(path)} could not be read.");
            return;
        }
        switch (verdict.Kind)
        {
            case DropKind.Patch:
                await TakePatch(path);
                break;
            case DropKind.GbaRom or DropKind.GbRom or DropKind.NdsRom:
                await TakeRom(path);
                break;
            case DropKind.CtrImage:
                Say(IntakeText.CannotRead);
                break;
            case DropKind.Profile or DropKind.Bundle:
                await TakeProfile(path);
                break;
            case DropKind.Save:
                SaveToOpen = path;
                KitKey = null;
                DialogResult = DialogResult.OK;
                Close();
                break;
            default:
                Say(verdict.Sentence ?? IntakeText.Unknown(Path.GetFileName(path)));
                break;
        }
    }

    private byte[]? ReadCapped(string path, long cap)
    {
        try
        {
            var length = new FileInfo(path).Length;
            if (length > cap)
            {
                Say(IntakeText.TooLarge(length, cap));
                return null;
            }
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            Say($"{Path.GetFileName(path)} could not be read ({ex.Message}).");
            return null;
        }
    }

    private static string? SymBeside(string path)
    {
        try
        {
            var sym = Path.ChangeExtension(path, ".sym");
            var info = new FileInfo(sym);
            return info.Exists && info.Length <= MaxSymBytes ? File.ReadAllText(sym) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private async Task TakeRom(string path)
    {
        if (ReadCapped(path, MaxImageBytes) is not { } rom)
            return;
        var sym = SymBeside(path);
        await Run(progress => intake.Derive(rom, path, sym, progress, path));
    }

    private async Task TakeProfile(string path)
    {
        if (ReadCapped(path, ProfileBundleReader.MaxBundleBytes) is not { } file)
            return;
        await Run(_ => intake.FromProfile(file, path));
    }

    private async Task TakePatch(string path)
    {
        if (ReadCapped(path, MaxImageBytes) is not { } patch)
            return;
        var patcher = PkhaxRomPatcher.Shared;
        PatchProbe probe;
        try
        {
            probe = patcher.Inspect(patch);
        }
        catch (RomPatchRefusedException ex)
        {
            Say($"The patch does not read: {ex.Message}");
            return;
        }
        if (!probe.Known)
        {
            Say("This file is not a patch PKHaX applies (IPS, IPS32, UPS, BPS or xdelta).");
            return;
        }
        var ask = probe.Format is PatchKind.Ips or PatchKind.Ips32 ? IntakeText.IpsPatch
            : probe.SourceCrc32 is { } crc && probe.SourceSize is { } size ? IntakeText.Patch(probe.Name, crc, size)
            : IntakeText.PatchNoCrc(probe.Name);
        Say(ask);

        string? basePath = null;
        if (probe.SourceCrc32 is { } wanted && store.FindBase(wanted, probe.SourceSize) is { } known
            && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, ask, $"PKHaX remembers this clean game at:{Environment.NewLine}{known.Path}", "Use it?") == DialogResult.Yes)
            basePath = known.Path;
        if (basePath is null)
        {
            using var dialog = new OpenFileDialog { Title = ask, Filter = "Game images|*.gba;*.gb;*.gbc;*.nds|All files|*.*" };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                Say("No clean game was chosen, so the patch was not used.");
                return;
            }
            basePath = dialog.FileName;
        }
        if (ReadCapped(basePath, MaxImageBytes) is not { } baseRom)
            return;
        var chosen = basePath;
        await Run(progress => intake.FromPatch(patch, baseRom, patcher, path, chosen, progress));
    }

    private async Task Run(Func<IProgress<string>, IntakeResult> work)
    {
        SetBusy(true);
        var progress = new Progress<string>(step => Say($"Reading: {step}..."));
        try
        {
            var result = await Task.Run(() => work(progress));
            ShowResult(result);
        }
        catch (Exception ex)
        {
            Say($"Nothing was kept: the reader stopped ({ex.Message}).");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowResult(IntakeResult result)
    {
        var lines = new List<string>();
        if (result.Sentence is { } sentence)
            lines.Add(sentence);
        if (result.Ok)
        {
            if (result.FromStore)
                lines.Add("This hack was already kept on this device.");
            lines.Add(IntakeText.Stored);
        }
        if (result.StoreProblem is { } problem)
            lines.Add(problem);
        lines.AddRange(result.Notes);
        Say(string.Join(Environment.NewLine, lines));
        ReloadKits(result.Kit?.Key);
    }

    private void OpenSaveForKit()
    {
        if (busy || Selected is not { Usable: true } kit)
            return;
        using var dialog = new OpenFileDialog
        {
            Title = $"Open a save for {kit.Kit.Name}",
            Filter = "Save files|*.sav;*.srm;*.fla;*.dsv;*.sa1;*.sa2;*.sa3;*.sa4;*.bin;*.dat|All files|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        SaveToOpen = dialog.FileName;
        KitKey = kit.Kit.Key;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void ExportProfile()
    {
        if (busy || Selected is not { Usable: true, Profile: not null } kit)
            return;
        string text;
        try
        {
            if (store.ExportProfile(kit.Kit.Key) is not { } exported)
            {
                Say($"{kit.Kit.Name}: its tables are not on this device, or they do not make a profile PKHaX reads back, so there is nothing to export. Drop the ROM again.");
                return;
            }
            text = exported;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            Say($"{kit.Kit.Name}: the profile could not be made ({ex.Message}).");
            return;
        }
        if (Encoding.UTF8.GetByteCount(text) > ProfileBundleReader.MaxMemberBytes)
        {
            Say($"{kit.Kit.Name}: the profile would be larger than {ProfileBundleReader.MaxMemberBytes:N0} bytes, which PKHaX does not read back, so it is not written.");
            return;
        }
        using var dialog = new SaveFileDialog
        {
            Title = "Export profile",
            FileName = SafeName(kit.Kit.Name) + ProfileSuffix,
            Filter = "PKHaX profile|*" + ProfileSuffix + "|All files|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            Say($"Exported to {dialog.FileName}. It holds the tables read from your ROM, not the ROM itself.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Say($"The profile was not written ({ex.Message}).");
        }
    }

    private void ForgetKit()
    {
        if (busy || Selected is not { } kit)
            return;
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Forget {kit.Kit.Name}?", "Its tables and the saves paired with it are removed from this device. Your ROM and your saves are not touched.") != DialogResult.Yes)
            return;
        try
        {
            Say(store.Forget(kit.Kit.Key) ? $"{kit.Kit.Name} is forgotten." : $"{kit.Kit.Name} was not stored.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Say($"{kit.Kit.Name} could not be removed ({ex.Message}).");
        }
        ReloadKits(null);
    }

    private static string SafeName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "hack" : clean;
    }
}
