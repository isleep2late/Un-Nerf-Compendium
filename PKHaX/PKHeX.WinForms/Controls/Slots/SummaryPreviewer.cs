using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

public sealed class SummaryPreviewer
{
    private readonly ToolTip ShowSet = new() { InitialDelay = 200, IsBalloon = false, AutoPopDelay = 32_767 };
    private readonly CryPlayer Cry = new();
    private readonly PokePreview Previewer = new();
    private CancellationTokenSource _source = new();
    private static HoverSettings Settings => Main.Settings.Hover;

    public void Show(Control pb, PKM pk, StorageSlotType type = StorageSlotType.None)
    {
        if (pk.Species == 0)
        {
            Clear();
            return;
        }

        var programLanguage = Language.GetLanguageValue(Main.Settings.Startup.Language);
        var cfg = Main.Settings.BattleTemplate;
        var settings = cfg.Hover.GetSettings(programLanguage, pk.Context);
        var localize = LegalityLocalizationSet.GetLocalization(programLanguage);
        var la = new LegalityAnalysis(pk, type);
        var ctx = LegalityLocalizationContext.Create(la, localize);

        if (Settings.HoverSlotShowPreview && Control.ModifierKeys != Keys.Alt)
        {
            UpdatePreview(pb, pk, settings, ctx); // PKHaX Gen-1 info is injected into the preview box itself (see PokePreview.Populate)
        }
        else if (Settings.HoverSlotShowText)
        {
            var text = GetPreviewText(pk, settings);
            if (!settings.Order.Contains(BattleTemplateToken.FirstLine))
            {
                var insert = GetPreviewText(pk, settings with { Order = [BattleTemplateToken.FirstLine] });
                text = insert + Environment.NewLine + text;
            }
            if (Settings.HoverSlotShowEncounter)
                text = AppendEncounterInfo(ctx, text);
            text = AppendG1HaxInfo(pk, text); // PKHaX: prepend Gen-1 sprite/type details
            ShowSet.SetToolTip(pb, text);
        }

        if (Settings.HoverSlotPlayCry)
            Cry.PlayCry(pk, pk.Context);
    }

    private void UpdatePreview(Control pb, PKM pk, in BattleTemplateExportSettings settings, in LegalityLocalizationContext ctx)
    {
        _source.Cancel();
        _source.Dispose(); // Properly dispose the previous CancellationTokenSource
        _source = new();
        UpdatePreviewPosition(new());
        Previewer.Populate(pk, settings, ctx);

        SetWindowState(Previewer, true);
        bool showFirst = !_isFirstShown;
        if (showFirst)
            _isFirstShown = true;
    }

    private static void SetWindowState(Form frm, bool visible)
    {
        try
        {
            const int SW_SHOWNOACTIVATE = 4;
            var state = visible ? SW_SHOWNOACTIVATE : 0;
            ShowWindowAsync(frm.Handle, state);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            static extern bool ShowWindowAsync(nint hWnd, int nCmdShow);
        }
        catch
        {
            // error handling
        }
    }

    private bool _isFirstShown;

    public void UpdatePreviewPosition(Point location)
    {
        var cLoc = Cursor.Position;
        var shift = Settings.PreviewCursorShift;
        cLoc.Offset(shift);
        Previewer.MoveForm(cLoc.X, cLoc.Y);
    }

    public void Show(Control pb, IEncounterInfo enc)
    {
        if (enc.Species == 0)
        {
            Clear();
            return;
        }

        if (Settings.HoverSlotShowText)
            ShowSet.SetToolTip(pb, GetPreviewText(enc, Settings.HoverSlotShowEncounterVerbose));
        if (Settings.HoverSlotPlayCry)
            Cry.PlayCry(enc, enc.Context);
    }

    public void Clear()
    {
        try
        {
            var token = _source.Token; // did the user move to another slot in time?
            var noToken = CancellationToken.None; // don't throw task canceled exceptions
            Task.Run(async () =>
            {
                if (!Previewer.IsHandleCreated || !_isFirstShown)
                    return; // not shown ever

                // Give a little bit of delay before hiding, assuming user is moving between slots. If they enter another, we'll cancel.

                await Task.Delay(50, noToken).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                    await Previewer.InvokeAsync(() => SetWindowState(Previewer, false), noToken); // hide
            }, noToken).ConfigureAwait(false);
        }
        catch
        {
            // Ignore.
        }
        ShowSet.RemoveAll();
        Cry.Stop();
    }

