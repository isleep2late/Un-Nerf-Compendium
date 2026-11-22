using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;

namespace PKHaX.Hacks.Analysis;

public static class Gen3ShapeFinder
{
    public const string FireRedHeaderSource = "pokefirered's save layout (SaveBlock2 0xF24, SaveBlock1 0x3D68, party count +0x34, party +0x38)";
    public const string CfruLayoutSource = "CFRU src/save.c: 0xFF0 bytes per section, the storage block kept at 0x83D0";

    public static ShapeHeader FireRedHeader { get; } = new(0, 0xF24, 0x3D68, 0x34, 0x38, 0xA, 0);

    public static RecordCodecKey Compact58 { get; } = new(RecordCodecId.Pk3CfruCompact58, null);
    public static RecordCodecKey CfruPlain { get; } = new(RecordCodecId.Pk3CfruPlain, null);
    public static RecordCodecKey RetailPk3 { get; } = new(RecordCodecId.Pk3, null);
    public static RecordCodecKey SoulGold96 { get; } = new(RecordCodecId.Pk3SoulGold96, null);

    public static SaveShape Derive(ReadOnlyMemory<byte> rom)
    {
        var sha1 = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(rom.Span));
        if (!HackDataCache.IsGba(rom.Span) || rom.Length is < 0x200 or > GbaImage.MaxLength)
            return SaveShape.Unknown(sha1, "the image is not a Game Boy Advance cartridge, and only Gen 3 save layouts are derived in this build");
        return Derive(new GbaImage(rom));
    }

    public static SaveShape Derive(GbaImage rom)
    {
        var notes = new List<string>();
        var headers = Headers(rom, notes);
        var rhh = RhhRomHeader.Read(rom);
        var table = rhh is null && CfruBoxTable.HasRun(rom) ? CfruBoxTable.Read(rom) : null;
        if (rhh is not null)
        {
            if (headers.Count == 0)
                return SaveShape.Unknown(rom.Sha1, $"the image has an RHHEXP header ({rhh.Version}) but no GF header at 0x{GfRomHeader.At:X} reads as a save layout at shifts {string.Join(", ", GfRomHeader.Shifts)}");
            var codecs = new List<RecordCodecKey>();
            if (ExpansionVersions.TryNormalize($"{rhh.Major}.{rhh.Minor}", out var version, out var whyNot))
                codecs.Add(new RecordCodecKey(RecordCodecId.Pk3x, version));
            else
                notes.Add($"pk3x records are not tried: {whyNot}");
            codecs.Add(SoulGold96);
            return new SaveShape
            {
                RomSha1 = rom.Sha1,
                Family = SaveShape.Expansion,
                FamilyTier = ShapeTier.Derived,
                FamilyOrigin = $"RHHEXP header {rhh.Version} at ROM 0x{rhh.Offset:X}",
                Headers = headers,
                PartyCodecCandidates = codecs,
                LayoutTier = ShapeTier.Derived,
                Signatures = [GbaSectorContainer.RetailSignature],
                Notes = notes,
            };
        }
        if (table is not null)
        {
            var located = table.Runs.Where(r => r.Located && r.Area is not null).ToList();
            var fromHeader = headers.Count > 0;
            if (!fromHeader)
                notes.Add($"no GF header reads as a save layout, so the party offsets are {FireRedHeaderSource}");
            return new SaveShape
            {
                RomSha1 = rom.Sha1,
                Family = SaveShape.Cfru,
                FamilyTier = ShapeTier.Derived,
                FamilyOrigin = $"{CfruBoxTable.Source}: {table.BoxCount} box pointers at ROM 0x{table.Offset:X}",
                Headers = fromHeader ? headers : [FireRedHeader],
                PartyCodec = CfruPlain,
                BoxCodec = Compact58,
                PartyCodecCandidates = [CfruPlain],
                FixedStride = SectionLayout.CfruStride,
                FixedStorageSize = CfruRamMap.FireRed10.StorageSize,
                LayoutTier = ShapeTier.Family,
                BoxSources = [.. located.Select(r => new ShapeBoxSource(r.FirstBox, r.Count, r.Area.GetValueOrDefault(), r.Offset, r.Origin, Tier(r.Tier), r.NeedsSaveCheck, r.WritableFromRomAlone))],
                BoxNotes = [.. table.Notes, .. table.Runs.Where(r => !r.Located).Select(r => $"boxes {r.FirstBox}-{r.LastBox}: {r.Origin}")],
                Signatures = [GbaSectorContainer.RetailSignature],
                Notes = [.. notes, CfruLayoutSource],
            };
        }
        if (headers.Count == 0)
            return SaveShape.Unknown(rom.Sha1, $"no RHHEXP header, no CFRU box pointer table, and no GF header at 0x{GfRomHeader.At:X} that reads as a save layout");
        return new SaveShape
        {
            RomSha1 = rom.Sha1,
            Family = SaveShape.Retail,
            FamilyTier = ShapeTier.Derived,
            FamilyOrigin = $"GF header at ROM 0x{GfRomHeader.At:X}, no RHHEXP header and no CFRU box pointer table",
            Headers = headers,
            PartyCodecCandidates = [RetailPk3, SoulGold96],
            LayoutTier = ShapeTier.Derived,
            Signatures = [GbaSectorContainer.RetailSignature],
            Notes = notes,
        };
    }

    private static ShapeTier Tier(CfruRunTier tier) => tier switch
    {
        CfruRunTier.Derived => ShapeTier.Derived,
        CfruRunTier.Family => ShapeTier.Family,
        _ => ShapeTier.Unknown,
    };

    public static IReadOnlyList<ShapeHeader> Headers(GbaImage rom) => Headers(rom, []);

    private static List<ShapeHeader> Headers(GbaImage rom, List<string> notes)
    {
        var result = new List<ShapeHeader>();
        foreach (var h in GfRomHeader.ReadAll(rom))
        {
            if (HeaderProblem(h) is { } problem)
            {
                notes.Add($"GF header at shift {h.Shift} is not used: {problem}");
                continue;
            }
            result.Add(new ShapeHeader(h.Shift, (int)h.SaveBlock2Size, (int)h.SaveBlock1Size, (int)h.PartyCountOffset, (int)h.PartyOffset, (int)h.TrainerIdOffset, (int)h.PlayerNameOffset));
        }
        return result;
    }

    public static string? HeaderProblem(GfRomHeader h) =>
        !h.SaneSizes ? "the block sizes do not read as a save layout"
        : h.SaveBlock2Size > SectionLayout.PayloadCapacity ? $"SaveBlock2 0x{h.SaveBlock2Size:X} does not fit one section of 0x{SectionLayout.PayloadCapacity:X}"
        : h.SaveBlock1Size > 4 * SectionLayout.PayloadCapacity ? $"SaveBlock1 0x{h.SaveBlock1Size:X} does not fit four sections of 0x{SectionLayout.PayloadCapacity:X}"
        : h.SaveBlock2Size % 4 != 0 || h.SaveBlock1Size % 4 != 0 ? "a block size is not a multiple of 4"
        : h.PartyCountOffset >= h.PartyOffset ? "the party count is not before the party"
        : (long)h.PlayerNameOffset + 8 > h.SaveBlock2Size || (long)h.TrainerIdOffset + 4 > h.SaveBlock2Size ? "the trainer offsets fall outside SaveBlock2"
        : null;

    public static bool WordInRom(ReadOnlySpan<byte> rom, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        ReadOnlySpan<byte> needle = buffer;
        for (var at = 0; at + 4 <= rom.Length;)
        {
            var hit = rom[at..].IndexOf(needle);
            if (hit < 0)
                return false;
            if ((at + hit) % 4 == 0)
                return true;
            at += hit + 1;
        }
        return false;
    }

    public static SaveShape? AcceptSignature(SaveShape shape, GbaImage rom, ReadOnlyMemory<byte> save, out string? why)
    {
        var file = GbaSectorContainer.TrimFooter(save);
        if (GbaSectorContainer.UniformSignature(file.Span) is not { } signature)
        {
            why = $"the {GbaSectorContainer.SlotSectors} sectors of the save's newest slot do not carry one signature, so no second signature is taken from this file";
            return null;
        }
        return AcceptSignatureValue(shape, rom, signature, out why);
    }

    public static uint? UnlistedSignature(SaveShape shape, ReadOnlyMemory<byte> save)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var file = GbaSectorContainer.TrimFooter(save).Span;
        if (!shape.Known || shape.Signatures.Count == 0 || file.Length != GbaSectorContainer.FileSize)
            return null;
        if (GbaSectorContainer.NewestSectionIds(file, shape.Signatures) is not null)
            return null;
        return GbaSectorContainer.UniformSignature(file) is { } signature && !shape.Signatures.Contains(signature) ? signature : null;
    }

    public static SaveShape? AcceptSignatureValue(SaveShape shape, GbaImage rom, uint signature, out string? why)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(rom);
        if (shape.Signatures.Contains(signature))
        {
            why = null;
            return shape;
        }
        if (!string.Equals(shape.RomSha1, rom.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            why = "the image is not the one this save layout was derived from";
            return null;
        }
        if (!WordInRom(rom.Span, signature))
        {
            why = $"the save's sectors carry 0x{signature:X8}, and that value does not occur word-aligned in this hack's ROM, so it is not taken as the hack's signature";
            return null;
        }
        why = null;
        return shape with { Signatures = [.. shape.Signatures, signature], Notes = [.. shape.Notes, $"signature 0x{signature:X8}: uniform in a save's newest slot and present word-aligned in the ROM"] };
    }
}
