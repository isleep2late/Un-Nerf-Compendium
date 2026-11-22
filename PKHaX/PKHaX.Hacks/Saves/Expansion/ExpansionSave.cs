using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;

namespace PKHaX.Hacks.Saves.Expansion;

public sealed record ExpansionLayout(
    int SaveBlock2Size,
    int SaveBlock1Size,
    int PartyCountOffset,
    int PartyOffset,
    int TrainerIdOffset,
    int PlayerNameOffset,
    RecordCodecKey Codec,
    int? MoneyOffset = null,
    int? MoneyKeyOffset = null,
    int StorageSize = ExpansionLayout.VanillaStorageSize,
    int HeaderShift = 0)
{
    public const int VanillaStorageSize = 0x83D0;
    public const int SectorData = SectionLayout.VanillaStride;
    public const int BoxesOffset = 4;
    public const int BoxSlots = 30;
    public const int BoxNameBytes = 9;

    public int Stride { get; init; } = SectionLayout.VanillaStride;

    public int? Boxes { get; init; }

    public int PartyRecordSize => Codec.Id == RecordCodecId.Pk3SoulGold96 ? Pk3SoulGold96Codec.Party : 100;

    public int BoxRecordSize => Codec.Id == RecordCodecId.Pk3SoulGold96 ? Pk3SoulGold96Codec.Box : Gen3Record.StoredSize;

    public int BoxCount => Boxes ?? Math.Max(0, (StorageSize - BoxesOffset) / StorageBytesPerBox(BoxRecordSize));

    public static int StorageBytesPerBox(int recordSize) => (BoxSlots * recordSize) + BoxNameBytes + 1;

    public static int StorageSizeFor(int boxes, int recordSize) => BoxesOffset + (boxes * StorageBytesPerBox(recordSize));

    public static ExpansionLayout? FromHeader(GfRomHeader header, RecordCodecKey codec, out string? why, int? moneyOffset = null, int? moneyKeyOffset = null, int stride = SectorData)
    {
        why = header.SaveBlock2Size < MinSaveBlock2 ? $"the GF header's SaveBlock2 size 0x{header.SaveBlock2Size:X} is below 0x{MinSaveBlock2:X}, so these bytes are not a save layout"
            : header.SaveBlock1Size < MinSaveBlock1 ? $"the GF header's SaveBlock1 size 0x{header.SaveBlock1Size:X} is below 0x{MinSaveBlock1:X}, so these bytes are not a save layout"
            : header.SaveBlock2Size > (uint)stride ? $"the GF header's SaveBlock2 size 0x{header.SaveBlock2Size:X} does not fit one 0x{stride:X} sector"
            : header.SaveBlock1Size > 4 * (uint)stride ? $"the GF header's SaveBlock1 size 0x{header.SaveBlock1Size:X} does not fit four 0x{stride:X} sectors"
            : (long)header.PartyOffset + (6 * (codec.Id == RecordCodecId.Pk3SoulGold96 ? Pk3SoulGold96Codec.Party : 100)) > header.SaveBlock1Size || (long)header.PartyCountOffset + 1 > header.PartyOffset ? "the GF header's party offsets fall outside SaveBlock1, or the party count is not before the party"
            : (long)header.PlayerNameOffset + 8 > header.SaveBlock2Size || (long)header.TrainerIdOffset + 4 > header.SaveBlock2Size ? "the GF header's trainer offsets fall outside SaveBlock2"
            : (long)header.PlayerNameOffset < (long)header.TrainerIdOffset + 4 && (long)header.TrainerIdOffset < (long)header.PlayerNameOffset + 8 ? "the GF header's player name and trainer id overlap"
            : codec.Id is not (RecordCodecId.Pk3x or RecordCodecId.Pk3SoulGold96) ? $"{codec} is not an expansion party record"
            : null;
        return why is null
            ? new ExpansionLayout((int)header.SaveBlock2Size, (int)header.SaveBlock1Size, (int)header.PartyCountOffset, (int)header.PartyOffset, (int)header.TrainerIdOffset, (int)header.PlayerNameOffset, codec, moneyOffset, moneyKeyOffset) { Stride = stride }
            : null;
    }

    public const uint MinSaveBlock2 = 0x100;
    public const uint MinSaveBlock1 = 0x1000;

    public static ExpansionLayout? FromRom(GbaImage rom, RecordCodecKey codec, out string? why, int? moneyOffset = null, int? moneyKeyOffset = null)
    {
        if (codec.Id == RecordCodecId.Pk3x && RhhRomHeader.Read(rom) is null)
        {
            why = "the image has no RHHEXP header, so it is not read as a pokeemerald-expansion hack with 100-byte pk3x records";
            return null;
        }
        var reasons = new List<string>();
        foreach (var shift in GfRomHeader.Shifts)
        {
            if (GfRomHeader.ReadAt(rom, shift) is not { } header)
            {
                reasons.Add($"shift {shift}: the image is too short for a GF header");
                continue;
            }
            if (FromHeader(header, codec, out var reason, moneyOffset, moneyKeyOffset) is { } layout)
            {
                why = null;
                return layout with { HeaderShift = shift };
            }
            reasons.Add($"shift {shift}: {reason}");
        }
        why = $"no GF header at 0x100 gives a save layout ({string.Join("; ", reasons)})";
        return null;
    }

    public SectionLayout Sections() => SectionLayout.FromBlockSizes(Stride == SectionLayout.VanillaStride ? "expansion" : $"expansion stride 0x{Stride:X}", SaveBlock2Size, SaveBlock1Size, StorageSize, Stride);
}

