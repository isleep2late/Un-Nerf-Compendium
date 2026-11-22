using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;

namespace PKHaX.Hacks.Saves.Cfru;

public enum CfruBoxArea
{
    Storage,
    Sectors30And31,
    SaveBlock1,
    SaveBlock2,
}

public sealed record CfruBoxSource(int FirstBox, int Count, CfruBoxArea Area, int Offset, string Origin, bool Measured = true)
{
    public int LastBox => FirstBox + Count - 1;
}

public sealed record CfruVariant(string Id, uint Signature, IReadOnlyList<uint> AcceptedSignatures, bool AutoDetect, int PartyCountOffset, int PartyOffset, int? MoneyOffset, int? MoneyKeyOffset, IReadOnlyList<CfruBoxSource> BoxSources)
{
    public const uint UnboundSignature = 0x01121999;
    public const int FireRedSecurityKey = 0xF20;
    public const int BoxSlots = 30;
    public const string CfruSource = "CFRU src/pokemon_storage_system.c sPokemonBoxPtrs";

    public static readonly IReadOnlyList<CfruBoxSource> CfruBoxes =
    [
        new(1, 19, CfruBoxArea.Storage, 4, CfruSource + " (ORIGINAL_BOX_POKEMON_RAM, gPokemonStorage + 4)"),
        new(20, 3, CfruBoxArea.Sectors30And31, 0xB0C, CfruSource + " (BOX_20_RAM 0x203CB44, sector 30 loaded at 0x203C038)"),
    ];

    public static readonly IReadOnlyList<CfruBoxSource> RadicalRedBoxes =
    [
        CfruBoxes[0] with { Origin = CfruBoxes[0].Origin + "; not measured on a Radical Red save", Measured = false },
        CfruBoxes[1] with { Origin = CfruBoxes[1].Origin + "; Radical Red's own source is not public, so this is not measured on a Radical Red save", Measured = false },
    ];

    public static readonly CfruVariant Unbound = new("gen3-cfru-unbound/1", UnboundSignature, [UnboundSignature, GbaSectorContainer.RetailSignature], true, 0x34, 0x38, 0x290, FireRedSecurityKey,
    [
        .. CfruBoxes,
        new(23, 2, CfruBoxArea.SaveBlock1, 0x1F08, CfruSource + " (BOX_23_RAM 0x2027434 = gSaveBlock1 0x202552C + 0x1F08; CFRU's public configuration, measured on Unbound 2.1.1.1 against the ROM's own box pointer table)"),
        new(25, 1, CfruBoxArea.SaveBlock2, 0xB0, CfruSource + " (BOX_25_RAM 0x2024638 = gSaveBlock2 0x2024588 + 0xB0; CFRU's public configuration, measured on Unbound 2.1.1.1 against the ROM's own box pointer table)"),
    ]);

    public static readonly CfruVariant RadicalRed = new("gen3-cfru-rr/1", GbaSectorContainer.RetailSignature, [GbaSectorContainer.RetailSignature], false, 0x34, 0x38, null, null, RadicalRedBoxes);

    public int BoxCount => BoxSources.Sum(s => s.Count);

    public static IReadOnlyList<CfruVariant> All { get; } = [Unbound, RadicalRed];

    public static CfruVariant? ById(string? id) => All.FirstOrDefault(v => v.Id == id);
}

public sealed class CfruSave : HackSave
{
    public const int PartySlots = 6;

    public static IReadOnlyList<ChecksumPolicy> CandidatePolicies { get; } =
    [
        ChecksumPolicy.FromLayout(SectionLayout.Cfru),
        ChecksumPolicy.Uniform("0xFF4 windows, section 0 at 0xADC (DESIGN-5 4.2 Unbound row)", 0xFF4, new Dictionary<int, int> { [0] = 0xADC }),
    ];

    private readonly GbaSectorContainer container;
    private readonly byte[] small;
    private readonly byte[] large;
    private readonly HackMon?[] party;
    private readonly Dictionary<(SaveBlock, int), BlockEdit> edits = [];

