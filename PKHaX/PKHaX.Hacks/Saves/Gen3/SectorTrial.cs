using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Expansion;

namespace PKHaX.Hacks.Saves.Gen3;

public enum TrialRule
{
    SectionSizes,
    UniformStride,
    FullWindows,
}

public sealed record TrialCandidate(ShapeHeader Header, RecordCodecKey PartyCodec, int Stride, int StorageSize, int Boxes, TrialRule Rule, GbaSectorContainer Container)
{
    public int Refused { get; init; }
    public int Decoded { get; init; }
    public int Witnesses { get; init; }
    public string? Dropped { get; init; }

    public string Name => $"GF header shift {Header.Shift}, {PartyCodec} party, stride 0x{Stride:X}, storage 0x{StorageSize:X}, {Container.Policy.Name}";
}

public sealed record TrialResult(IReadOnlyList<TrialCandidate> Checksummed, IReadOnlyList<TrialCandidate> Survivors, string? Refusal, int? SectionIds, int FooterBytes)
{
    public int Layouts => Survivors.Select(c => (c.Header, c.PartyCodec, c.Stride, c.StorageSize)).Distinct().Count();

    public bool Settled => Layouts == 1;

    public TrialCandidate? Primary => Survivors.Count > 0 ? Survivors[0] : null;

    public int Witnesses => Primary?.Witnesses ?? 0;
}

public static class SectorTrial
{
    public const string TieSentence = "two save layouts both fit this file and would put this edit in different places; box a Pokemon in a later box in the game, save, and open the save again";
    public const string EmptySlot = ExpansionSave.EmptySlot;
    public const int PartySlots = 6;

    public static string SectionsSentence(int sections) => $"this hack's save slot has {sections} sections; the editor reads {SectionLayout.SectionCount}";

    public static IReadOnlyList<int> Strides(int saveBlock1Size)
    {
        var low = ((((saveBlock1Size + 3) / 4) + 3) / 4) * 4;
        var result = new List<int>();
        for (var stride = Math.Max(4, low); stride <= SectionLayout.PayloadCapacity; stride += 4)
            result.Add(stride);
        return result;
    }

    public static (int Boxes, int Size)? StorageFor(int stride, int recordSize)
    {
        if (stride <= 0 || recordSize <= 0)
            return null;
        for (var boxes = 1; ; boxes++)
        {
            var size = (ExpansionLayout.StorageSizeFor(boxes, recordSize) + 3) & ~3;
            if (size > 9 * stride)
                return null;
            if (size > 8 * stride)
                return (boxes, size);
        }
    }

    public static int? Section0Window(ReadOnlySpan<byte> file, IReadOnlyCollection<uint> signatures)
    {
        if (file.Length < GbaSectorContainer.FileSize)
            return null;
        var best = -1;
        uint bestCounter = 0;
        for (var sector = 0; sector < 2 * SectionLayout.SectionCount; sector++)
        {
            var data = file.Slice(sector * SectionLayout.SectorSize, SectionLayout.SectorSize);
            if (!signatures.Contains(BinaryPrimitives.ReadUInt32LittleEndian(data[GbaSectorContainer.SignatureOffset..])) || BinaryPrimitives.ReadUInt16LittleEndian(data[GbaSectorContainer.IdOffset..]) != 0)
                continue;
            var counter = BinaryPrimitives.ReadUInt32LittleEndian(data[GbaSectorContainer.CounterOffset..]);
            if (best < 0 || Newer(bestCounter, counter))
            {
                best = sector;
                bestCounter = counter;
            }
        }
        if (best < 0)
            return null;
        var chosen = file.Slice(best * SectionLayout.SectorSize, SectionLayout.SectorSize);
        var stored = BinaryPrimitives.ReadUInt16LittleEndian(chosen[GbaSectorContainer.ChecksumOffset..]);
        for (var window = SectionLayout.PayloadCapacity; window >= 4; window -= 4)
        {
            if (ChecksumPolicy.Fold(chosen[..window]) == stored)
                return window;
        }
        return null;
    }

