using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Expansion;
using PKHaX.Hacks.Saves.Gen3;

namespace PKHaX.Hacks.Saves;

public static class HackSaves
{
    public static IReadOnlyList<string> Variants { get; } = [.. CfruVariant.All.Select(v => v.Id), ExpansionSave.VariantId, RetailGen3Save.VariantId];

    public static bool Handles(string? variant) => variant is not null && Variants.Contains(variant, StringComparer.Ordinal);

    public static HackSave? TryOpen(ReadOnlyMemory<byte> data, string variant, WriteEvidence evidence, out string? why)
    {
        if (CfruVariant.ById(variant) is { } cfru)
            return CfruSave.TryOpen(data, evidence, out why, cfru.Id);
        if (variant == ExpansionSave.VariantId)
        {
            why = $"{variant} needs the hack's layout: open it with HackSaves.TryOpenExpansion and the ROM's GF header";
            return null;
        }
        if (variant == RetailGen3Save.VariantId)
            return RetailGen3Save.TryOpen(data, evidence, out why);
        why = $"no hack-native save codec handles '{variant}' yet ({string.Join(", ", Variants)})";
        return null;
    }

    public static HackSave? TryOpen(ReadOnlyMemory<byte> data, SaveShape shape, WriteEvidence evidence, out string? why) => TryOpen(data, shape, evidence, out why, out _);

    public static HackSave? TryOpen(ReadOnlyMemory<byte> data, SaveShape shape, WriteEvidence evidence, out string? why, out TrialResult trial)
    {
        trial = SectorTrial.Run(data, shape);
        why = trial.Refusal;
        if (why is not null || trial.Survivors.Count == 0)
        {
            why ??= "no layout of this hack fits the file";
            return null;
        }
        var capped = evidence == WriteEvidence.GameAccepted ? WriteEvidence.Confined : evidence;
        var saves = new List<HackSave>();
        foreach (var candidate in trial.Survivors)
        {
            if (Build(shape, candidate, capped, out why) is not { } save)
                return null;
            saves.Add(save);
        }
        why = null;
        var footer = trial.FooterBytes > 0 ? data[GbaSectorContainer.FileSize..] : ReadOnlyMemory<byte>.Empty;
        var result = saves.Count == 1 ? saves[0] : new TiedSave(saves, [.. trial.Survivors.Select(c => c.Name)]);
        foreach (var save in saves)
            save.Footer = footer;
        result.Footer = footer;
        return result;
    }

    private static HackSave? Build(SaveShape shape, TrialCandidate candidate, WriteEvidence evidence, out string? why)
    {
        why = null;
        var h = candidate.Header;
        if (shape.Family == SaveShape.Cfru)
            return CfruSave.FromTrial(shape, h, candidate.Container, evidence);
        if (candidate.PartyCodec.Id == Records.RecordCodecId.Pk3)
        {
            if (shape.Family == SaveShape.Retail)
                return RetailGen3Save.FromTrial(shape, candidate, evidence, out why);
            why = $"the hack's layout says {shape.Family}, but the save's party reads as retail pk3 records, which only the {SaveShape.Retail} family keeps";
            return null;
        }
        if (candidate.PartyCodec.Id is not (Records.RecordCodecId.Pk3x or Records.RecordCodecId.Pk3SoulGold96))
        {
            why = $"no hack save class reads {candidate.PartyCodec} party records";
            return null;
        }
        var money = shape.Money is { } m && m.Offset >= 0 && m.Offset + 4 <= h.SaveBlock1Size && (m.KeyOffset is not { } key || (key >= 0 && key + 4 <= h.SaveBlock2Size)) ? m : null;
        var layout = new ExpansionLayout(h.SaveBlock2Size, h.SaveBlock1Size, h.PartyCountOffset, h.PartyOffset, h.TrainerIdOffset, h.PlayerNameOffset, candidate.PartyCodec,
            money?.Offset, money?.KeyOffset, candidate.StorageSize, h.Shift) { Stride = candidate.Stride, Boxes = candidate.Boxes };
        return ExpansionSave.FromContainer(layout, candidate.Container, evidence);
    }

    public static HackSave? TryOpenExpansion(ReadOnlyMemory<byte> data, ExpansionLayout layout, WriteEvidence evidence, out string? why) =>
        ExpansionSave.TryOpen(data, layout, evidence, out why);
}
