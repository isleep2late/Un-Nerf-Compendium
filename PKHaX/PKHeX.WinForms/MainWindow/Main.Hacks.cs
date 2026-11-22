using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using PKHaX.Hacks.Intake;
using PKHeX.Core;

namespace PKHeX.WinForms;

public partial class Main
{
    internal const long MaxHackSaveBytes = 0x100_0000;

    private readonly ToolStripMenuItem Menu_RomHacks = new(IntakeText.Title + "...") { Name = "Menu_RomHacks" };
    private HackKitStore? hackStore;
    private OpenResult? hackSession;
    private bool hackOwnedOpen;
    private bool hackLegalityOff;
    private string? hackPlainPath;
    private DateTime hackPlainStamp;

    private static Main? hackHost;

    private HackKitStore HackStore => hackStore ??= new HackKitStore(HackKitStore.DefaultRoot());

    internal static SaveStateFacet? CurrentStateFacet =>
        hackHost is { StateSession: { } state } host && ReferenceEquals(state.Save, host.C_SAV.SAV) ? state.Facet : null;

    internal static string? CurrentWriteRefusal => hackHost?.hackSession?.WriteRefusal;

    internal static StoredKit? CurrentKit => hackHost?.hackSession?.Kit;

    private void AddHackMenu()
    {
        hackHost = this;
        var at = Menu_File.DropDownItems.IndexOf(Menu_ExportSAV);
        Menu_File.DropDownItems.Insert(at < 0 ? Menu_File.DropDownItems.Count : at + 1, Menu_RomHacks);
        Menu_RomHacks.Click += (_, _) => ShowHackDrop(null);
        Menu_ExportSAV.Click -= ClickExportSAV;
        Menu_ExportSAV.Click += HackExportSAV;
        Menu_Settings.Click += (_, _) => ApplyHackState();
        PB_Legal.VisibleChanged += (_, _) =>
        {
            if (hackLegalityOff && PB_Legal.Visible)
                PB_Legal.Visible = false;
        };
    }

    internal static bool IsHackInput(DropVerdict verdict, string? name) => verdict.Kind switch
    {
        DropKind.Bundle => name is not null && name.EndsWith(ProfileBundleReader.Extension, StringComparison.OrdinalIgnoreCase),
        var kind => kind is DropKind.Patch or DropKind.GbaRom or DropKind.GbRom or DropKind.NdsRom or DropKind.CtrImage or DropKind.Profile,
    };

    private static bool IsOpenFailure(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
        or NotSupportedException or System.Text.Json.JsonException or FormatException or IndexOutOfRangeException or OverflowException;

    private static DateTime StampOf(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return DateTime.MinValue;
        }
    }

