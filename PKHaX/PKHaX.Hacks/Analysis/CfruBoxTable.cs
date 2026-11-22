using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;

namespace PKHaX.Hacks.Analysis;

public enum CfruRunTier
{
    Derived,
    Family,
    Unknown,
}

public sealed record CfruRamMap(string Source, uint Storage, int StorageSize, uint SaveBlock1, int SaveBlock1Size, uint SaveBlock2, int SaveBlock2Size, uint ExtraSectors, int ExtraSectorsSize)
{
    public const string FireRedCode = "BPRE";
    public const int RevisionAt = 0xBC;

    public static CfruRamMap FireRed10 { get; } = new(
        "pokefirered's RAM map for FireRed 1.0 (gPokemonStorage 0x02029314, gSaveBlock1 0x0202552C, gSaveBlock2 0x02024588) and CFRU's sector 30 buffer at 0x0203C038",
        0x02029314, 0x83D0, 0x0202552C, 0x3D68, 0x02024588, 0xF24, 0x0203C038, 2 * GbaSectorContainer.RawPayload);

    public static CfruRamMap? For(GbaImage rom) => For(rom, out _);

    public static CfruRamMap? For(GbaImage rom, out string? why)
    {
        var code = rom.GameCode;
        var revision = rom.U8(RevisionAt);
        if (code == FireRedCode && revision == 0)
        {
            why = null;
            return FireRed10;
        }
        why = $"the image's header says {Printable(code)} revision {revision}; the RAM map is known for {FireRedCode} revision 0 only";
        return null;
    }

    private static string Printable(string code) => code.All(c => c is >= ' ' and <= '~') ? code : "an unreadable game code";

    public (CfruBoxArea Area, int Offset, int Size)? Locate(uint address)
    {
        (CfruBoxArea Area, uint Base, int Size)[] areas =
        [
            (CfruBoxArea.Storage, Storage, StorageSize),
            (CfruBoxArea.Sectors30And31, ExtraSectors, ExtraSectorsSize),
            (CfruBoxArea.SaveBlock1, SaveBlock1, SaveBlock1Size),
            (CfruBoxArea.SaveBlock2, SaveBlock2, SaveBlock2Size),
        ];
        foreach (var (area, start, size) in areas)
        {
            if (address >= start && address - start < (uint)size)
                return (area, (int)(address - start), size);
        }
        return null;
    }
}

public sealed record CfruBoxRun(int FirstBox, int Count, uint FirstAddress, CfruBoxArea? Area, int Offset, CfruRunTier Tier, string Origin)
{
    public bool SaveChecked { get; init; }

    public int LastBox => FirstBox + Count - 1;

    public bool Located => Area is not null && Tier != CfruRunTier.Unknown;

    public bool NeedsSaveCheck => Count == 1;

    public bool Readable => Located && (!NeedsSaveCheck || SaveChecked);

    public bool WritableFromRomAlone => Located && Tier == CfruRunTier.Derived && !NeedsSaveCheck && Area is (CfruBoxArea.Storage or CfruBoxArea.Sectors30And31);

    public CfruBoxSource? Source() => Readable && Area is { } area ? new CfruBoxSource(FirstBox, Count, area, Offset, Origin, WritableFromRomAlone) : null;
}

public sealed record CfruBoxTable(int Offset, IReadOnlyList<uint> Entries, IReadOnlyList<CfruBoxRun> Runs, CfruRamMap? Map, IReadOnlyList<string> Notes)
{
    public const int BoxBytes = CfruVariant.BoxSlots * RecordSize;
    public const int RecordSize = 58;
    public const int MinRun = 3;
    public const int EngineRun = 8;
    public const int MaxEntries = 128;
    public const int BoxNameLength = 8;
    public const int NameStride = BoxNameLength + 1;
    public const uint Ewram = 0x02000000;
    public const int EwramSize = 0x40000;
    public const string Source = CfruVariant.CfruSource;

    private const string Provisional = "the first entry is a lone pointer, so box numbers are provisional until the save check (WithSave)";

    public int BoxCount => Entries.Count;

