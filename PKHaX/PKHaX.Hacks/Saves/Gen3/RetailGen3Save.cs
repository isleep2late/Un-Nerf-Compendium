using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHeX.Core;

namespace PKHaX.Hacks.Saves.Gen3;

public sealed record RetailLayout(string Name, SectionLayout Sections, int PartyCountOffset, int PartyOffset, int TrainerIdOffset, int PlayerNameOffset, int Boxes)
{
    public int BoxesOffset { get; init; } = RetailGen3Save.BoxesOffset;
    public int? GenderOffset { get; init; }
    public int? MoneyOffset { get; init; }
    public int? MoneyKeyOffset { get; init; }
}

public sealed class RetailGen3Save : HackSave
{
    public const string VariantId = "gen3-legacy/1";
    public const int PartySlots = 6;
    public const int BoxSlots = 30;
    public const int BoxesOffset = 4;
    public const int VanillaBoxes = 14;
    public const string EmptySlot = "empty slot";

    public const string Source = "pokefirered include/global.h:327-333 (SaveBlock2: playerName 0x0, playerGender 0x8, playerTrainerId 0xA), :770-773 (SaveBlock1: playerPartyCount 0x34, playerParty 0x38); pokeemerald include/global.h:999-1001 (playerPartyCount 0x234, playerParty 0x238); pokefirered include/pokemon_storage_system.h:44-50 (PokemonStorage: boxes after the currentBox byte, padded to +4, since boxNames sit at 0x8344 = 4 + 14 x 30 x 80); include/save.h:8, :15, :24 (3968 data bytes per sector, signature 0x08012025, 14 sectors per slot)";

    public static RetailLayout FireRedLeafGreen { get; } = new("FireRed/LeafGreen", SectionLayout.FireRedLeafGreen, 0x34, 0x38, 0xA, 0, VanillaBoxes) { GenderOffset = 8 };

    public static RetailLayout Emerald { get; } = new("Emerald", SectionLayout.Emerald, 0x234, 0x238, 0xA, 0, VanillaBoxes) { GenderOffset = 8 };

    public static IReadOnlyList<RetailLayout> Listed { get; } = [FireRedLeafGreen, Emerald];

    private readonly GbaSectorContainer container;
    private readonly byte[] small;
    private readonly byte[] large;
    private readonly byte[] storage;
    private readonly HackMon?[] party;
    private readonly Dictionary<int, string> partyRefused = [];
    private readonly HackBox[] boxes;
    private readonly string? boxesReason;
    private readonly Dictionary<(SaveBlock, int), BlockEdit> edits = [];
    private readonly List<BlockWindow> windows = [];

    private RetailGen3Save(RetailLayout layout, GbaSectorContainer container, WriteEvidence evidence, bool fromShape) : base(VariantId, container.File, Capped(evidence))
    {
        Layout = layout;
        FromShape = fromShape;
        this.container = container;
        small = container.Block(SaveBlock.Small);
        large = container.Block(SaveBlock.Large);
        storage = container.Block(SaveBlock.Storage);
        party = ReadParty();
        boxes = ReadBoxes(out boxesReason);
    }

    private static WriteEvidence Capped(WriteEvidence evidence) => evidence == WriteEvidence.GameAccepted ? WriteEvidence.Confined : evidence;

    public RetailLayout Layout { get; }
    public bool FromShape { get; }
    public GbaSectorContainer Container => container;
    public string PolicyName => container.Policy.Name;

    public static RetailGen3Save? TryOpen(ReadOnlyMemory<byte> file, WriteEvidence evidence, out string? why)
    {
        var body = GbaSectorContainer.TrimFooter(file);
        var footer = GbaSectorContainer.FooterLength(file) > 0 ? file[GbaSectorContainer.FileSize..] : ReadOnlyMemory<byte>.Empty;
        var opened = new List<RetailGen3Save>();
        var reasons = new List<string>();
        foreach (var layout in Listed)
        {
            if (TryOpen(body, layout, evidence, out var reason) is { } save)
                opened.Add(save);
            else
                reasons.Add($"{layout.Name}: {reason}");
        }
        if (opened.Count == 0)
        {
            why = $"no retail Gen 3 section layout checksums this file ({string.Join("; ", reasons)})";
            return null;
        }
        var pick = opened.Count == 1 ? opened[0] : Settle(opened);
        if (pick is null)
        {
            why = $"both the {string.Join(" and the ", opened.Select(s => s.Layout.Name))} section layouts checksum this file and read its party alike, so it is not known which one the game wrote";
            return null;
        }
        if (ReferenceEquals(pick.Layout, Emerald) && pick.IsRubySapphire)
        {
            why = $"this file is a Ruby/Sapphire save: SaveBlock2+0x{FireRedMarkAt:X} holds 0x{BinaryPrimitives.ReadUInt32LittleEndian(pick.small.AsSpan(FireRedMarkAt)):X} and nothing is stored past SaveBlock2+0x{RubySapphireEnd:X}, and the {VariantId} reader opens only the FireRed/LeafGreen and Emerald layouts ({GameSource})";
            return null;
        }
        why = null;
        pick.Footer = footer;
        return pick;
    }