    // PKHaX: Gen-1 type byte -> name (Gen-1 byte values are not PKHeX's modern type indices).
    private static string G1TypeName(byte t) => t switch
    {
        0 => "Normal", 1 => "Fighting", 2 => "Flying", 3 => "Poison", 4 => "Ground", 5 => "Rock",
        7 => "Bug", 8 => "Ghost", 20 => "Fire", 21 => "Water", 22 => "Grass", 23 => "Electric",
        24 => "Psychic", 25 => "Ice", 26 => "Dragon", _ => $"0x{t:X2}",
    };

    private static string G1SpeciesName(byte internalIdx)
    {
        var names = GameInfo.Strings.specieslist;
        var nat = SpeciesConverter.GetNational1(internalIdx);
        return nat is >= 1 and <= 151 && nat < names.Length ? names[nat] : "(glitch)";
    }

    // PKHaX: build the Gen-1 hackmons info block (data species, desynced sprite, exact typing).
    public static string BuildG1HaxBlock(PK1 pk1)
    {
        var sb = new List<string>(3)
        {
            $"Data species: {G1SpeciesName(pk1.SpeciesInternal)} (0x{pk1.SpeciesInternal:X2})",
            $"Sprite: {G1SpeciesName(pk1.SpriteSpeciesInternal)} (0x{pk1.SpriteSpeciesInternal:X2}){(pk1.IsSpriteDesynced ? " [desynced]" : string.Empty)}",
            pk1.Type1 == pk1.Type2
                ? $"Type: {G1TypeName(pk1.Type1)} (mono)"
                : $"Type: {G1TypeName(pk1.Type1)} / {G1TypeName(pk1.Type2)} (dual)",
        };
        return string.Join(Environment.NewLine, sb);
    }

    private static string AppendG1HaxInfo(PKM pk, string text)
    {
        if (pk is not PK1 pk1)
            return text;
        var block = BuildG1HaxBlock(pk1);
        return string.IsNullOrEmpty(text) ? block : block + Environment.NewLine + Environment.NewLine + text;
    }

    public static string GetPreviewText(PKM pk, BattleTemplateExportSettings settings) => ShowdownParsing.GetLocalizedPreviewText(pk, settings);

    public static string AppendEncounterInfo(LegalityLocalizationContext la, string text)
    {
        var result = new List<string>(8);
        if (text.Length != 0) // add a blank line between the set and the encounter info if isn't already a blank line
        {
            result.Add(text);
            result.Add(string.Empty);
        }
        LegalityFormatting.AddEncounterInfo(la, result);
        return string.Join(Environment.NewLine, result);
    }

    private static string GetPreviewText(IEncounterInfo enc, bool verbose = false)
    {
        var lines = enc.GetTextLines(verbose, Main.CurrentLanguage);
        return string.Join(Environment.NewLine, lines);
    }

    public static string AppendLegalityHint(in LegalityLocalizationContext la, string line)
    {
        // Get the first illegal check result, and append the localization of it as the hint.
        // If all legal, return the input string unchanged.
        var analysis = la.Analysis;
        if (analysis.Valid)
            return line;

        foreach (var chk in analysis.Results)
        {
            if (chk.Valid)
                continue;
            var hint = la.Humanize(chk, verbose: true);
            return Join(line, hint);
        }

        for (var i = 0; i < analysis.Info.Moves.Length; i++)
        {
            var chk = analysis.Info.Moves[i];
            if (chk.Valid)
                continue;
            var hint = la.FormatMove(chk, i + 1, la.Analysis.Info.Entity.Context);
            return Join(line, hint);
        }

        for (var i = 0; i < analysis.Info.Relearn.Length; i++)
        {
            var chk = analysis.Info.Relearn[i];
            if (chk.Valid)
                continue;
            var hint = la.FormatMove(chk, i + 1, la.Analysis.Info.Entity.Context);
            return Join(line, hint);
        }

        return line;

        static string Join(string line, string hint)
        {
            if (hint.Length > 67)
                hint = hint[..67] + "...";
            return string.IsNullOrEmpty(line) ? hint : $"{line}{Environment.NewLine}{hint}";
        }
    }
}