    public int LocatedBoxCount => Runs.Where(r => r.Located).Sum(r => r.Count);

    public bool NumberingProvisional => Runs.Count > 1 && Runs[0] is { Located: true, NeedsSaveCheck: true, SaveChecked: false };

    public IReadOnlyList<CfruBoxSource> Sources() => [.. Runs.Select(r => r.Source()).OfType<CfruBoxSource>()];

    public static bool IsBoxPointer(uint value) => value >= Ewram && value - Ewram <= EwramSize - BoxBytes;

    public static bool HasRun(GbaImage rom, int min = EngineRun) => HasRun(rom.Span, min);

    public static bool HasRun(ReadOnlySpan<byte> rom, int min = EngineRun) => min >= 1 && LongestRun(rom).Count >= min;

    public static (int Offset, int Count) LongestRun(ReadOnlySpan<byte> rom)
    {
        var best = (Offset: -1, Count: 0);
        var start = -1;
        var count = 0;
        uint previous = 0;
        for (var o = 0; o + 4 <= rom.Length; o += 4)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(rom[o..]);
            var pointer = IsBoxPointer(value);
            if (count > 0 && pointer && value == previous + BoxBytes)
            {
                count++;
            }
            else
            {
                if (count > best.Count)
                    best = (start, count);
                start = pointer ? o : -1;
                count = pointer ? 1 : 0;
            }
            previous = value;
        }
        return count > best.Count ? (start, count) : best;
    }

    public static CfruBoxTable? Read(GbaImage rom)
    {
        var map = CfruRamMap.For(rom, out var why);
        return Read(rom.Span, map, why);
    }

    public static CfruBoxTable? Read(ReadOnlySpan<byte> rom, CfruRamMap? map, string? noMapReason = null)
    {
        var seed = LongestRun(rom);
        if (seed.Count < MinRun)
            return null;
        var first = seed.Offset;
        var end = seed.Offset + (Math.Min(seed.Count, MaxEntries) * 4);
        var notes = new List<string>();
        while ((end - first) / 4 < MaxEntries && BoxPointerAt(rom, end) && !NameStepAt(rom, end))
            end += 4;
        while ((end - first) / 4 < MaxEntries && BoxPointerAt(rom, first - 4) && !NameStepAt(rom, first - 8))
            first -= 4;
        var namesAfter = NameRun(rom, end, 4);
        var namesBefore = NameRun(rom, first - 4, -4);
        if ((namesAfter == 0 && BoxPointerAt(rom, end)) || (namesBefore == 0 && BoxPointerAt(rom, first - 4)))
            notes.Add($"the pointer table was cut at {MaxEntries} entries");
        if (namesAfter > 0)
            notes.Add(NamesNote(namesAfter, "after"));
        if (namesBefore > 0)
            notes.Add(NamesNote(namesBefore, "before"));
        var entries = new List<uint>();
        for (var o = first; o < end; o += 4)
            entries.Add(BinaryPrimitives.ReadUInt32LittleEndian(rom[o..]));
        if (map is null)
            notes.Add(noMapReason ?? "no RAM map was given, so only the longest run is placed");
        return Build(first, entries, map, noMapReason, notes);
    }

    private static bool BoxPointerAt(ReadOnlySpan<byte> rom, int offset) =>
        offset >= 0 && offset + 4 <= rom.Length && IsBoxPointer(BinaryPrimitives.ReadUInt32LittleEndian(rom[offset..]));

    private static string NamesNote(int count, string where) =>
        $"{count} EWRAM pointer(s) {where} the table are a name pointer table, not boxes: it starts with two pointers {NameStride} bytes apart (a box name, BOX_NAME_LENGTH + 1, pokefirered include/pokemon_storage_system.h boxNames) and every step in it is under one box";

    private static long Step(ReadOnlySpan<byte> rom, int offset) =>
        (long)BinaryPrimitives.ReadUInt32LittleEndian(rom[(offset + 4)..]) - BinaryPrimitives.ReadUInt32LittleEndian(rom[offset..]);

    private static bool NameStepAt(ReadOnlySpan<byte> rom, int offset) =>
        BoxPointerAt(rom, offset) && BoxPointerAt(rom, offset + 4) && Math.Abs(Step(rom, offset)) == NameStride;

    private static int NameRun(ReadOnlySpan<byte> rom, int start, int direction)
    {
        var pair = direction > 0 ? start : start - 4;
        if (!NameStepAt(rom, pair))
            return 0;
        var count = 1;
        var at = start;
        while (count < MaxEntries && BoxPointerAt(rom, at + direction))
        {
            var gap = Math.Abs(Step(rom, direction > 0 ? at : at - 4));
            if (gap == 0 || gap >= BoxBytes)
                break;
            count++;
            at += direction;
        }
        return count;
    }

    private static CfruBoxTable Build(int offset, List<uint> entries, CfruRamMap? map, string? noMapReason, List<string> notes)
    {
        var before = 0;
        var after = 0;
        List<CfruBoxRun> runs;
        while (true)
        {
            var spans = Split(entries);
            runs = map is null ? Family(entries, spans, noMapReason) : Mapped(entries, spans, map);
            if (runs.Count > 1 && runs[0].Count == 1 && !runs[0].Located)
            {
                entries.RemoveAt(0);
                offset += 4;
                before++;
            }
            else if (map is not null && runs.Count > 1 && runs[^1].Count == 1 && map.Locate(runs[^1].FirstAddress) is null)
            {
                entries.RemoveAt(entries.Count - 1);
                after++;
            }
            else
            {
                break;
            }
        }
        if (before > 0)
            notes.Add($"{before} lone EWRAM pointer(s) before the table are placed in no save area and are not boxes");
        if (after > 0)
            notes.Add($"{after} lone EWRAM pointer(s) after the table are in no save area and are not boxes");
        if (runs.Count > 1 && runs[0] is { Located: true, NeedsSaveCheck: true })
            notes.Add(Provisional);
        return new CfruBoxTable(offset, entries, runs, map, notes);
    }

    private static List<(int Index, int Count)> Split(List<uint> entries)
    {
        var spans = new List<(int Index, int Count)>();
        var index = 0;
        for (var i = 1; i <= entries.Count; i++)
        {
            if (i < entries.Count && entries[i] == entries[i - 1] + BoxBytes)
                continue;
            spans.Add((index, i - index));
            index = i;
        }
        return spans;
    }

    private static (CfruBoxArea Area, int Offset)? Place(CfruRamMap map, uint address, int count)
    {
        if (map.Locate(address) is not { } hit)
            return null;
        return (long)hit.Offset + ((long)count * BoxBytes) <= hit.Size ? (hit.Area, hit.Offset) : null;
    }

    private static List<CfruBoxRun> Mapped(List<uint> entries, List<(int Index, int Count)> spans, CfruRamMap map)
    {
        var runs = new List<CfruBoxRun>();
        foreach (var (index, count) in spans)
        {
            var address = entries[index];
            var spot = $"{Source} entry {index + 1} = 0x{address:X8}";
            if (Place(map, address, count) is { } placed)
                runs.Add(new CfruBoxRun(index + 1, count, address, placed.Area, placed.Offset, CfruRunTier.Derived, $"{spot}, {placed.Area} + 0x{placed.Offset:X} by {map.Source}"));
            else if (map.Locate(address) is { } hit)
                runs.Add(new CfruBoxRun(index + 1, count, address, null, 0, CfruRunTier.Unknown, $"{spot}: not located, {count} box(es) from {hit.Area} + 0x{hit.Offset:X} would run past its 0x{hit.Size:X} bytes"));
            else
                runs.Add(new CfruBoxRun(index + 1, count, address, null, 0, CfruRunTier.Unknown, $"{spot}: not located, the address is in no save area of {map.Source}"));
        }
        for (var a = 0; a < runs.Count; a++)
        {
            for (var b = a + 1; b < runs.Count; b++)
            {
                if (!Overlap(runs[a], runs[b]))
                    continue;
                var text = $"boxes {runs[a].FirstBox}-{runs[a].LastBox} and {runs[b].FirstBox}-{runs[b].LastBox} overlap in {runs[a].Area}";
                var loseA = runs[a].Count <= runs[b].Count;
                var loseB = runs[b].Count <= runs[a].Count;
                if (loseA)
                    runs[a] = Lost(runs[a], text);
                if (loseB)
                    runs[b] = Lost(runs[b], text);
            }
        }
        return runs;
    }

    private static bool Overlap(CfruBoxRun a, CfruBoxRun b) =>
        a.Located && b.Located && a.Area == b.Area
        && a.Offset < (long)b.Offset + ((long)b.Count * BoxBytes) && b.Offset < (long)a.Offset + ((long)a.Count * BoxBytes);

    private static CfruBoxRun Lost(CfruBoxRun run, string why) =>
        run with { Area = null, Offset = 0, Tier = CfruRunTier.Unknown, SaveChecked = false, Origin = $"{Source} entry {run.FirstBox} = 0x{run.FirstAddress:X8}: not located, {why}" };

    private static List<CfruBoxRun> Family(List<uint> entries, List<(int Index, int Count)> spans, string? noMapReason)
    {
        var longest = spans.MaxBy(s => s.Count);
        var because = noMapReason ?? "no RAM map was given";
        var runs = new List<CfruBoxRun>();
        foreach (var (index, count) in spans)
        {
            var address = entries[index];
            var spot = $"{Source} entry {index + 1} = 0x{address:X8}";
            var storage = index == longest.Index && count >= EngineRun && 4 + ((long)count * BoxBytes) <= CfruRamMap.FireRed10.StorageSize;
            runs.Add(storage
                ? new CfruBoxRun(index + 1, count, address, CfruBoxArea.Storage, 4, CfruRunTier.Family, $"{spot}, taken as the storage block + 4 (CFRU ORIGINAL_BOX_POKEMON_RAM is gPokemonStorage + 4; {because})")
                : new CfruBoxRun(index + 1, count, address, null, 0, CfruRunTier.Unknown, $"{spot}: not located ({because})"));
        }
        return runs;
    }

    public CfruBoxTable WithSave(Func<CfruBoxArea, ReadOnlyMemory<byte>> area)
    {
        ArgumentNullException.ThrowIfNull(area);
        var codec = RecordCodecs.For(RecordCodecId.Pk3CfruCompact58);
        var runs = new List<CfruBoxRun>(Runs.Count);
        for (var i = 0; i < Runs.Count; i++)
        {
            var run = Runs[i];
            if (!run.Located || !run.NeedsSaveCheck || run.Area is not { } spot)
            {
                runs.Add(run);
                continue;
            }
            var bytes = area(spot).Span;
            var why = (long)run.Offset + BoxBytes > bytes.Length
                ? $"the save's {spot} area holds 0x{bytes.Length:X} bytes, short of the box at 0x{run.Offset:X}"
                : SlotProblem(codec, bytes.Slice(run.Offset, BoxBytes));
            if (why is not null && i == 0 && Runs.Count > 1)
            {
                List<uint> rest = [.. Entries.Skip(1)];
                List<string> notes = [.. Notes.Where(n => n != Provisional), $"the lone pointer 0x{run.FirstAddress:X8} before the table is not a box ({why}); it is left out and the boxes are numbered from the next entry"];
                return Build(Offset + 4, rest, Map, null, notes).WithSave(area);
            }
            runs.Add(why is null
                ? run with { SaveChecked = true }
                : Lost(run, $"a lone pointer is a box only when all {CfruVariant.BoxSlots} slots in the save are empty or plausible, and {why}"));
        }
        return this with { Runs = runs };
    }

    public static string? SlotProblem(IHackRecordCodec codec, ReadOnlySpan<byte> box)
    {
        for (var s = 0; s < CfruVariant.BoxSlots; s++)
        {
            var record = box.Slice(s * RecordSize, RecordSize);
            if (BinaryPrimitives.ReadUInt16LittleEndian(record[0x1C..]) == 0 || !record.ContainsAnyExcept((byte)0xFF))
                continue;
            if (codec.Decode(record, out var why) is null)
                return $"slot {s + 1} is refused ({why ?? "refused"})";
        }
        return null;
    }
}