    public const string SettleSource = "pokefirered src/save.c:182 (a sector is zeroed before its section is copied in, so a FireRed file also checksums under Emerald's longer sections); PKHeX.Core Saves/Util/SaveUtil.cs:302-315 (FireRed/LeafGreen keep the fixed 1 at SaveBlock2+0xAC, Emerald its encryption key)";

    public const int FireRedMarkAt = 0xAC;

    public const int RubySapphireEnd = 0x890;

    public const int EmeraldSmallEnd = 0xF2C;

    public const string GameSource = "PKHeX.Core Saves/Util/SaveUtil.cs:302-320 GetVersionG3SAV (SaveBlock2+0xAC: 1 is FireRed/LeafGreen, 0 is Ruby/Sapphire, otherwise Emerald when any byte from 0x890 to 0xF2C is set, else Ruby/Sapphire)";

    public bool IsRubySapphire
    {
        get
        {
            if (small.Length < FireRedMarkAt + 4)
                return false;
            var mark = BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(FireRedMarkAt));
            if (mark == 1)
                return false;
            if (mark == 0)
                return true;
            var end = Math.Min(small.Length, EmeraldSmallEnd);
            return end <= RubySapphireEnd || !small.AsSpan(RubySapphireEnd, end - RubySapphireEnd).ContainsAnyExcept((byte)0);
        }
    }

    private static RetailGen3Save? Settle(IReadOnlyList<RetailGen3Save> opened)
    {
        var scored = opened.Select(s => (Save: s, Read: s.Party.Count(p => p is not null), Refused: s.PartyRefused.Count + (s.PartyCountByte > PartySlots ? PartySlots : 0))).ToList();
        var best = scored.OrderByDescending(x => x.Read).ThenBy(x => x.Refused).ToList();
        if (best[0].Read != best[1].Read || best[0].Refused != best[1].Refused)
            return best[0].Save;
        var marked = opened.Where(s => s.small.Length >= FireRedMarkAt + 4 && (BinaryPrimitives.ReadUInt32LittleEndian(s.small.AsSpan(FireRedMarkAt)) == 1) == ReferenceEquals(s.Layout, FireRedLeafGreen)).ToList();
        return marked.Count == 1 ? marked[0] : null;
    }

    public static RetailGen3Save? TryOpen(ReadOnlyMemory<byte> file, RetailLayout layout, WriteEvidence evidence, out string? why)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var body = GbaSectorContainer.TrimFooter(file);
        var c = GbaSectorContainer.TryOpen(body, layout.Sections, ChecksumPolicy.FromLayout(layout.Sections), [GbaSectorContainer.RetailSignature], out why);
        if (c is null)
            return null;
        if (Problem(layout, c) is { } problem)
        {
            why = problem;
            return null;
        }
        return new RetailGen3Save(layout, c, evidence, false);
    }

    internal static RetailGen3Save? FromTrial(SaveShape shape, TrialCandidate candidate, WriteEvidence evidence, out string? why)
    {
        var h = candidate.Header;
        var money = shape.Money is { } m && m.Tier != ShapeTier.Unknown && m.Offset >= 0 && m.Offset + 4 <= h.SaveBlock1Size && (m.KeyOffset is not { } key || (key >= 0 && key + 4 <= h.SaveBlock2Size)) ? m : null;
        var layout = new RetailLayout($"GF header shift {h.Shift}, stride 0x{candidate.Stride:X}", candidate.Container.Layout, h.PartyCountOffset, h.PartyOffset, h.TrainerIdOffset, h.PlayerNameOffset,
            candidate.Boxes > 0 ? candidate.Boxes : VanillaBoxes)
        {
            GenderOffset = h.PlayerNameOffset + 8,
            MoneyOffset = money?.Offset,
            MoneyKeyOffset = money?.KeyOffset,
        };
        if (Problem(layout, candidate.Container) is { } problem)
        {
            why = problem;
            return null;
        }
        why = null;
        return new RetailGen3Save(layout, candidate.Container, evidence, true);
    }

    private static string? Problem(RetailLayout layout, GbaSectorContainer c)
    {
        var smallSize = c.Layout.BlockSize(SaveBlock.Small);
        var largeSize = c.Layout.BlockSize(SaveBlock.Large);
        var storageSize = c.Layout.BlockSize(SaveBlock.Storage);
        return layout.PartyCountOffset < 0 || layout.PartyOffset <= layout.PartyCountOffset || (long)layout.PartyOffset + (PartySlots * Gen3Record.PartySize) > largeSize
                ? $"the party offsets (count +0x{layout.PartyCountOffset:X}, party +0x{layout.PartyOffset:X}) do not fit SaveBlock1 of 0x{largeSize:X} bytes"
            : layout.PlayerNameOffset < 0 || layout.TrainerIdOffset < 0 || layout.PlayerNameOffset + 8 > smallSize || layout.TrainerIdOffset + 4 > smallSize
                ? $"the trainer offsets do not fit SaveBlock2 of 0x{smallSize:X} bytes"
            : layout.Boxes < 1 || layout.BoxesOffset < 0 || (long)layout.BoxesOffset + ((long)layout.Boxes * BoxSlots * Gen3Record.StoredSize) > storageSize
                ? $"{layout.Boxes} boxes of {BoxSlots} {Gen3Record.StoredSize}-byte records at +{layout.BoxesOffset} do not fit the storage block of 0x{storageSize:X} bytes"
            : null;
    }

    public byte PartyCountByte => large[Layout.PartyCountOffset];

    public int PartyCount => PartyCountByte <= PartySlots ? PartyCountByte : 0;

    public string? StructureProblem =>
        Layout.MoneyOffset is { } mo && mo + 4 > large.Length ? $"the money offset 0x{mo:X} is outside SaveBlock1 (0x{large.Length:X})"
        : Layout.MoneyKeyOffset is { } ko && ko + 4 > small.Length ? $"the money key offset 0x{ko:X} is outside SaveBlock2 (0x{small.Length:X})"
        : PartyCountByte > PartySlots ? $"the party count byte is {PartyCountByte}, above {PartySlots}, so this file is not read as a {VariantId} save with these offsets"
        : partyRefused.Count > 0 ? $"party slot {partyRefused.Keys.Min() + 1} does not read as a retail pk3 record ({partyRefused[partyRefused.Keys.Min()]}), so this save opens read-only"
        : container.ActiveSlot != (int)(container.Counter % 2) ? $"the newest complete slot (counter {container.Counter}) is not the one the game loads (counter % 2 = {container.Counter % 2})"
        : null;

    public override string? ReadOnlyReason => StructureProblem ?? base.ReadOnlyReason;

    public override IReadOnlyList<HackMon?> Party => party;

    public override IReadOnlyDictionary<int, string> PartyRefused => partyRefused;

    public override HackTrainer Trainer => new(
        StringConverter3.GetString(small.AsSpan(Layout.PlayerNameOffset, 8), (int)LanguageID.English),
        BinaryPrimitives.ReadUInt16LittleEndian(small.AsSpan(Layout.TrainerIdOffset)),
        BinaryPrimitives.ReadUInt16LittleEndian(small.AsSpan(Layout.TrainerIdOffset + 2)),
        Layout.GenderOffset is { } g && g < small.Length ? small[g] : (byte)0);

    private uint MoneyKey => Layout.MoneyKeyOffset is { } k ? BinaryPrimitives.ReadUInt32LittleEndian(small.AsSpan(k)) : 0;

    public override uint? Money => Layout.MoneyOffset is { } at && at + 4 <= large.Length && (Layout.MoneyKeyOffset is not { } k || k + 4 <= small.Length)
        ? BinaryPrimitives.ReadUInt32LittleEndian(large.AsSpan(at)) ^ MoneyKey
        : null;

    public override string? BoxesReason => boxesReason;

    public override IReadOnlyList<HackBox> Boxes => boxes;

    private HackMon?[] ReadParty()
    {
        var codec = RecordCodecs.For(RecordCodecId.Pk3);
        var result = new HackMon?[PartySlots];
        for (var i = 0; i < PartyCount; i++)
        {
            result[i] = codec.Decode(large.AsSpan(Layout.PartyOffset + (i * Gen3Record.PartySize), Gen3Record.PartySize), out var why);
            if (result[i] is null)
                partyRefused[i] = why ?? "refused";
        }
        return result;
    }

    private HackBox[] ReadBoxes(out string? reason)
    {
        var codec = RecordCodecs.For(RecordCodecId.Pk3);
        var size = Gen3Record.StoredSize;
        var origin = $"storage block + {Layout.BoxesOffset}, {Layout.Boxes} boxes of {BoxSlots} {size}-byte pk3 records (pokefirered PokemonStorage)";
        var result = new HackBox[Layout.Boxes];
        for (var b = 0; b < Layout.Boxes; b++)
        {
            var start = Layout.BoxesOffset + (b * BoxSlots * size);
            var slots = new HackMon?[BoxSlots];
            var refused = new Dictionary<int, string>();
            for (var s = 0; s < BoxSlots; s++)
            {
                var record = storage.AsSpan(start + (s * size), size);
                if (!record.ContainsAnyExcept((byte)0))
                    continue;
                slots[s] = codec.Decode(record, out var why);
                if (slots[s] is null && why != EmptySlot)
                    refused[s] = why ?? "refused";
            }
            result[b] = new HackBox(b + 1, origin, slots, refused);
        }
        reason = null;
        return result;
    }

    public override void SetPartySlot(int slot, HackMon mon)
    {
        ArgumentNullException.ThrowIfNull(mon);
        if (slot < 0 || slot >= PartyCount)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"the party holds {PartyCount} Pokemon; filling an empty slot is not supported yet");
        var current = party[slot] ?? throw new FaithfulWriteException(partyRefused.TryGetValue(slot, out var refused)
            ? $"party slot {slot + 1} holds a record the codec refuses ({refused}), so it is not written"
            : $"party slot {slot + 1} is empty; filling an empty slot is not supported yet");
        if (!mon.Raw.Span.SequenceEqual(current.Raw.Span))
            throw new FaithfulWriteException($"the edit for party slot {slot + 1} does not start from the bytes now in that slot, so it is not written there");
        var bytes = Pk3Encoder.Encode(mon);
        if (bytes.Length != Gen3Record.PartySize)
            throw new FaithfulWriteException($"a party slot takes a {Gen3Record.PartySize}-byte party record, got {bytes.Length} bytes");
        var offset = Layout.PartyOffset + (slot * Gen3Record.PartySize);
        edits[(SaveBlock.Large, offset)] = new BlockEdit(SaveBlock.Large, offset, bytes);
        windows.Add(new BlockWindow(SaveBlock.Large, offset, Gen3Record.PartySize));
        bytes.CopyTo(large.AsSpan(offset));
        party[slot] = RecordCodecs.For(RecordCodecId.Pk3).Decode(bytes, out _);
    }

    public override string? BoxWriteRefusal(int box) => boxes.Any(b => b.Number == box) ? null : $"{Variant} has boxes 1 to {boxes.Length}";

    public override void SetBoxSlot(int box, int slot, HackMon mon)
    {
        ArgumentNullException.ThrowIfNull(mon);
        var index = Array.FindIndex(boxes, b => b.Number == box);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(box), box, BoxWriteRefusal(box));
        if (slot < 0 || slot >= BoxSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, $"a box holds {BoxSlots} Pokemon");
        var held = boxes[index];
        var current = held.Slots[slot] ?? throw new FaithfulWriteException(held.Refused.TryGetValue(slot, out var refused)
            ? $"box {box} slot {slot + 1} holds a record the codec refuses ({refused}), so it is not written"
            : $"box {box} slot {slot + 1} is empty; filling an empty slot is not supported yet");
        if (!mon.Raw.Span.SequenceEqual(current.Raw.Span))
            throw new FaithfulWriteException($"the edit for box {box} slot {slot + 1} does not start from the bytes now in that slot, so it is not written there");
        var bytes = Pk3Encoder.Encode(mon);
        if (bytes.Length != Gen3Record.StoredSize)
            throw new FaithfulWriteException($"a box slot takes a {Gen3Record.StoredSize}-byte stored record, got {bytes.Length} bytes");
        var offset = Layout.BoxesOffset + ((((box - 1) * BoxSlots) + slot) * Gen3Record.StoredSize);
        edits[(SaveBlock.Storage, offset)] = new BlockEdit(SaveBlock.Storage, offset, bytes);
        windows.Add(new BlockWindow(SaveBlock.Storage, offset, Gen3Record.StoredSize));
        bytes.CopyTo(storage.AsSpan(offset));
        ((HackMon?[])held.Slots)[slot] = RecordCodecs.For(RecordCodecId.Pk3).Decode(bytes, out _);
    }

    public override void SetMoney(uint money)
    {
        if (Layout.MoneyOffset is not { } at || Money is null)
            throw new FaithfulWriteException("where this hack keeps money is not known for its retail save container");
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, money ^ MoneyKey);
        edits[(SaveBlock.Large, at)] = new BlockEdit(SaveBlock.Large, at, bytes);
        windows.Add(new BlockWindow(SaveBlock.Large, at, 4));
        bytes.CopyTo(large.AsSpan(at));
    }

    public override WriteResult Plan() => FaithfulWriter.Patch(container, edits.Values.ToList(), windows);
}