public sealed class ExpansionSave : HackSave
{
    public const string VariantId = "gen3-expansion/1";
    public const int PartySlots = 6;

    public static ChecksumPolicy Policy { get; } = ChecksumPolicy.Uniform($"0x{SectionLayout.VanillaStride:X} windows (DESIGN-5 M2)", SectionLayout.VanillaStride);

    private readonly GbaSectorContainer container;
    private readonly byte[] small;
    private readonly byte[] large;
    private readonly HackMon?[] party;
    private readonly Dictionary<(SaveBlock, int), BlockEdit> edits = [];
    private readonly List<BlockWindow> windows = [];

    private ExpansionSave(ExpansionLayout layout, GbaSectorContainer container, WriteEvidence evidence) : base(VariantId, container.File, evidence)
    {
        Layout = layout;
        this.container = container;
        small = container.Block(SaveBlock.Small);
        large = container.Block(SaveBlock.Large);
        party = ReadParty();
        boxes = ReadBoxes(out boxesReason);
    }

    private readonly HackBox[] boxes;
    private readonly string? boxesReason;
    private readonly Dictionary<int, string> partyRefused = [];

    public ExpansionLayout Layout { get; }
    public GbaSectorContainer Container => container;
    public string PolicyName => container.Policy.Name;

    public override IReadOnlyList<HackBox> Boxes => boxes;

    public override IReadOnlyDictionary<int, string> PartyRefused => partyRefused;

    public bool FromShape { get; private init; }

    internal static ExpansionSave FromContainer(ExpansionLayout layout, GbaSectorContainer container, WriteEvidence evidence) => new(layout, container, evidence) { FromShape = true };

    public static ExpansionSave? TryOpen(ReadOnlyMemory<byte> file, ExpansionLayout layout, WriteEvidence evidence, out string? why)
    {
        SectionLayout sections;
        try
        {
            sections = layout.Sections();
        }
        catch (ArgumentException ex)
        {
            why = ex.Message;
            return null;
        }
        var policy = layout.Stride == SectionLayout.VanillaStride ? Policy : ChecksumPolicy.Uniform($"0x{layout.Stride:X} windows", layout.Stride);
        var c = GbaSectorContainer.TryOpen(file, sections, policy, [GbaSectorContainer.RetailSignature], out why);
        return c is null ? null : new ExpansionSave(layout, c, evidence);
    }

    public byte PartyCountByte => large[Layout.PartyCountOffset];

    public int PartyCount => PartyCountByte <= PartySlots ? PartyCountByte : 0;