    private static bool Newer(uint a, uint b) => (a == uint.MaxValue && b == 0) || (!(a == 0 && b == uint.MaxValue) && b > a);

    public static int BoxRecordSize(RecordCodecKey partyCodec) => partyCodec.Id switch
    {
        RecordCodecId.Pk3CfruPlain => CfruBoxTable.RecordSize,
        RecordCodecId.Pk3SoulGold96 => Pk3SoulGold96Codec.Box,
        _ => Gen3Record.StoredSize,
    };

    public static RecordCodecKey BoxCodec(RecordCodecKey partyCodec) =>
        partyCodec.Id == RecordCodecId.Pk3CfruPlain ? new RecordCodecKey(RecordCodecId.Pk3CfruCompact58, null) : partyCodec;

    private static TrialResult Refuse(string why, int? sections, int footer, IReadOnlyList<TrialCandidate>? checksummed = null) =>
        new(checksummed ?? [], [], why, sections, footer);

    public static TrialResult Run(ReadOnlyMemory<byte> data, SaveShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        var footer = GbaSectorContainer.FooterLength(data);
        var file = GbaSectorContainer.TrimFooter(data);
        if (!shape.Known || shape.Headers.Count == 0 || shape.PartyCodecCandidates.Count == 0 || shape.Signatures.Count == 0)
            return Refuse($"this hack's save layout is not known ({shape.Reason ?? "no header, record codec or signature was derived"})", null, footer);
        if (file.Length != GbaSectorContainer.FileSize)
            return Refuse($"a Gen 3 cartridge save is 0x{GbaSectorContainer.FileSize:X} bytes, this file is 0x{data.Length:X}", null, footer);
        var signatures = shape.Signatures;
        var sections = GbaSectorContainer.NewestSectionIds(file.Span, signatures);
        if (sections is null)
        {
            return Refuse(GbaSectorContainer.UniformSignature(file.Span) is { } other && !signatures.Contains(other)
                ? $"this save's sectors carry the signature 0x{other:X8}, which this hack's layout does not list; open the hack's ROM once so the editor can look for that value there"
                : $"no sector carries an accepted signature ({string.Join(", ", signatures.Select(x => $"0x{x:X8}"))})", null, footer);
        }
        if (sections > SectionLayout.SectionCount)
            return Refuse(SectionsSentence(sections.Value), sections, footer);
        if (shape.Family == SaveShape.Cfru && CfruSave.Vanilla(file) is { } vanilla)
            return Refuse($"the file checksums as a vanilla {vanilla} save, so it is not read as CFRU", sections, footer);

        var window0 = Section0Window(file.Span, signatures);
        var checksummed = new List<TrialCandidate>();
        string? firstWhy = null;
        foreach (var codec in shape.PartyCodecCandidates)
        {
            var boxRecord = BoxRecordSize(codec);
            foreach (var header in shape.Headers)
            {
                IReadOnlyList<int> strides = shape.FixedStride is { } fixedStride ? [fixedStride] : Strides(header.SaveBlock1Size);
                foreach (var stride in strides)
                {
                    int storage;
                    var boxes = 0;
                    if (shape.FixedStorageSize is { } fixedStorage)
                        storage = fixedStorage;
                    else if (StorageFor(stride, boxRecord) is { } fit)
                        (boxes, storage) = fit;
                    else
                        continue;
                    SectionLayout layout;
                    try
                    {
                        layout = SectionLayout.FromBlockSizes($"stride 0x{stride:X}", header.SaveBlock2Size, header.SaveBlock1Size, storage, stride);
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                    foreach (var (rule, policy) in Policies(layout, stride, window0))
                    {
                        if (GbaSectorContainer.TryOpen(file, layout, policy, signatures, out var why) is { } container)
                            checksummed.Add(new TrialCandidate(header, codec, stride, storage, boxes, rule, container));
                        else
                            firstWhy ??= why;
                    }
                }
            }
        }
        if (checksummed.Count == 0)
            return Refuse($"no layout of this hack fits the file ({firstWhy ?? "no section layout could be built from the hack's block sizes"})", sections, footer);

        var sized = checksummed.Where(c => c.Rule == TrialRule.SectionSizes).Select(c => Place(c)).ToHashSet();
        var pool = sized.Count > 0 ? checksummed.Where(c => sized.Contains(Place(c))).ToList() : checksummed;
        var judged = pool.Select(c => Witness(c, shape)).ToList();
        var alive = judged.Where(c => c.Dropped is null).ToList();
        if (alive.Count == 0)
            return Refuse($"the file's sections validate, but its party does not read as this hack's save ({judged[0].Dropped})", sections, footer, checksummed);
        var mostDecoded = alive.Max(c => c.Decoded);
        alive = alive.Where(c => c.Decoded == mostDecoded).ToList();
        var survivors = alive.OrderBy(c => c.Stride == SectionLayout.VanillaStride ? 0 : 1).ThenBy(c => c.Stride).ThenBy(c => c.Rule).ThenBy(c => shape.Headers.ToList().IndexOf(c.Header)).ToList();
        return new TrialResult(checksummed, survivors, null, sections, footer);
    }

    private static (int Shift, int Stride, int Storage) Place(TrialCandidate c) => (c.Header.Shift, c.Stride, c.StorageSize);

    private static IEnumerable<(TrialRule Rule, ChecksumPolicy Policy)> Policies(SectionLayout layout, int stride, int? window0)
    {
        yield return (TrialRule.SectionSizes, ChecksumPolicy.FromLayout(layout));
        yield return (TrialRule.UniformStride, ChecksumPolicy.Uniform($"0x{stride:X} windows", stride));
        if (window0 is { } w)
            yield return (TrialRule.FullWindows, ChecksumPolicy.Uniform($"0x{SectionLayout.PayloadCapacity:X} windows, section 0 at 0x{w:X} (from the save's own bytes)", SectionLayout.PayloadCapacity, new Dictionary<int, int> { [0] = w }));
    }

    public static TrialCandidate Witness(TrialCandidate c, SaveShape shape)
    {
        var codec = RecordCodecs.For(c.PartyCodec.Id, c.PartyCodec.Version);
        var h = c.Header;
        var small = c.Container.Layout.BlockSize(SaveBlock.Small);
        var large = c.Container.Block(SaveBlock.Large);
        if (codec.PartySize is not { } size || h.PartyCountOffset < 0 || h.PartyOffset < 0 || h.PartyCountOffset >= large.Length || (long)h.PartyOffset + (PartySlots * size) > large.Length)
            return c with { Dropped = $"the party of {c.PartyCodec} records lies outside SaveBlock1" };
        if (h.TrainerIdOffset < 0 || h.PlayerNameOffset < 0 || h.TrainerIdOffset + 4 > small || h.PlayerNameOffset + 8 > small)
            return c with { Dropped = "the trainer offsets lie outside SaveBlock2" };
        var count = large[h.PartyCountOffset];
        if (count > PartySlots)
            return c with { Dropped = $"the party count byte is {count}, above {PartySlots}" };
        for (var i = 0; i < PartySlots; i++)
        {
            var record = large.AsSpan(h.PartyOffset + (i * size), size);
            var mon = codec.Decode(record, out var why);
            if (i < count && mon is null)
                return c with { Dropped = $"party slot {i + 1} does not decode as {c.PartyCodec} ({why ?? "refused"})" };
            if (i >= count && (mon is not null || (why != EmptySlot && record.ContainsAnyExcept((byte)0))))
                return c with { Dropped = $"party slot {i + 1}, after the party count of {count}, is not empty" };
        }
        var boxKey = BoxCodec(c.PartyCodec);
        var boxCodec = RecordCodecs.For(boxKey.Id, boxKey.Version);
        if (boxCodec.StoredSize is not { } stored)
            return c;
        var storage = c.Container.Block(SaveBlock.Storage);
        var first = c.Container.Layout.Slots[SectionLayout.IdsOf(SaveBlock.Storage).First()].Size;
        var cfru = shape.Family == SaveShape.Cfru;
        var refused = 0;
        var decoded = 0;
        var past = 0;
        var ranges = new List<(int Start, int Records)>();
        if (cfru)
            ranges.AddRange(shape.BoxSources.Where(b => b.Area == CfruBoxArea.Storage && b.Tier != ShapeTier.Unknown).Select(b => (b.Offset, b.Count * CfruVariant.BoxSlots)));
        else
            ranges.Add((ExpansionLayout.BoxesOffset, c.Boxes * ExpansionLayout.BoxSlots));
        foreach (var (start, records) in ranges)
        {
            for (var n = 0; n < records; n++)
            {
                var at = start + (n * stored);
                if (at < 0 || at + stored > storage.Length)
                    break;
                var record = storage.AsSpan(at, stored);
                var occupied = cfru
                    ? BinaryPrimitives.ReadUInt16LittleEndian(record[0x1C..]) != 0 && record.ContainsAnyExcept((byte)0xFF)
                    : record.ContainsAnyExcept((byte)0);
                if (!occupied)
                    continue;
                if (boxCodec.Decode(record, out var why) is not null)
                {
                    decoded++;
                    if (at + stored > first)
                        past++;
                }
                else if (why != EmptySlot)
                {
                    refused++;
                }
            }
        }
        return c with { Refused = refused, Decoded = decoded, Witnesses = past };
    }
}

public sealed class TiedSave : HackSave
{
    private readonly IReadOnlyList<HackSave> all;
    private string? broken;

    public TiedSave(IReadOnlyList<HackSave> saves, IReadOnlyList<string> layoutNames) : base(First(saves).Variant, First(saves).Original, First(saves).Evidence)
    {
        all = saves;
        LayoutNames = layoutNames;
    }

    private static HackSave First(IReadOnlyList<HackSave> saves)
    {
        ArgumentNullException.ThrowIfNull(saves);
        if (saves.Count < 2)
            throw new ArgumentException("a tied save holds two or more layouts", nameof(saves));
        return saves[0];
    }

    public HackSave Primary => all[0];

    public IReadOnlyList<HackSave> Layouts => all;

    public IReadOnlyList<string> LayoutNames { get; }

    public override string? ReadOnlyReason => Primary.ReadOnlyReason;

    public override IReadOnlyList<HackMon?> Party => Primary.Party;

    public override HackTrainer Trainer => Primary.Trainer;

    public override uint? Money => Primary.Money;

    public override string? BoxesReason => Primary.BoxesReason;

    public override IReadOnlyList<HackBox> Boxes => Primary.Boxes;

    public override IReadOnlyDictionary<int, string> PartyRefused => Primary.PartyRefused;

    public override string? BoxWriteRefusal(int box) => Primary.BoxWriteRefusal(box);

    public override void SetPartySlot(int slot, HackMon mon) => Each(s => s.SetPartySlot(slot, mon));

    public override void SetBoxSlot(int box, int slot, HackMon mon) => Each(s => s.SetBoxSlot(box, slot, mon));

    public override void SetMoney(uint money) => Each(s => s.SetMoney(money));

    private void Each(Action<HackSave> edit)
    {
        edit(Primary);
        foreach (var other in all.Skip(1))
        {
            try
            {
                edit(other);
            }
            catch (Exception e) when (e is FaithfulWriteException or RecordEncodeException or ArgumentOutOfRangeException)
            {
                broken = SectorTrial.TieSentence;
            }
        }
    }

    public override WriteResult Plan()
    {
        if (broken is not null)
            throw new FaithfulWriteException(broken);
        var first = Primary.Plan();
        foreach (var other in all.Skip(1))
        {
            if (!other.Plan().Bytes.AsSpan().SequenceEqual(first.Bytes))
                throw new FaithfulWriteException(SectorTrial.TieSentence);
        }
        return first;
    }
}
