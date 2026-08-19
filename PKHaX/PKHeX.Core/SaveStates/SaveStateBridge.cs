using System;

namespace PKHeX.Core;

/// PKHaX: which editable view of a save state the user picked.
public enum SaveStateFacet
{
    EmbeddedSave,
    GBParty,
    RamParty,
}

/// PKHaX: bridges a save state's live-RAM party into a regular SaveFile so the full editor can edit it,
/// then syncs the edits back into the state.
public sealed class SaveStateSession
{
    public required SaveStateAnalysis Analysis { get; init; }
    public required SaveStateFacet Facet { get; init; }
    public required SaveFile Save { get; init; }
    public RamParty? Party { get; init; }
    public string StatePath => Analysis.Container.FilePath;

    public static SaveStateSession? CreateEmbedded(SaveStateAnalysis analysis)
    {
        if (analysis.EmbeddedSaveFile is not { } sav)
            return null;
        return new SaveStateSession { Analysis = analysis, Facet = SaveStateFacet.EmbeddedSave, Save = sav };
    }

    public static SaveStateSession? CreateGBParty(SaveStateAnalysis analysis)
    {
        if (analysis.GBParty is not { } game)
            return null;
        var version = game.Layout.Kind switch
        {
            GBRamGameKind.Gen1RedBlue => GameVersion.BU,
            GBRamGameKind.Gen1Yellow => GameVersion.YW,
            GBRamGameKind.Gen2GoldSilver => GameVersion.GD,
            _ => GameVersion.C,
        };
        var sav = BlankSaveFile.Get(version, "PKHAX");
        int count = Math.Min(game.PartyCount, 6);
        for (int i = 0; i < count; i++)
            sav.SetPartySlotAtIndex(game.GetSlot(i), i);
        return new SaveStateSession { Analysis = analysis, Facet = SaveStateFacet.GBParty, Save = sav };
    }

    public static SaveStateSession? CreateRamParty(SaveStateAnalysis analysis, RamParty party)
    {
        var version = party.Generation switch
        {
            3 => GameVersion.E,
            5 => GameVersion.B2,
            6 or 7 => VersionFor67(party),
            _ => GameVersion.Pt,
        };
        var sav = BlankSaveFile.Get(version, "PKHAX");
        for (int i = 0; i < party.Count; i++)
            sav.SetPartySlotAtIndex(party.GetSlot(i), i);
        return new SaveStateSession { Analysis = analysis, Facet = SaveStateFacet.RamParty, Save = sav, Party = party };
    }

    private static GameVersion VersionFor67(RamParty party)
    {
        var version = party.GetSlot(0).Version;
        return version switch
        {
            GameVersion.X or GameVersion.Y or GameVersion.AS or GameVersion.OR
                or GameVersion.SN or GameVersion.MN or GameVersion.US or GameVersion.UM => version,
            _ => party.Generation == 6 ? GameVersion.X : GameVersion.US,
        };
    }

    /// Pulls the edits out of the SaveFile, pushes them into the container, and returns the rebuilt state file.
    public byte[] WriteBack()
    {
        switch (Facet)
        {
            case SaveStateFacet.EmbeddedSave:
                break;
            case SaveStateFacet.GBParty:
            {
                var game = Analysis.GBParty!;
                int count = Math.Min(Save.PartyCount, 6);
                game.PartyCount = count;
                for (int i = 0; i < count; i++)
                {
                    var pk = Save.GetPartySlotAtIndex(i);
                    if (pk.Species != 0)
                        game.SetSlot(i, pk);
                }
                break;
            }
            case SaveStateFacet.RamParty:
            {
                var party = Party!;
                for (int i = 0; i < party.Count; i++)
                {
                    var pk = Save.GetPartySlotAtIndex(i);
                    if (pk.Species != 0)
                        party.SetSlot(i, pk);
                }
                break;
            }
        }
        return Analysis.Serialize();
    }
}