    public string? StructureProblem =>
        Layout.MoneyOffset is { } mo && mo + 4 > Layout.SaveBlock1Size ? $"the money offset 0x{mo:X} is outside SaveBlock1 (0x{Layout.SaveBlock1Size:X})"
        : Layout.MoneyKeyOffset is { } ko && ko + 4 > Layout.SaveBlock2Size ? $"the money key offset 0x{ko:X} is outside SaveBlock2 (0x{Layout.SaveBlock2Size:X})"
        : PartyCountByte > PartySlots ? $"the party count byte is {PartyCountByte}, above {PartySlots}, so this file is not read as an expansion save with these offsets"
        : container.ActiveSlot != (int)(container.Counter % 2) ? $"the newest complete slot (counter {container.Counter}) is not the one the game loads (counter % 2 = {container.Counter % 2})"
        : null;

    public override string? ReadOnlyReason => StructureProblem ?? base.ReadOnlyReason;

    public override IReadOnlyList<HackMon?> Party => party;

    public override HackTrainer Trainer => new(
        StringConverter3.GetString(small.AsSpan(Layout.PlayerNameOffset, 8), (int)LanguageID.English),
        BinaryPrimitives.ReadUInt16LittleEndian(small.AsSpan(Layout.TrainerIdOffset)),
        BinaryPrimitives.ReadUInt16LittleEndian(small.AsSpan(Layout.TrainerIdOffset + 2)),
        0);

