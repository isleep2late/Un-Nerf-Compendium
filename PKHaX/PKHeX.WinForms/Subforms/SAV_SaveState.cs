using System;
using System.Drawing;
using System.Windows.Forms;
using PKHeX.Core;

namespace PKHeX.WinForms;

/// PKHaX: shown when an emulator save state is opened; lets the user pick which facet to edit.
public sealed class SAV_SaveState : Form
{
    public SaveStateSession? Result { get; private set; }

    public SAV_SaveState(SaveStateAnalysis analysis)
    {
        var container = analysis.Container;
        Text = $"Save State — {System.IO.Path.GetFileName(container.FilePath)}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(460, 260);

        var info = new Label { AutoSize = false };
        info.SetBounds(12, 12, 436, 40);
        info.Text = $"Recognized: {Describe(container.Kind)} ({container.Console}). " +
            "Pick what to edit; changes are written back into the state file.";
        Controls.Add(info);

        int y = 60;
        if (analysis.EmbeddedSaveFile is { } sav)
        {
            var b = MakeButton($"Game save — full editor (boxes, items, trainer)\n{sav.GetType().Name} · OT {sav.OT} · party of {sav.PartyCount}", y);
            b.Click += (_, _) => Pick(SaveStateSession.CreateEmbedded(analysis));
            y += 56;
        }
        if (analysis.GBParty is { } game)
        {
            var b = MakeButton($"Live party (RAM) — {game.Layout.Name}, {game.PartyCount} in party", y);
            b.Click += (_, _) => Pick(SaveStateSession.CreateGBParty(analysis));
            y += 56;
        }
        if (analysis.GBParty is { HasBattleTypes: true, IsBattleActive: true } battle)
        {
            var b = MakeButton("Battle typing — edit the active battle's volatile types", y);
            b.Click += (_, _) =>
            {
                using var dlg = new SAV_GBBattleTypes(analysis, battle);
                dlg.ShowDialog();
            };
            y += 56;
        }
        foreach (var party in analysis.RamParties)
        {
            var local = party;
            var b = MakeButton($"Live party (RAM) — Gen {party.Generation}, {party.Count} Pokémon at 0x{party.Offset:X}", y);
            b.Click += (_, _) => Pick(SaveStateSession.CreateRamParty(analysis, local));
            y += 56;
        }
        if (y == 60)
        {
            var none = new Label { AutoSize = false };
            none.SetBounds(12, y, 436, 60);
            none.Text = "No editable Pokémon data was found in this state. " +
                "Get a party first (or save in-game so the state carries a save file), then re-save the state.";
            Controls.Add(none);
            y += 66;
        }

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(360, y + 6, 88, 28);
        Controls.Add(cancel);
        CancelButton = cancel;
        ClientSize = new Size(460, y + 46);
    }

    private Button MakeButton(string text, int y)
    {
        var b = new Button { Text = text, TextAlign = ContentAlignment.MiddleLeft };
        b.SetBounds(12, y, 436, 50);
        Controls.Add(b);
        return b;
    }

    private void Pick(SaveStateSession? session)
    {
        if (session is null)
        {
            WinFormsUtil.Error("Could not open this part of the state.");
            return;
        }
        Result = session;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string Describe(SaveStateKind kind) => kind switch
    {
        SaveStateKind.MgbaPng => "mGBA save state",
        SaveStateKind.MgbaRaw => "mGBA save state (raw)",
        SaveStateKind.Bess => "BESS save state (BGB / SameBoy / Emulicious)",
        SaveStateKind.VbaM => "VisualBoyAdvance-M save state",
        SaveStateKind.DeSmuME => "DeSmuME save state",
        SaveStateKind.MelonDS => "melonDS save state",
        _ => "raw memory dump",
    };
}
