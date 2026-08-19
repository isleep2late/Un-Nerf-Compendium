using System;
using System.Collections.Generic;

namespace PKHeX.Core;

/// PKHaX: everything editable that was found inside one emulator save state.
public sealed class SaveStateAnalysis
{
    public required SaveStateFile Container { get; init; }
    public SaveFile? EmbeddedSaveFile { get; private set; }
    public GBRamGame? GBParty { get; private set; }
    public List<RamParty> RamParties { get; } = [];
    public RamBattle? Battle { get; private set; }

    public bool HasAnything => EmbeddedSaveFile is not null || GBParty is not null || RamParties.Count > 0;

    public static SaveStateAnalysis? TryAnalyze(ReadOnlySpan<byte> input, string path)
    {
        if (!SaveStateFile.TryParse(input, path, out var container) || container is null)
            return null;
        var result = new SaveStateAnalysis { Container = container };
        if (container.EmbeddedSave is { } sav)
        {
            var copy = new byte[sav.Length];
            sav.CopyTo(copy, 0);
            var embedded = SaveUtil.GetSaveFile(copy, path);
            if (embedded is not null)
                result.EmbeddedSaveFile = embedded;
        }
        result.GBParty = GBRamGame.TryDetect(container);
        if (container.Console == StateConsole.GBA || (container.Console == StateConsole.GB && container.Kind is SaveStateKind.VbaM or SaveStateKind.RawMemory))
        {
            if (result.GBParty is null)
                result.RamParties.AddRange(RamPartyScan.FindGen3Parties(container));
        }
        if (container.Console == StateConsole.NDS)
            result.RamParties.AddRange(RamPartyScan.FindGen4Parties(container));
        if (container.Console == StateConsole.N3DS)
            result.RamParties.AddRange(RamPartyScan.FindGen67Parties(container));
        if (result.RamParties.Count > 0)
            result.Battle = RamBattleScan.FindBattle(container, result.RamParties);
        return result;
    }

    /// Writes the edited embedded save back into the container before serializing.
    public byte[] Serialize()
    {
        if (EmbeddedSaveFile is not null && Container.EmbeddedSave is not null)
        {
            var final = EmbeddedSaveFile.Write();
            if (final.Length == Container.EmbeddedSave.Length)
                Container.SetEmbeddedSave(final.Span);
        }
        return Container.Serialize();
    }
}