    private uint MoneyKey => Layout.MoneyKeyOffset is { } k ? BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(k)) : 0;

    public override uint? Money => Layout.MoneyOffset is { } at && at + 4 <= Layout.SaveBlock1Size && (Layout.MoneyKeyOffset is not { } k || k + 4 <= Layout.SaveBlock2Size)
        ? BinaryPrimitives.ReadUInt32LittleEndian(large.AsSpan(at)) ^ MoneyKey
        : null;

    public override string? BoxesReason => boxesReason;

    private HackBox[] ReadBoxes(out string? reason)
    {
        var codec = RecordCodecs.For(Layout.Codec.Id, Layout.Codec.Version);
        var size = Layout.BoxRecordSize;
        var count = Layout.BoxCount;
        if (codec.StoredSize != size)
        {
            reason = $"{Layout.Codec} has no {size}-byte stored record, so the PC boxes are not read";
            return [];
        }
        if (count < 1)
        {
            reason = $"the storage block of 0x{Layout.StorageSize:X} bytes holds no whole box of {ExpansionLayout.BoxSlots} {size}-byte records";
            return [];
        }
        var storage = container.Block(SaveBlock.Storage);
        var result = new List<HackBox>();
        var problems = new List<string>();
        var origin = $"storage block + {ExpansionLayout.BoxesOffset}, {count} boxes of {ExpansionLayout.BoxSlots} {size}-byte {Layout.Codec} records (pokeemerald PokemonStorage)";
        for (var b = 0; b < count; b++)
        {
            var start = ExpansionLayout.BoxesOffset + (b * ExpansionLayout.BoxSlots * size);
            if (start + (ExpansionLayout.BoxSlots * size) > storage.Length)
            {
                problems.Add($"box {b + 1} would end at 0x{start + (ExpansionLayout.BoxSlots * size):X}, past the storage block's 0x{storage.Length:X} bytes");
                continue;
            }
            var slots = new HackMon?[ExpansionLayout.BoxSlots];
            var refused = new Dictionary<int, string>();
            for (var s = 0; s < ExpansionLayout.BoxSlots; s++)
            {
                var record = storage.AsSpan(start + (s * size), size);
                if (!record.ContainsAnyExcept((byte)0))
                    continue;
                slots[s] = codec.Decode(record, out var why);
                if (slots[s] is null && why != EmptySlot)
                    refused[s] = why ?? "refused";
            }
            result.Add(new HackBox(b + 1, origin, slots, refused));
        }
        reason = problems.Count > 0 ? string.Join("; ", problems) : null;
        return [.. result];
    }

    public const string EmptySlot = "empty slot";

    private HackMon?[] ReadParty()
    {
        var codec = RecordCodecs.For(Layout.Codec.Id, Layout.Codec.Version);
        var result = new HackMon?[PartySlots];
        for (var i = 0; i < PartyCount; i++)
        {
            result[i] = codec.Decode(large.AsSpan(Layout.PartyOffset + (i * Layout.PartyRecordSize), Layout.PartyRecordSize), out var why);
            if (result[i] is null)
                partyRefused[i] = why ?? "refused";
        }
        return result;
    }

    public override void SetPartySlot(int slot, HackMon mon)
    {
        if (slot < 0 || slot >= PartyCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"the party holds {PartyCount} Pokemon; filling an empty slot is not supported yet");
        var bytes = Layout.Codec.Id == RecordCodecId.Pk3SoulGold96 ? Pk3SoulGold96Encoder.Encode(mon) : Pk3xEncoder.Encode(mon);
        if (bytes.Length != Layout.PartyRecordSize)
            throw new FaithfulWriteException($"a party slot takes a {Layout.PartyRecordSize}-byte party record, got {bytes.Length} bytes");
        var offset = Layout.PartyOffset + (slot * Layout.PartyRecordSize);
        edits[(SaveBlock.Large, offset)] = new BlockEdit(SaveBlock.Large, offset, bytes);
        windows.Add(new BlockWindow(SaveBlock.Large, offset, Layout.PartyRecordSize));
        bytes.CopyTo(large.AsSpan(offset));
        party[slot] = RecordCodecs.For(Layout.Codec.Id, Layout.Codec.Version).Decode(bytes, out _);
    }

    public override string? BoxWriteRefusal(int box) =>
        boxes.Length == 0 ? $"{Variant}: {boxesReason ?? "no PC box is read from this save"}"
        : !boxes.Any(b => b.Number == box) ? $"{Variant} has boxes 1 to {boxes.Length}"
        : null;

    public override void SetBoxSlot(int box, int slot, HackMon mon)
    {
        var index = Array.FindIndex(boxes, b => b.Number == box);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(box), box, BoxWriteRefusal(box));
        if (slot < 0 || slot >= ExpansionLayout.BoxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"a box holds {ExpansionLayout.BoxSlots} Pokemon");
        var held = boxes[index];
        var current = held.Slots[slot] ?? throw new FaithfulWriteException(held.Refused.TryGetValue(slot, out var refused)
            ? $"box {box} slot {slot + 1} holds a record the codec refuses ({refused}), so it is not written"
            : $"box {box} slot {slot + 1} is empty; filling an empty slot is not supported yet");
        if (!mon.Raw.Span.SequenceEqual(current.Raw.Span))
            throw new FaithfulWriteException($"the edit for box {box} slot {slot + 1} does not start from the bytes now in that slot, so it is not written there");
        var bytes = Layout.Codec.Id == RecordCodecId.Pk3SoulGold96 ? Pk3SoulGold96Encoder.Encode(mon) : Pk3xEncoder.Encode(mon);
        var size = Layout.BoxRecordSize;
        if (bytes.Length != size)
            throw new FaithfulWriteException($"a box slot takes a {size}-byte stored record, got {bytes.Length} bytes");
        var offset = ExpansionLayout.BoxesOffset + ((((box - 1) * ExpansionLayout.BoxSlots) + slot) * size);
        edits[(SaveBlock.Storage, offset)] = new BlockEdit(SaveBlock.Storage, offset, bytes);
        windows.Add(new BlockWindow(SaveBlock.Storage, offset, size));
        ((HackMon?[])held.Slots)[slot] = RecordCodecs.For(Layout.Codec.Id, Layout.Codec.Version).Decode(bytes, out _);
    }

    public override void SetMoney(uint money)
    {
        if (Layout.MoneyOffset is not { } at || Money is null)
            throw new FaithfulWriteException("where this hack keeps money is not measured yet, or the profile's offset is outside the block (DESIGN-5 M3)");
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, money ^ MoneyKey);
        edits[(SaveBlock.Large, at)] = new BlockEdit(SaveBlock.Large, at, bytes);
        windows.Add(new BlockWindow(SaveBlock.Large, at, 4));
        bytes.CopyTo(large.AsSpan(at));
    }

    public override WriteResult Plan() => FaithfulWriter.Patch(container, edits.Values.ToList(), windows);
}