    internal static DropVerdict? ClassifyHackFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var length = stream.Length;
            var take = (int)Math.Min(length, length <= DropClassifier.WholeFileBelow ? length : DropClassifier.HeadBytes);
            var head = new byte[take];
            stream.ReadExactly(head);
            return Classify(head, length, Path.GetFileName(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static DropVerdict? Classify(ReadOnlySpan<byte> head, long length, string? name)
    {
        try
        {
            return DropClassifier.Classify(head, length, name);
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private bool RouteHackDrop(string path)
    {
        if (!File.Exists(path) || ClassifyHackFile(path) is not { } verdict || !IsHackInput(verdict, Path.GetFileName(path)))
            return false;
        ShowHackDrop(path);
        return true;
    }

    private void ShowHackDrop(string? file)
    {
        hackPlainPath = null;
        string? save;
        string? key;
        using (var form = new SAV_HackDrop(HackStore, file))
        {
            if (form.ShowDialog(Visible ? this : null) != DialogResult.OK || form.SaveToOpen is null)
                return;
            save = form.SaveToOpen;
            key = form.KitKey;
        }
        OpenHackSaveFile(save, key);
    }

    private void OpenHackSaveFile(string path, string? kitKey)
    {
        byte[] bytes;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxHackSaveBytes)
            {
                WinFormsUtil.Error(IntakeText.NotASave, path);
                return;
            }
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WinFormsUtil.Error(MessageStrings.MsgFileInUse + path, ex);
            return;
        }
        var hint = kitKey is null ? null : new OpenHint { KitKey = kitKey, Answered = true };
        if (!OpenHackSave(bytes, path, hint))
            OpenFromPath(path);
    }

    private bool OpenHackFile(Memory<byte> input, string path)
    {
        hackPlainPath = null;
        var name = string.IsNullOrEmpty(path) ? null : Path.GetFileName(path);
        var span = (ReadOnlySpan<byte>)input.Span;
        var head = input.Length <= DropClassifier.WholeFileBelow ? span : span[..Math.Min(span.Length, DropClassifier.HeadBytes)];
        if (Classify(head, input.Length, name) is not { } verdict)
            return false;
        if (IsHackInput(verdict, name) && !string.IsNullOrEmpty(path) && File.Exists(path))
        {
            ShowHackDrop(path);
            return true;
        }
        if (verdict.Kind != DropKind.Save)
            return false;
        if (OpenHackSave(input, path, null))
            return true;
        if (!string.IsNullOrEmpty(path))
        {
            hackPlainPath = path;
            hackPlainStamp = StampOf(path);
        }
        return false;
    }

    private bool HackGuardOpen(SaveFile sav, string path)
    {
        if (hackOwnedOpen)
            return false;
        var plain = hackPlainPath;
        hackPlainPath = null;
        if (StateSession is { } state && ReferenceEquals(state.Save, sav))
            return HackGuardState(state);
        if (plain is not null && string.Equals(plain, path, StringComparison.Ordinal) && hackPlainStamp != DateTime.MinValue && StampOf(path) == hackPlainStamp)
            return false;
        if (string.IsNullOrWhiteSpace(path) || !sav.State.Exportable || !File.Exists(path))
            return !EndHackSession(out _) && KeepHackSave();
        byte[] bytes;
        try
        {
            if (new FileInfo(path).Length > MaxHackSaveBytes)
                return !EndHackSession(out _) && KeepHackSave();
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return !EndHackSession(out _) && KeepHackSave();
        }
        return OpenHackSave(bytes, path, null);
    }

    private bool KeepHackSave()
    {
        var held = C_SAV.SAV;
        if (IsHandleCreated)
            BeginInvoke((MethodInvoker)(() => held.State.Edited = true));
        return true;
    }

    private bool HackGuardState(SaveStateSession state)
    {
        if (!EndHackSession(out var replace))
            return KeepHackSave();
        if (state.Facet != SaveStateFacet.EmbeddedSave || state.Analysis.Container.EmbeddedSave is not { } embedded)
            return false;
        if (StateHackRefusal(embedded, null) is not { } why)
            return false;
        StateSession = null;
        WinFormsUtil.Alert("PKHaX does not open this save from inside the save state.", why, "Open the game's own save file instead.");
        AfterHackRefusal(replace);
        return true;
    }

    private string? StateHackRefusal(ReadOnlyMemory<byte> bytes, OpenHint? hint)
    {
        if (HackOpen.CfruContainer(bytes) is not null)
            return IntakeText.NativeNeedsKit;
        OpenResult result;
        try
        {
            result = HackOpen.Open(bytes, null, HackStore, null, (hint ?? new OpenHint()) with { Live = true, Language = CurrentLanguage });
        }
        catch (Exception ex) when (IsOpenFailure(ex))
        {
            HackOpen.Close();
            return $"PKHaX could not check it against the ROM hacks it keeps ({ex.Message}).";
        }
        try
        {
            if (result.Kind == OpenKind.Refused && result.Sentence == IntakeText.NotASave)
                return null;
            if (result.Kind == OpenKind.Ask)
            {
                var answer = AskWhichHack(result, "");
                if (answer is { NoKit: true })
                    return StateHackRefusal(bytes, answer);
                return answer is null ? "No game was chosen for it." : "It is a save of a ROM hack.";
            }
            if (result.Kind == OpenKind.Stock && result.Kit is null)
            {
                if (result.WriteRefusal is null)
                    return null;
                if (result.AsksUnmodified && result.UnmodifiedQuestion is { } question)
                {
                    return WinFormsUtil.Prompt(MessageBoxButtons.YesNo, question, $"Yes: {result.ConfirmLabel}.", "No: do not open it.") == DialogResult.Yes
                        ? null
                        : result.WriteRefusal;
                }
                return result.WriteRefusal;
            }
            if (result.Kit is { } kit)
                return $"It reads as a save of {kit.Kit.Name}, and PKHaX edits a ROM hack's save only from its own save file.";
            return result.WriteRefusal ?? result.Sentence ?? "It is a save of a ROM hack.";
        }
        finally
        {
            result.Dispose();
            HackOpen.Close();
        }
    }

    private bool OpenHackSave(ReadOnlyMemory<byte> input, string path, OpenHint? hint, bool ended = false)
    {
        if (!EndHackSession(out var replace))
            return KeepHackSave();
        replace |= ended;
        OpenResult result;
        try
        {
            result = HackOpen.Open(input, string.IsNullOrEmpty(path) ? null : path, HackStore, null, (hint ?? new OpenHint()) with { Language = CurrentLanguage });
        }
        catch (Exception ex) when (IsOpenFailure(ex))
        {
            HackOpen.Close();
            WinFormsUtil.Error("PKHaX could not check this save against the ROM hacks it keeps, so it is not opened.", ex.Message);
            AfterHackRefusal(replace);
            return true;
        }

        switch (result.Kind)
        {
            case OpenKind.Ask:
            {
                var answer = AskWhichHack(result, path);
                result.Dispose();
                if (answer is null)
                {
                    AfterHackRefusal(replace);
                    return true;
                }
                return OpenHackSave(input, path, answer, replace);
            }
            case OpenKind.Refused:
            {
                result.Dispose();
                if (result.Sentence == IntakeText.NotASave)
                {
                    if (replace)
                        AfterHackRefusal(true);
                    return false;
                }
                WinFormsUtil.Alert(result.Sentence ?? IntakeText.NotASave, NotesOf(result));
                AfterHackRefusal(replace);
                return true;
            }
            case OpenKind.Native:
            {
                using (result)
                {
                    using var editor = new SAV_HackEditor(result, path, input);
                    editor.ShowDialog(Visible ? this : null);
                }
                AfterHackRefusal(replace);
                return true;
            }
            default:
                return OpenHackStock(result, path, replace);
        }
    }

    private bool OpenHackStock(OpenResult result, string path, bool replace)
    {
        if (result.Kit is null && !result.AsksUnmodified && result.WriteRefusal is null)
        {
            result.Dispose();
            return false;
        }
        if (result.AsksUnmodified && result.UnmodifiedQuestion is { } question
            && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, question, $"Yes: {result.ConfirmLabel}.", "No: open it read-only.") == DialogResult.Yes)
            result.ConfirmUnmodified();
        if (result.Stock is not { } sav)
        {
            result.Dispose();
            AfterHackRefusal(replace);
            return true;
        }

        var before = C_SAV.SAV;
        hackOwnedOpen = true;
        try
        {
            OpenSAV(sav, path);
        }
        finally
        {
            hackOwnedOpen = false;
        }
        if (ReferenceEquals(C_SAV.SAV, before))
        {
            result.Dispose();
            HackOpen.Close();
            AfterHackRefusal(replace);
            return true;
        }

        hackSession = result;
        hackLegalityOff = result.Kit is not null && !result.ShowsLegality;
        if (result.OverlayActive)
            RebuildHackLists();
        ApplyHackState();
        if (result.WriteRefusal is { } why)
            WinFormsUtil.Alert(result.Kit is { } kit ? $"{kit.Kit.Name}: this save opens read-only." : "This save opens read-only.", result.Banner ?? why, NotesOf(result));
        return true;
    }

    private OpenHint? AskWhichHack(OpenResult result, string path)
    {
        var choices = result.Choices;
        var options = choices.Select(k => k.Kit.Summary).Append("None of these: open it as the plain game").ToList();
        var text = result.Sentence ?? IntakeText.Question;
        if (!string.IsNullOrEmpty(path))
            text += Environment.NewLine + Path.GetFileName(path);
        if (!this.TrySelectIndex(IntakeText.Title, text, options, out var index) || index < 0)
            return null;
        return index < choices.Count ? new OpenHint { KitKey = choices[index].Kit.Key, Answered = true } : new OpenHint { NoKit = true };
    }

    private bool EndHackSession(out bool replace)
    {
        replace = false;
        if (hackSession is not { } session)
            return true;
        var hadOverlay = session.OverlayActive;
        var tracked = hadOverlay || session.Kit is not null || !session.CanWrite;
        if (tracked && C_SAV.SAV.State.Edited)
        {
            var closing = session.Kit is { } kit ? $"Opening another save closes the {kit.Kit.Name} save." : "Opening another save closes this read-only save.";
            if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, closing, "Its changes that were not exported are lost. Continue?") != DialogResult.Yes)
                return false;
        }
        replace = tracked;
        hackSession = null;
        session.Dispose();
        HackOpen.Close();
        Menu_ExportSAV.ToolTipText = null;
        if (hackLegalityOff)
        {
            hackLegalityOff = false;
            C_SAV.FlagIllegal = Settings.Display.FlagIllegal;
            PB_Legal.Visible = !HaX;
        }
        if (tracked)
            C_SAV.SAV.State.Edited = false;
        if (hadOverlay)
            RebuildHackLists();
        return true;
    }

    private void AfterHackRefusal(bool replace)
    {
        if (!replace && C_SAV.SAV is not FakeSaveFile)
            return;
        hackOwnedOpen = true;
        try
        {
            LoadBlankSaveFile(Settings.Startup.DefaultSaveVersion);
        }
        finally
        {
            hackOwnedOpen = false;
        }
    }

    private void RebuildHackLists()
    {
        var sav = C_SAV.SAV;
        GameInfo.FilteredSources = new FilteredGameDataSource(sav, GameInfo.Sources, HaX);
        if (sav is FakeSaveFile || !PKME_Tabs.IsInitialized)
            return;
        var pk = PKME_Tabs.CurrentPKM.Clone();
        PKME_Tabs.ChangeLanguage(sav);
        PKME_Tabs.PopulateFields(pk);
    }

    private void ApplyHackState()
    {
        if (hackSession is not { } session)
            return;
        Menu_ExportSAV.Enabled = C_SAV.SAV.State.Exportable && session.CanWrite;
        Menu_ExportSAV.ToolTipText = session.WriteRefusal;
        C_SAV.FlagIllegal = !hackLegalityOff && Settings.Display.FlagIllegal;
        if (hackLegalityOff)
            PB_Legal.Visible = false;
        Text = GetProgramTitle(C_SAV.SAV) + (session.Kit is { } kit ? $" [{kit.Kit.Name}]" : "") + (session.CanWrite ? "" : " (read-only)");
    }

    private void HackExportSAV(object? sender, EventArgs e)
    {
        if (hackSession is { } session)
        {
            if (session.WriteRefusal is { } why)
            {
                WinFormsUtil.Alert("Nothing was written.", why);
                return;
            }
            if (session.Kit is not null && !BackupHackSave(C_SAV.SAV.Metadata.FilePath))
                return;
        }
        ClickExportSAV(sender, e);
        ApplyHackState();
    }

    private static bool BackupHackSave(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            WinFormsUtil.Error("Nothing was written: this hack save has no file on disk to back up first.");
            return false;
        }
        try
        {
            HackSaveBackup.Write(path, File.ReadAllBytes(path));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            WinFormsUtil.Error("Nothing was written: the backup of this hack save could not be made.", ex.Message);
            return false;
        }
    }

    private static string NotesOf(OpenResult result) => string.Join(Environment.NewLine, result.Notes.Take(8));
}

internal static class HackSaveBackup
{
    public static string Write(string path, ReadOnlySpan<byte> bytes)
    {
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full) ?? throw new IOException($"{full} has no folder to hold a backup");
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        for (var n = 1; n <= 99; n++)
        {
            var name = Path.Combine(dir, $"{Path.GetFileName(full)}.{stamp}{(n == 1 ? "" : $"-{n}")}.bak");
            if (File.Exists(name))
                continue;
            var created = false;
            try
            {
                using (var stream = new FileStream(name, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    stream.Write(bytes);
                    stream.Flush(true);
                }
                if (!File.ReadAllBytes(name).AsSpan().SequenceEqual(bytes))
                    throw new IOException($"the backup {name} does not read back as the save");
                return name;
            }
            catch (Exception ex) when (created && ex is IOException or UnauthorizedAccessException)
            {
                TryDelete(name);
                throw;
            }
        }
        throw new IOException($"no free backup name beside {full} for {stamp}");
    }

    public static void Replace(string path, byte[] bytes)
    {
        var temp = path + ".pkhax-new";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(true);
        }
        File.Move(temp, path, overwrite: true);
    }

    private static void TryDelete(string name)
    {
        try
        {
            File.Delete(name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