    public const string ShapeVariantId = "gen3-cfru/1";

    private readonly string? shapeReason;

    public bool FromShape { get; }

    internal static CfruSave FromTrial(SaveShape shape, ShapeHeader header, GbaSectorContainer container, WriteEvidence evidence)
    {
        var notes = shape.BoxNotes.Where(n => n.Contains("not located", StringComparison.Ordinal)).ToList();
        var sources = CheckedSources(shape, container, notes);
        if (sources.Count == 0)
            notes.Add("no PC box is located in this save");
        var money = shape.Money is { } m && m.Tier != ShapeTier.Unknown && m.Offset >= 0 && m.Offset + 4 <= header.SaveBlock1Size && (m.KeyOffset is not { } key || (key >= 0 && key + 4 <= header.SaveBlock2Size)) ? m : null;
        var variant = new CfruVariant(ShapeVariantId, container.Signature, shape.Signatures, false, header.PartyCountOffset, header.PartyOffset, money?.Offset, money?.KeyOffset, sources);
        return new CfruSave(variant, container, evidence, notes.Count > 0 ? string.Join("; ", notes) : null, true);
    }

    private static List<CfruBoxSource> CheckedSources(SaveShape shape, GbaSectorContainer container, List<string> notes)
    {
        var codec = RecordCodecs.For(RecordCodecId.Pk3CfruCompact58);
        var ordered = shape.BoxSources.OrderBy(b => b.FirstBox).ToList();
        var kept = new List<CfruBoxSource>();
        var renumber = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var source = ordered[i];
            if (source.Tier == ShapeTier.Unknown)
                continue;
            if (source.NeedsSaveCheck)
            {
                var area = AreaOf(container, source.Area);
                var why = (long)source.Offset + CfruBoxTable.BoxBytes > area.Length
                    ? $"the save's {source.Area} area holds 0x{area.Length:X} bytes, short of the box at 0x{source.Offset:X}"
                    : CfruBoxTable.SlotProblem(codec, area.AsSpan(source.Offset, CfruBoxTable.BoxBytes));
                if (why is not null)
                {
                    var leading = i == 0 && ordered.Count > 1;
                    notes.Add(leading
                        ? $"the lone pointer before the box table is not a box ({why}); it is left out and the boxes are numbered from the next entry"
                        : $"box {source.FirstBox - renumber}: not located, a lone pointer is a box only when all {CfruVariant.BoxSlots} slots in the save are empty or plausible, and {why}");
                    if (leading)
                        renumber = source.Count;
                    continue;
                }
            }
            kept.Add(new CfruBoxSource(source.FirstBox - renumber, source.Count, source.Area, source.Offset, source.Origin, source.WritableFromRomAlone));
        }
        return kept;
    }

    private static byte[] AreaOf(GbaSectorContainer container, CfruBoxArea area) => area switch
    {
        CfruBoxArea.Storage => container.Block(SaveBlock.Storage),
        CfruBoxArea.SaveBlock1 => container.Block(SaveBlock.Large),
        CfruBoxArea.SaveBlock2 => container.Block(SaveBlock.Small),
        _ => [.. container.RawSector(30), .. container.RawSector(31)],
    };

    private CfruSave(CfruVariant variant, GbaSectorContainer container, WriteEvidence evidence, string? shapeReason = null, bool fromShape = false) : base(variant.Id, container.File, evidence)
    {
        this.shapeReason = shapeReason;
        FromShape = fromShape;
        VariantInfo = variant;
        this.container = container;
        small = container.Block(SaveBlock.Small);
        large = container.Block(SaveBlock.Large);
        party = ReadParty();
        boxes = ReadBoxes(out boxesReason);
    }

    private readonly HackBox[] boxes;
    private readonly string? boxesReason;

    public override IReadOnlyList<HackBox> Boxes => boxes;

    public CfruVariant VariantInfo { get; }
    public GbaSectorContainer Container => container;
    public string PolicyName => container.Policy.Name;

    public static CfruSave? TryOpen(ReadOnlyMemory<byte> file, WriteEvidence evidence, out string? why, string? variantId = null)
    {
        var wanted = variantId is null ? CfruVariant.All.Where(v => v.AutoDetect).ToList() : CfruVariant.All.Where(v => v.Id == variantId).ToList();
        if (wanted.Count == 0)
        {
            why = $"'{variantId}' is not a CFRU save variant ({string.Join(", ", CfruVariant.All.Select(v => v.Id))})";
            return null;
        }
        var reasons = new List<string>();
        foreach (var variant in wanted)
        {
            if (variant.AcceptedSignatures.Contains(GbaSectorContainer.RetailSignature) && Vanilla(file) is { } vanilla)
            {
                reasons.Add($"{variant.Id}: the file checksums as a vanilla {vanilla} save, so it is not read as CFRU");
                continue;
            }
            foreach (var policy in CandidatePolicies)
            {
                if (GbaSectorContainer.TryOpen(file, SectionLayout.Cfru, policy, variant.AcceptedSignatures, out var w) is { } c)
                {
                    if (variant.Signature != GbaSectorContainer.RetailSignature && !CarriesSignature(file.Span, variant.Signature))
                    {
                        reasons.Add($"{variant.Id} ({policy.Name}): no save sector carries the hack's signature 0x{variant.Signature:X8}");
                        continue;
                    }
                    why = null;
                    return new CfruSave(variant, c, evidence);
                }
                reasons.Add($"{variant.Id} ({policy.Name}): {w}");
            }
        }
        why = string.Join("; ", reasons);
        return null;
    }

    public static string? Vanilla(ReadOnlyMemory<byte> file)
    {
        foreach (var layout in new[] { SectionLayout.FireRedLeafGreen, SectionLayout.Emerald, SectionLayout.RubySapphire })
        {
            if (GbaSectorContainer.TryOpen(file, layout, ChecksumPolicy.FromLayout(layout), [GbaSectorContainer.RetailSignature], out _) is not null)
                return layout.Name;
        }
        return null;
    }

    private static bool CarriesSignature(ReadOnlySpan<byte> file, uint signature)
    {
        for (var sector = 0; sector < 2 * SectionLayout.SectionCount; sector++)
        {
            if (BinaryPrimitives.ReadUInt32LittleEndian(file[((sector * SectionLayout.SectorSize) + GbaSectorContainer.SignatureOffset)..]) == signature)
                return true;
        }
        return false;
    }

    public byte PartyCountByte => large[VariantInfo.PartyCountOffset];

    public int PartyCount => PartyCountByte <= PartySlots ? PartyCountByte : 0;

    public string? StructureProblem =>
        PartyCountByte > PartySlots ? $"the party count byte is {PartyCountByte}, above {PartySlots}, so this file is not read as a {Variant} save"
        : container.ActiveSlot != (int)(container.Counter % 2) ? $"the newest complete slot ({(container.ActiveSlot == 0 ? "A" : "B")}, counter {container.Counter}) is not the one the game loads (counter % 2 = {container.Counter % 2})"
        : null;

    public override string? ReadOnlyReason => StructureProblem ?? base.ReadOnlyReason;

    public override IReadOnlyList<HackMon?> Party => party;

    public override HackTrainer Trainer => new(
        StringConverter3.GetString(small.AsSpan(0, 8), (int)LanguageID.English),
        BinaryPrimitives.ReadUInt16LittleEndian(small.AsSpan(0xA)),
        BinaryPrimitives.ReadUInt16LittleEndian(small.AsSpan(0xC)),
        small[8]);

    private uint MoneyKey => VariantInfo.MoneyKeyOffset is { } k ? BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(k)) : 0;

    public override uint? Money => VariantInfo.MoneyOffset is { } at ? BinaryPrimitives.ReadUInt32LittleEndian(large.AsSpan(at)) ^ MoneyKey : null;

    public override string? BoxesReason => (shapeReason, boxesReason) switch
    {
        (null, _) => boxesReason,
        (_, null) => shapeReason,
        _ => $"{shapeReason}; {boxesReason}",
    };

    private byte[] Area(CfruBoxArea area) => area switch
    {
        CfruBoxArea.Storage => container.Block(SaveBlock.Storage),
        CfruBoxArea.SaveBlock1 => large,
        CfruBoxArea.SaveBlock2 => small,
        _ => [.. container.RawSector(30), .. container.RawSector(31)],
    };

    private HackBox[] ReadBoxes(out string? reason)
    {
        var codec = RecordCodecs.For(RecordCodecId.Pk3CfruCompact58);
        var size = Pk3CfruCompact58Codec.Size;
        var result = new List<HackBox>();
        var problems = new List<string>();
        foreach (var source in VariantInfo.BoxSources)
        {
            var area = Area(source.Area);
            for (var b = 0; b < source.Count; b++)
            {
                var start = source.Offset + (b * CfruVariant.BoxSlots * size);
                if (start + (CfruVariant.BoxSlots * size) > area.Length)
                {
                    problems.Add($"box {source.FirstBox + b} would end at 0x{start + (CfruVariant.BoxSlots * size):X}, past the {source.Area} area's 0x{area.Length:X} bytes");
                    continue;
                }
                var slots = new HackMon?[CfruVariant.BoxSlots];
                var refused = new Dictionary<int, string>();
                for (var s = 0; s < CfruVariant.BoxSlots; s++)
                {
                    var record = area.AsSpan(start + (s * size), size);
                    if (BinaryPrimitives.ReadUInt16LittleEndian(record[0x1C..]) == 0 || !record.ContainsAnyExcept((byte)0xFF))
                        continue;
                    slots[s] = codec.Decode(record, out var why);
                    if (slots[s] is null)
                        refused[s] = why ?? "refused";
                }
                result.Add(new HackBox(source.FirstBox + b, source.Origin, slots, refused));
            }
        }
        reason = problems.Count > 0 ? string.Join("; ", problems) : null;
        return [.. result];
    }

    private readonly Dictionary<int, string> partyRefused = [];

    public override IReadOnlyDictionary<int, string> PartyRefused => partyRefused;

    private HackMon?[] ReadParty()
    {
        var codec = RecordCodecs.For(RecordCodecId.Pk3CfruPlain);
        var result = new HackMon?[PartySlots];
        for (var i = 0; i < PartyCount; i++)
        {
            result[i] = codec.Decode(PartyRecord(i), out var why);
            if (result[i] is null)
                partyRefused[i] = why ?? "refused";
        }
        return result;
    }

    public override string? BoxWriteRefusal(int box) =>
        VariantInfo.BoxSources.FirstOrDefault(s => box >= s.FirstBox && box <= s.LastBox) is not { } source ? $"{Variant} has boxes 1 to {VariantInfo.BoxCount}"
        : !source.Measured ? $"{Variant}: boxes {source.FirstBox}-{source.LastBox} are not written until their map is measured on a {Variant} save"
        : null;

    private ReadOnlySpan<byte> PartyRecord(int slot) => large.AsSpan(VariantInfo.PartyOffset + (slot * Gen3Record.PartySize), Gen3Record.PartySize);

    public override void SetPartySlot(int slot, HackMon mon)
    {
        if (slot < 0 || slot >= PartyCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"the party holds {PartyCount} Pokemon; filling an empty slot is not supported yet");
        var bytes = Pk3CfruPlainEncoder.Encode(mon);
        var offset = VariantInfo.PartyOffset + (slot * Gen3Record.PartySize);
        edits[(SaveBlock.Large, offset)] = new BlockEdit(SaveBlock.Large, offset, bytes);
        windows.Add(new BlockWindow(SaveBlock.Large, offset, Gen3Record.PartySize));
        party[slot] = RecordCodecs.For(RecordCodecId.Pk3CfruPlain).Decode(bytes, out _);
    }

    public override void SetBoxSlot(int box, int slot, HackMon mon)
    {
        var index = Array.FindIndex(boxes, b => b.Number == box);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(box), box, $"{Variant} has boxes 1 to {VariantInfo.BoxCount}{(boxesReason is null ? "" : $"; {boxesReason}")}");
        if (slot < 0 || slot >= CfruVariant.BoxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"a box holds {CfruVariant.BoxSlots} Pokemon");
        var held = boxes[index];
        var current = held.Slots[slot] ?? throw new FaithfulWriteException(held.Refused.TryGetValue(slot, out var refused)
            ? $"box {box} slot {slot + 1} holds a record the codec refuses ({refused}), so it is not written"
            : $"box {box} slot {slot + 1} is empty; filling an empty slot is not supported yet");
        if (!mon.Raw.Span.SequenceEqual(current.Raw.Span))
            throw new FaithfulWriteException($"the edit for box {box} slot {slot + 1} does not start from the bytes now in that slot, so it is not written there");
        var source = VariantInfo.BoxSources.First(s => box >= s.FirstBox && box <= s.LastBox);
        if (!source.Measured)
            throw new FaithfulWriteException($"{Variant}: boxes {source.FirstBox}-{source.LastBox} are read from {source.Origin}; they are not written until that map is measured on a {Variant} save");
        var bytes = Pk3CfruCompact58Encoder.Encode(mon);
        var size = Pk3CfruCompact58Codec.Size;
        var offset = source.Offset + ((box - source.FirstBox) * CfruVariant.BoxSlots * size) + (slot * size);
        switch (source.Area)
        {
            case CfruBoxArea.Storage:
                AddEdit(SaveBlock.Storage, offset, bytes);
                break;
            case CfruBoxArea.SaveBlock1:
                AddEdit(SaveBlock.Large, offset, bytes);
                bytes.CopyTo(large.AsSpan(offset));
                break;
            case CfruBoxArea.SaveBlock2:
                AddEdit(SaveBlock.Small, offset, bytes);
                bytes.CopyTo(small.AsSpan(offset));
                break;
            default:
                var first = Math.Clamp(GbaSectorContainer.RawPayload - offset, 0, size);
                if (first > 0)
                    raw[(30, offset)] = new RawEdit(30, offset, bytes[..first]);
                if (first < size)
                    raw[(31, Math.Max(0, offset - GbaSectorContainer.RawPayload))] = new RawEdit(31, Math.Max(0, offset - GbaSectorContainer.RawPayload), bytes[first..]);
                break;
        }
        ((HackMon?[])held.Slots)[slot] = RecordCodecs.For(RecordCodecId.Pk3CfruCompact58).Decode(bytes, out _);
    }

    private void AddEdit(SaveBlock block, int offset, byte[] bytes)
    {
        edits[(block, offset)] = new BlockEdit(block, offset, bytes);
        windows.Add(new BlockWindow(block, offset, bytes.Length));
    }

    private readonly Dictionary<(int, int), RawEdit> raw = [];

    public override void SetMoney(uint money)
    {
        if (VariantInfo.MoneyOffset is not { } at)
            throw new FaithfulWriteException($"{Variant}: where this hack keeps money is not known yet");
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, money ^ MoneyKey);
        edits[(SaveBlock.Large, at)] = new BlockEdit(SaveBlock.Large, at, bytes);
        windows.Add(new BlockWindow(SaveBlock.Large, at, 4));
        bytes.CopyTo(large.AsSpan(at));
    }

    private readonly List<BlockWindow> windows = [];

    public override WriteResult Plan() => FaithfulWriter.Patch(container, edits.Values.ToList(), windows, raw.Values.ToList());
}
