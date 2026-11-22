using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public static class GbHackData
{
    public const string ReaderAnchor = "gb-names";
    public const string ReaderSym = "gb-sym";
    public const int NameWidth = 10;
    public const byte Terminator = 0x50;
    public const int MaxEntries = 512;
    public const int Gen1SpeciesSlots = 190;
    public const int Gen2SpeciesSlots = 256;

    public const int Gen1BaseSize = 28;
    public const int Gen2BaseSize = 32;

    private static readonly byte[] Gen1BaseAnchor = [1, 45, 49, 49, 45, 65];
    private static readonly byte[] Gen2BaseAnchor = [1, 45, 49, 49, 45, 65, 65];
    private static readonly byte[] Gen1IvysaurBase = [2, 60, 62, 63, 60, 80];
    private static readonly byte[] Gen2IvysaurBase = [2, 60, 62, 63, 60, 80, 80];

    public static int? FindBaseStats(ReadOnlySpan<byte> rom, int gen, SymFile? sym)
    {
        if (sym?.RomOffset(gen == 1 ? "BaseStats" : "BaseData") is { } labelled)
            return labelled < rom.Length ? labelled : null;
        var (anchor, next, size) = gen == 1 ? (Gen1BaseAnchor, Gen1IvysaurBase, Gen1BaseSize) : (Gen2BaseAnchor, Gen2IvysaurBase, Gen2BaseSize);
        var from = 0;
        while (from < rom.Length)
        {
            var hit = rom[from..].IndexOf(anchor);
            if (hit < 0)
                return null;
            var at = from + hit;
            if (at + size + next.Length <= rom.Length && rom.Slice(at + size, next.Length).SequenceEqual(next))
                return at;
            from = at + 1;
        }
        return null;
    }

    private static HackSpecies WithBase(HackSpecies s, ReadOnlySpan<byte> e, int gen) => gen == 1
        ? s with { BaseStats = [e[1], e[2], e[3], e[4], e[5], e[5]], Types = [e[6], e[7]], CatchRate = e[8], ExpYield = e[9], Growth = e[19], GenderRead = false }
        : s with { BaseStats = [e[1], e[2], e[3], e[4], e[5], e[6]], Types = [e[7], e[8]], CatchRate = e[9], ExpYield = e[10], GenderRatio = e[13], Growth = e[22], GenderRead = true };

    private static bool SameStats(ReadOnlySpan<byte> rom, int row, PersonalInfo1 p) =>
        rom[row + 1] == p.HP && rom[row + 2] == p.ATK && rom[row + 3] == p.DEF && rom[row + 4] == p.SPE && rom[row + 5] == p.SPC;

    private static readonly string[] Gen1Anchor = ["RHYDON", "KANGASKHAN", "NIDORAN♂"];
    private static readonly string[] Gen2Anchor = ["BULBASAUR", "IVYSAUR", "VENUSAUR"];
    private static readonly string[] MoveAnchor = ["POUND", "KARATE CHOP", "DOUBLESLAP"];
    private static readonly string[] ItemAnchor = ["MASTER BALL", "ULTRA BALL"];
    private static readonly string[] MoveAnchorShort = ["POUND", "KARATE CHOP"];

    public const int Gen1MoveSize = 6;
    public const int Gen2MoveSize = 7;
    public const byte GbSpecialType = 0x14;
    public const int MaxBadNames = 2;

    public static int Percent(byte raw) => ((raw * 100) + 127) / 255;

    public sealed record GbMoveRows(int Offset, int Count, int Anchors)
    {
        public int Stride { get; init; }
        public int Renumbered { get; init; }
    }

    private static (int Agree, int Compared) MapAgreement(ReadOnlySpan<byte> rom, GbIndexMap map, IReadOnlyList<int?> nationals, int names, IReadOnlyDictionary<int, int> byDex)
    {
        var compared = 0;
        var agree = 0;
        for (var i = 0; i < Math.Min(map.Length, Math.Min(names, nationals.Count)); i++)
        {
            if (nationals[i] is not { } n || n > PersonalTable.StockRB.MaxSpeciesID || !(map.Dex[i] is > 0 and var d) || !byDex.TryGetValue(d, out var row))
                continue;
            compared++;
            var stock = PersonalTable.StockRB[n];
            if (rom[row + 6] == stock.Type1 && rom[row + 7] == stock.Type2)
                agree++;
        }
        return (agree, compared);
    }

    private static (int Agree, int Compared) Gen2TypeAgreement(ReadOnlySpan<byte> rom, IReadOnlyList<HackSpecies> list, IReadOnlyDictionary<int, int> byDex)
    {
        var compared = 0;
        var agree = 0;
        for (var i = 1; i < list.Count; i++)
        {
            if (list[i].National is not { } n || n > PersonalTable.StockC.MaxSpeciesID || !byDex.TryGetValue(list[i].Id, out var row))
                continue;
            compared++;
            var stock = PersonalTable.StockC[n];
            if (rom[row + 7] == stock.Type1 && rom[row + 8] == stock.Type2)
                agree++;
        }
        return (agree, compared);
    }

    public static GbMoveRows? MoveRows(ReadOnlySpan<byte> rom, int stride, int names, IReadOnlyList<HackMove> moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        var need = Math.Min(GbStructure.MinBaseRows, Math.Max(MoveAnchors.MinAgree, names * 9 / 10));
        GbMoveRows? best = null;
        foreach (var run in GbStructure.MoveRows(rom, stride, need))
        {
            var p = run.Offset;
            var n = run.Count;
            if (best is not null && n <= best.Count)
                continue;
            var present = 0;
            var agree = 0;
            foreach (var a in MoveAnchors.All)
            {
                var id = -1;
                for (var i = 1; i < moves.Count; i++)
                {
                    if (moves[i].National == a.Move)
                    {
                        id = i;
                        break;
                    }
                }
                if (id < 1 || id > n)
                    continue;
                present++;
                var at = p + ((id - 1) * stride);
                if (rom[at + 2] == a.Power && Percent(rom[at + 4]) == a.Accuracy && rom[at + 5] == a.Pp)
                    agree++;
            }
            if (present >= MoveAnchors.MinAgree && agree >= MoveAnchors.MinAgree)
                best = new GbMoveRows(p, n, agree) { Stride = stride, Renumbered = run.Renumbered };
        }
        return best;
    }

    public const string ReaderStructure = "gb-structure";
    public const int MapSlack = 2;
    public const int Gen1LevelOneMoves = 15;
    public const double ListCountSlack = 0.1;

    public sealed record GbSpeciesRun(int Gen, GbNameRun Run, bool ByVote);

    public sealed record GbVotedList(GbNameList List, int Agree, int NonBlank);

    public static GbSpeciesRun? StructuralSpecies(ReadOnlySpan<byte> rom, out string why)
    {
        var rows1 = GbStructure.BaseRows(rom, Gen1BaseSize, 5);
        var rows2 = rows1 is null ? GbStructure.BaseRows(rom, Gen2BaseSize, 6) : null;
        if (rows1 is null && rows2 is null)
        {
            why = $"by structure, no run of at least {GbStructure.MinBaseRows} base rows at stride {Gen1BaseSize} (five stats, Gen 1) or {Gen2BaseSize} (six stats, Gen 2) whose first byte counts 1, 2, 3 ... was found either, so the generation and the species list are unknown";
            return null;
        }
        var gen = rows1 is not null ? 1 : 2;
        var rows = rows1 ?? rows2!;
        var runs = GbStructure.FixedNameRuns(rom);
        if (runs.Count == 0)
        {
            why = $"by structure, Gen {gen} base rows were found ({rows.Count} rows at 0x{rows.Offset:X}), but no run of at least {GbStructure.MinFixedSlots} ten-byte name slots (a letter, text, then 0x50 padding) was found";
            return null;
        }
        var keys = NameVote.Keys(NameVote.StockSpecies);
        GbNameRun? voted = null;
        var votedAgree = 0;
        var votedNonBlank = 0;
        foreach (var run in runs)
        {
            var names = ReadFixed(rom, run.Offset, run.Count);
            List<string> indexed = ["", .. names];
            var (agree, nonBlank) = NameVote.Score(indexed, NameVote.StockSpecies, NameVoteMode.ByMembership, NameVote.Window, keys);
            if (NameVote.Accepted(agree, nonBlank) && agree > votedAgree)
                (voted, votedAgree, votedNonBlank) = (run, agree, nonBlank);
        }
        if (voted is not null)
        {
            why = $"species names by structure: {voted.Count} ten-byte slots at 0x{voted.Offset:X}, {votedAgree} of {votedNonBlank} of them stock species names (by vote among {runs.Count} fixed-width runs); Gen {gen} by the base rows ({rows.Count} rows of {rows.Stride} bytes at 0x{rows.Offset:X})";
            return new GbSpeciesRun(gen, voted, true);
        }
        var maps = gen == 1 ? GbStructure.IndexMaps(rom, rows.Count) : new List<GbIndexMap>();
        var target = maps.Count > 0 ? maps[0].Length : rows.Count;
        var chosen = runs.OrderBy(r => Math.Abs(r.Count - target)).ThenBy(r => r.Offset).First();
        var ties = runs.Count(r => Math.Abs(r.Count - target) == Math.Abs(chosen.Count - target));
        why = $"species names by structure, chosen by count: no fixed-width run holds stock species names, so the run of {chosen.Count} ten-byte slots at 0x{chosen.Offset:X} is taken as the species list because its length is the closest to {target} (the {(maps.Count > 0 ? "length of the species index map" : "number of base rows")}) among {runs.Count} run{(runs.Count == 1 ? "" : "s")}{(ties > 1 ? $"; {ties} runs are equally close and the first is taken" : "")}; Gen {gen} by the base rows ({rows.Count} rows of {rows.Stride} bytes at 0x{rows.Offset:X})";
        return new GbSpeciesRun(gen, chosen, false);
    }

    public static GbVotedList? VoteList(IReadOnlyList<GbNameList> lists, IReadOnlyList<string> stock, int minNames, GbNameList? except)
    {
        ArgumentNullException.ThrowIfNull(lists);
        ArgumentNullException.ThrowIfNull(stock);
        var keys = NameVote.Keys(stock);
        GbVotedList? best = null;
        foreach (var list in lists)
        {
            if (list.Count < minNames || (except is not null && list.Offset == except.Offset))
                continue;
            var (agree, nonBlank) = NameVote.Score(list.Indexed(), stock, NameVoteMode.ByMembership, NameVote.Window, keys);
            if (NameVote.Accepted(agree, nonBlank) && (best is null || agree > best.Agree))
                best = new GbVotedList(list, agree, nonBlank);
        }
        return best;
    }

    public static GbNameList? ClosestList(IReadOnlyList<GbNameList> lists, int count, GbNameList? except)
    {
        ArgumentNullException.ThrowIfNull(lists);
        GbNameList? best = null;
        foreach (var list in lists)
        {
            if (list.Count < GbStructure.MinListNames || (except is not null && list.Offset == except.Offset) || Math.Abs(list.Count - count) > ListCountSlack * count)
                continue;
            if (best is null || Math.Abs(list.Count - count) < Math.Abs(best.Count - count))
                best = list;
        }
        return best;
    }

    public static GbRowRun? ShapedMoveRows(ReadOnlySpan<byte> rom, int gen, IReadOnlyList<int> strides)
    {
        ArgumentNullException.ThrowIfNull(strides);
        foreach (var stride in strides)
        {
            GbRowRun? best = null;
            foreach (var run in GbStructure.MoveRows(rom, stride, GbStructure.MinListNames))
            {
                if ((best is null || run.Count > best.Count) && GbStructure.MoveShape(rom, run, gen))
                    best = run;
            }
            if (best is not null)
                return best;
        }
        return null;
    }

    private static void FillMoves(ReadOnlySpan<byte> rom, List<HackMove> moves, int offset, int stride, int count)
    {
        for (var i = 1; i < moves.Count && i <= count; i++)
        {
            var at = offset + ((i - 1) * stride);
            var power = rom[at + 2];
            var type = rom[at + 3];
            moves[i] = moves[i] with { Power = power, Type = type, Accuracy = Percent(rom[at + 4]), Pp = rom[at + 5], Category = power == 0 ? HackMove.StatusCategory : type < GbSpecialType ? HackMove.Physical : HackMove.Special, CategoryByType = true };
        }
    }

    public static List<string> ListWithGaps(ReadOnlySpan<byte> rom, int start, int max, List<string> notes, string what)
    {
        ArgumentNullException.ThrowIfNull(notes);
        var strict = ReadList(rom, start, max);
        if (strict.Count >= NameVote.MinAgree)
            return strict;
        var names = new List<string>();
        var at = start;
        var bad = 0;
        var skipped = 0;
        var lastGood = -1;
        for (var i = 0; i < max && at >= 0 && at < rom.Length; i++)
        {
            var end = rom[at..].IndexOf(Terminator);
            if (end <= 0 || end > 20)
                break;
            var name = Text(rom.Slice(at, end + 1));
            at += end + 1;
            if (name is null)
            {
                if (++bad > MaxBadNames)
                    break;
                names.Add("");
                skipped++;
                continue;
            }
            bad = 0;
            names.Add(name);
            lastGood = names.Count - 1;
        }
        names.RemoveRange(lastGood + 1, names.Count - lastGood - 1);
        if (names.Count < strict.Count + NameVote.MinAgree)
            return strict;
        skipped = names.Count(n => n.Length == 0);
        notes.Add($"the {what}-name list has {skipped} name{(skipped == 1 ? "" : "s")} with characters this reader does not decode, after the first {strict.Count}; {(skipped == 1 ? "it is" : "they are")} kept as blank so later ids stay in place ({names.Count} {what}s in all)");
        return names;
    }

    public static byte[] Encode(string text)
    {
        var buffer = new byte[text.Length * 2 + 1];
        var n = StringConverter1.SetString(buffer, text, text.Length, false);
        var span = buffer.AsSpan(0, n);
        var end = span.IndexOf(Terminator);
        return (end >= 0 ? span[..end] : span).ToArray();
    }

    public static byte[] Padded(string text)
    {
        var slot = new byte[NameWidth];
        slot.AsSpan().Fill(Terminator);
        Encode(text).AsSpan(0, Math.Min(NameWidth, text.Length)).CopyTo(slot);
        return slot;
    }

    public static int FindFixed(ReadOnlySpan<byte> rom, IReadOnlyList<string> anchor)
    {
        var needle = new byte[anchor.Count * NameWidth];
        for (var i = 0; i < anchor.Count; i++)
            Padded(anchor[i]).CopyTo(needle, i * NameWidth);
        return rom.IndexOf(needle);
    }

    public static int FindList(ReadOnlySpan<byte> rom, IReadOnlyList<string> anchor)
    {
        var parts = new List<byte>();
        foreach (var a in anchor)
        {
            parts.AddRange(Encode(a));
            parts.Add(Terminator);
        }
        return rom.IndexOf(parts.ToArray());
    }

    public static string? Text(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes[0] == Terminator || bytes[0] == 0x00)
            return null;
        foreach (var b in bytes)
        {
            if (b == Terminator)
                break;
            if (b is not (>= 0x80 and <= 0xBF or >= 0xE0 and <= 0xFF or 0x7F))
                return null;
        }
        var s = StringConverter1.GetString(bytes, false);
        return s.Length == 0 ? null : s;
    }

    public static List<string> ReadFixed(ReadOnlySpan<byte> rom, int start, int max)
    {
        var names = new List<string>();
        if (start < 0 || start >= rom.Length)
            return names;
        for (var i = 0; i < max && start + (i + 1) * NameWidth <= rom.Length; i++)
        {
            var name = Text(rom.Slice(start + i * NameWidth, NameWidth));
            if (name is null)
                break;
            names.Add(name);
        }
        return names;
    }

    public static List<string> ReadList(ReadOnlySpan<byte> rom, int start, int max)
    {
        var names = new List<string>();
        var at = start;
        for (var i = 0; i < max && at >= 0 && at < rom.Length; i++)
        {
            var end = rom[at..].IndexOf(Terminator);
            if (end <= 0 || end > 20)
                break;
            var name = Text(rom.Slice(at, end + 1));
            if (name is null)
                break;
            names.Add(name);
            at += end + 1;
        }
        return names;
    }

    public static HackDataResult Read(ReadOnlyMemory<byte> bytes, string? symText = null, IdMapper? mapper = null)
    {
        var rom = bytes.Span;
        if (rom.Length is < 0x8000 or > 0x800000)
            return HackDataResult.Refused($"a Game Boy image is 32 KiB to 8 MiB; this one is {rom.Length} bytes");
        var sym = symText is null ? null : SymFile.Parse(symText);
        int gen;
        int species;
        string reader;
        var notes = new List<string>();
        var structuralSlots = 0;
        if (sym?.RomOffset("MonsterNames") is { } s1)
            (gen, species, reader) = (1, s1, ReaderSym);
        else if (sym?.RomOffset("PokemonNames") is { } s2)
            (gen, species, reader) = (2, s2, ReaderSym);
        else if (FindFixed(rom, Gen1Anchor) is >= 0 and var a1)
            (gen, species, reader) = (1, a1, ReaderAnchor);
        else if (FindFixed(rom, Gen2Anchor) is >= 0 and var a2)
            (gen, species, reader) = (2, a2, ReaderAnchor);
        else if (StructuralSpecies(rom, out var structuralWhy) is { } found)
        {
            (gen, species, reader) = (found.Gen, found.Run.Offset, ReaderStructure);
            structuralSlots = found.Run.Count;
            notes.Add(structuralWhy);
        }
        else
            return HackDataResult.Refused(symText is null
                ? $"no Gen 1 (RHYDON, KANGASKHAN, NIDORAN♂) or Gen 2 (BULBASAUR, IVYSAUR, VENUSAUR) species-name table was found, and no .sym file was given (hacks without the vanilla anchor need their .sym); {structuralWhy}"
                : $"the .sym file names neither MonsterNames nor PokemonNames, and no vanilla anchor was found; {structuralWhy}");
        if (sym is not null && reader == ReaderAnchor)
            notes.Add("the .sym file did not name the species table, so the vanilla anchor was used");

        mapper ??= IdMapper.English();
        var strings = GameInfo.GetStrings("en");
        var anchorCap = gen == 1 ? Gen1SpeciesSlots : Gen2SpeciesSlots;
        var cap = reader == ReaderSym ? MaxEntries : reader == ReaderStructure ? Math.Min(structuralSlots, GbStructure.MaxSlots) : gen == 1 ? GbStructure.MaxSlots : Gen2SpeciesSlots;
        var speciesNames = ReadFixed(rom, species, cap);
        if (speciesNames.Count == 0)
            return HackDataResult.Refused($"the species-name table at 0x{species:X} holds no names");
        var nationals = new List<int?>(speciesNames.Count);
        for (var i = 0; i < speciesNames.Count; i++)
        {
            var id = i + 1;
            var national = mapper.Species(speciesNames[i]);
            if (national is null && gen == 1 && id <= byte.MaxValue && SpeciesConverter.GetNational1((byte)id) is var n1 and > 0
                && IdMapper.Normalise(strings.specieslist[n1]) == IdMapper.Normalise(speciesNames[i]))
                national = n1;
            nationals.Add(national);
        }

        var size = gen == 1 ? Gen1BaseSize : Gen2BaseSize;
        var byDex = new Dictionary<int, int>();
        var baseAt = FindBaseStats(rom, gen, sym);
        var structuralRows = false;
        if (baseAt is null && GbStructure.BaseRows(rom, size, gen == 1 ? 5 : 6) is { } structural)
        {
            structuralRows = true;
            baseAt = structural.Offset;
            notes.Add($"the base rows have no vanilla Bulbasaur anchor; a run of {structural.Count} rows at stride {size} whose first byte counts 1, 2, 3 ... with plausible stats is taken at 0x{structural.Offset:X}");
        }
        if (baseAt is { } baseStart)
        {
            for (var i = 0; i < MaxEntries && baseStart + (i + 1) * size <= rom.Length; i++)
            {
                var dex = rom[baseStart + i * size];
                if (dex == 0 || dex != i + 1)
                    break;
                byDex[dex] = baseStart + i * size;
            }
        }

        GbIndexMap? indexMap = null;
        string? mapNote = null;
        var candidates = gen == 1 && reader != ReaderSym ? GbStructure.IndexMaps(rom, byDex.Count) : new List<GbIndexMap>();
        var mapUnchecked = false;
        GbIndexMap? candidate = null;
        var agree = -1;
        var compared = 0;
        foreach (var c in candidates)
        {
            var (a, n) = MapAgreement(rom, c, nationals, speciesNames.Count, byDex);
            if (a > agree)
                (candidate, agree, compared) = (c, a, n);
        }
        if (candidate is not null)
        {
            var among = candidates.Count > 1 ? $" (the best of {candidates.Count} equally wide windows)" : "";
            if (compared >= GbStructure.MinBaseRows / 2 && agree >= FieldVote.MinShare * compared)
            {
                indexMap = candidate;
                mapNote = $"species index map at 0x{candidate.Offset:X}{among}: {candidate.Length} slots name {candidate.Distinct} of the {byDex.Count} dex-ordered base rows, and the row each slot names has the stock types of the slot's species on {agree} of {compared} slots";
            }
            else if (reader == ReaderStructure && nationals.All(x => x is null) && candidates.Count == 1 && Math.Abs(candidate.Length - Math.Min(speciesNames.Count, GbStructure.MaxSlots)) <= MapSlack)
            {
                indexMap = candidate;
                mapUnchecked = true;
                mapNote = $"species index map at 0x{candidate.Offset:X}: the only candidate, {candidate.Length} slots naming {candidate.Distinct} of the {byDex.Count} dex-ordered base rows, as many slots as there are names; no name is a stock species name, so the map cannot be checked against the names and the stats it attaches are shown, not overlaid";
            }
            else
            {
                mapNote = $"a byte run at 0x{candidate.Offset:X}{among} looks like a species index map ({candidate.Length} slots, {candidate.Distinct} distinct dex numbers), but the rows it names have the stock types of the slots' species on only {agree} of {compared} slots ({FieldVote.MinShare * 100:0}% of at least {GbStructure.MinBaseRows / 2} needed), so it is not used";
            }
        }
        var slots = reader == ReaderSym ? speciesNames.Count : indexMap is not null ? Math.Min(speciesNames.Count, indexMap.Length) : reader == ReaderStructure ? speciesNames.Count : Math.Min(speciesNames.Count, anchorCap);
        if (mapNote is not null)
            notes.Add(mapNote);

        var list = new List<HackSpecies>(slots + 1) { new(0, "", null, 0, [], [], [], null, 0, null, null) };
        for (var i = 0; i < slots; i++)
        {
            int? dexOf = indexMap is null ? null : indexMap.Dex[i] is > 0 and var dv ? dv : null;
            list.Add(new HackSpecies(i + 1, speciesNames[i], nationals[i], 0, [], [], [], null, 0, null, null) { Dex = dexOf });
        }

        var filledSpecies = 0;
        var baseRowOf = new Dictionary<int, int>();
        var nationalAttachRefused = false;
        var gen2Unaligned = false;
        if (baseAt is not null)
        {
            if (gen == 1 && indexMap is null)
            {
                var statsCompared = 0;
                var same = 0;
                for (var i = 1; i < list.Count; i++)
                {
                    if (list[i].National is not { } n || n > PersonalTable.StockRB.MaxSpeciesID || !byDex.TryGetValue(n, out var row))
                        continue;
                    statsCompared++;
                    if (SameStats(rom, row, PersonalTable.StockRB[n]) || SameStats(rom, row, PersonalTable.StockY[n]))
                        same++;
                }
                nationalAttachRefused = statsCompared > 0 && same < FieldVote.MinShare * statsCompared;
                if (nationalAttachRefused)
                    notes.Add($"no species index map was found, and only {same} of {statsCompared} base rows the names would take by national number have the stock Red or Yellow stats ({FieldVote.MinShare * 100:0}% needed), so the stats are left unread rather than attached through the national number");
            }
            var filled = 0;
            for (var i = 1; i < list.Count && !nationalAttachRefused; i++)
            {
                var dex = gen == 1 ? indexMap is null ? list[i].National : list[i].Dex : list[i].Id;
                if (dex is { } d && byDex.TryGetValue(d, out var at))
                {
                    list[i] = WithBase(list[i], rom.Slice(at, size), gen);
                    baseRowOf[i] = at;
                    filled++;
                }
            }
            filledSpecies = filled;
            if (gen == 2 && structuralRows && filled > 0)
            {
                var (typeAgree, typeCompared) = Gen2TypeAgreement(rom, list, byDex);
                gen2Unaligned = typeCompared < GbStructure.MinBaseRows || typeAgree < FieldVote.MinShare * typeCompared;
                notes.Add(gen2Unaligned
                    ? $"the base rows found without the Bulbasaur anchor have the stock Crystal types of the species their names give on only {typeAgree} of {typeCompared} rows ({FieldVote.MinShare * 100:0}% of at least {GbStructure.MinBaseRows} needed), so the rows are not shown to line up with the names: they are shown and not overlaid"
                    : $"the base rows found without the Bulbasaur anchor have the stock Crystal types of the species their names give on {typeAgree} of {typeCompared} rows, so they line up with the names");
            }
            notes.Add($"base stats for {filled} species ({byDex.Count} dex-ordered entries; stats in the game's own order, types as the game's own type ids{(gen == 1 ? ", Special given for both Sp. Atk and Sp. Def" : "")})");
        }
        else
        {
            notes.Add("no base-stats table was found");
        }

        var moveStart = sym?.RomOffset("MoveNames") ?? (FindList(rom, MoveAnchor) is >= 0 and var m ? m : null);
        if (moveStart is null && FindList(rom, MoveAnchorShort) is >= 0 and var m2)
        {
            moveStart = m2;
            notes.Add("the move-name list was found by POUND and KARATE CHOP only (the third vanilla name differs)");
        }
        var itemStart = sym?.RomOffset("ItemNames") ?? (FindList(rom, ItemAnchor) is >= 0 and var it ? it : null);
        var nameLists = moveStart is null || itemStart is null ? GbStructure.NameLists(rom, GbStructure.MinShortListNames) : new List<GbNameList>();
        var strides = gen == 1 ? new[] { Gen1MoveSize, Gen2MoveSize } : new[] { Gen2MoveSize, Gen1MoveSize };
        var moves = new List<HackMove> { new(0, "", null, 0, 0, 0, 0) };
        string? movesReason = null;
        List<string>? moveNames = null;
        GbNameList? moveList = null;
        if (moveStart is { } ms)
        {
            moveNames = ListWithGaps(rom, ms, MaxEntries, notes, "move");
        }
        else if (VoteList(nameLists, NameVote.StockMoves, GbStructure.MinListNames, null) is { } votedMoves)
        {
            moveList = votedMoves.List;
            var skip = GbStructure.BestStart(votedMoves.List.Names, NameVote.StockMoves);
            moveNames = [.. votedMoves.List.Names.Skip(skip)];
            notes.Add($"the move-name list has no vanilla anchor; it was found by structure at 0x{votedMoves.List.Offset:X}: {votedMoves.List.Count} names ended by 0x50, {votedMoves.Agree} of {votedMoves.NonBlank} of them stock move names{(skip > 0 ? $"; the first {skip} names do not line up with the stock move order and are left out" : "")}");
        }
        if (moveNames is not null)
        {
            for (var i = 0; i < moveNames.Count; i++)
                moves.Add(new HackMove(i + 1, moveNames[i], moveNames[i].Length == 0 ? null : mapper.Move(i + 1, moveNames[i]), 0, 0, 0, 0));
            GbMoveRows? rows = null;
            foreach (var stride in strides)
            {
                rows = MoveRows(rom, stride, moveNames.Count, moves);
                if (rows is not null)
                    break;
            }
            if (rows is not null)
            {
                FillMoves(rom, moves, rows.Offset, rows.Stride, rows.Count);
                notes.Add($"move rows at 0x{rows.Offset:X}: {rows.Count} rows of {rows.Stride} bytes{(rows.Stride != strides[0] ? $" (not the Gen {gen} stride of {strides[0]})" : "")} whose first byte counts 1, 2, 3 ...{(rows.Renumbered > 0 ? $" on all but {rows.Renumbered} rows (the first byte is the move's animation, which a hack may reuse)" : "")}, checked against {rows.Anchors} of the ten anchor moves (power +2, type +3, accuracy +4 as a fraction of 255, PP +5); the category is the Gen 1-2 type rule");
            }
            else
            {
                movesReason = $"move names only; no run of {strides[0]}- or {strides[1]}-byte rows counting 1, 2, 3 ... gives at least {MoveAnchors.MinAgree} of the ten anchor moves their power, accuracy and PP";
                notes.Add(movesReason);
            }
        }
        else if (ShapedMoveRows(rom, gen, strides) is { } shaped && ClosestList(nameLists, shaped.Count, null) is { } byCount)
        {
            moveList = byCount;
            for (var i = 0; i < byCount.Count; i++)
                moves.Add(new HackMove(i + 1, byCount.Names[i], byCount.Names[i].Length == 0 ? null : mapper.Move(i + 1, byCount.Names[i]), 0, 0, 0, 0));
            FillMoves(rom, moves, shaped.Offset, shaped.Stride, shaped.Count);
            movesReason = $"found by structure only: no list holds stock move names, so the {byCount.Count} names ended by 0x50 at 0x{byCount.Offset:X} are taken as the move names because their count is the closest to the {shaped.Count} move rows of {shaped.Stride} bytes at 0x{shaped.Offset:X} (first byte counts 1, 2, 3 ..., types valid on {MoveAnchors.ShapeShare * 100:0}% and power a multiple of 5 on {MoveAnchors.PowerFiveShare * 100:0}%); no anchor move checks them, so they are shown and not overlaid";
            notes.Add(movesReason);
        }
        else
        {
            notes.Add("no move-name list was found");
            notes.Add($"by structure, none of the {nameLists.Count(l => l.Count >= GbStructure.MinListNames)} lists of at least {GbStructure.MinListNames} names ended by 0x50 holds enough stock move names, and no move rows were matched to one by count");
        }

        var items = new List<HackItem> { new(0, "", null, 0, 0) };
        List<string>? itemNames = null;
        if (itemStart is { } st)
        {
            itemNames = ListWithGaps(rom, st, MaxEntries, notes, "item");
        }
        else
        {
            var ownItems = strings.GetItemStrings(gen == 1 ? EntityContext.Gen1 : EntityContext.Gen2);
            if (VoteList(nameLists, [.. strings.itemlist, .. ownItems], GbStructure.MinShortListNames, moveList) is { } votedItems)
            {
                var skip = GbStructure.BestStart(votedItems.List.Names, ownItems);
                itemNames = [.. votedItems.List.Names.Skip(skip)];
                notes.Add($"the item-name list has no vanilla anchor; it was found by structure at 0x{votedItems.List.Offset:X}: {votedItems.List.Count} names ended by 0x50, {votedItems.Agree} of {votedItems.NonBlank} of them stock item names{(skip > 0 ? $"; the first {skip} names do not line up with the Gen {gen} item order and are left out" : "")}");
            }
        }
        if (itemNames is { } names)
        {
            var own = strings.GetItemStrings(EntityContext.Gen2);
            for (var i = 0; i < names.Count; i++)
            {
                var id = i + 1;
                int? national = null;
                if (names[i].Length > 0 && gen == 2 && id <= byte.MaxValue && id < own.Length && IdMapper.Normalise(own[id]) == IdMapper.Normalise(names[i]))
                {
                    var future = ItemConverter.GetItemFuture2((byte)id);
                    if (ItemConverter.IsItemTransferable34(future))
                        national = future;
                }
                if (names[i].Length > 0)
                    national ??= mapper.ItemByName(names[i]);
                items.Add(new HackItem(id, names[i], national, 0, 0));
            }
        }
        else
        {
            notes.Add("no item-name list was found");
            notes.Add($"by structure, none of the {nameLists.Count(l => l.Count >= GbStructure.MinShortListNames)} lists of at least {GbStructure.MinShortListNames} names ended by 0x50 holds enough stock item names");
        }

        var maxMove = moves.Count > 1 ? moves.Count - 1 : byte.MaxValue;
        var learnSlots = gen == 2 && byDex.Count > 0 ? Math.Min(list.Count - 1, byDex.Count) : list.Count - 1;
        var learnAt = sym?.RomOffset(gen == 1 ? "EvosMovesPointerTable" : "EvosAttacksPointers");
        var learnsets = GbLearnsets.Find(rom, gen, learnSlots, maxMove, out var learnWhy, learnAt);
        var levelUp = new Dictionary<int, IReadOnlyList<HackLearn>>();
        HackTableStatus levelStatus;
        if (learnsets is not null)
        {
            var level1 = 0;
            for (var i = 0; i < learnsets.Count && i + 1 < list.Count; i++)
            {
                if (learnsets.Lists[i] is not { } parsed)
                    continue;
                var id = i + 1;
                var entries = new List<HackLearn>();
                if (gen == 1 && baseRowOf.TryGetValue(id, out var row))
                {
                    for (var k = Gen1LevelOneMoves; k < Gen1LevelOneMoves + 4; k++)
                    {
                        if (rom[row + k] is > 0 and var mv && mv <= maxMove)
                        {
                            entries.Add(new HackLearn(1, mv));
                            level1++;
                        }
                    }
                }
                entries.AddRange(parsed);
                if (entries.Count > 0)
                    levelUp[id] = entries;
            }
            notes.Add($"level-up learnsets from the pointer run at 0x{learnsets.Offset:X}{(learnAt is not null ? " (named by the .sym file)" : "")}: {learnsets.Count} two-byte pointers into bank 0x{learnsets.Offset / GbLearnsets.BankSize:X2}{(learnsets.DataFollows ? ", the first pointing right after the run" : "")}; {learnsets.Read} species' targets parse as an evolution list ended by 0 then (level, move) pairs ended by 0{(learnsets.Unread > 0 ? $", {learnsets.Unread} do not and are left unread" : "")}{(gen == 1 ? $"; {level1} level-1 moves from the base rows (+{Gen1LevelOneMoves} to +{Gen1LevelOneMoves + 3})" : "")}{(learnsets.Candidates > 1 ? $"; {learnsets.Candidates} runs passed and the one with the fewest unread lists was taken" : "")}");
            levelStatus = learnsets.Unread == 0
                ? new HackTableStatus(HackTableStatus.LevelUp, HackTableStatus.Read)
                : new HackTableStatus(HackTableStatus.LevelUp, HackTableStatus.Partial, $"{learnsets.Unread} of {learnsets.Count} species' level-up lists do not parse with the base game's record sizes, so they are left unread");
        }
        else
        {
            var reason = $"level-up not read: {learnWhy}";
            notes.Add(reason);
            levelStatus = new HackTableStatus(HackTableStatus.LevelUp, HackTableStatus.Missing, reason);
        }

        HackTableStatus[] status =
        [
            filledSpecies > 0 && mapUnchecked
                ? new HackTableStatus(HackTableStatus.Species, HackTableStatus.Partial, "no species name is a stock name, so the species index map that attaches the base rows is not checked against the names: the stats are shown and not overlaid", false)
            : filledSpecies > 0 && gen2Unaligned
                ? new HackTableStatus(HackTableStatus.Species, HackTableStatus.Partial, "the base rows were found without the Bulbasaur anchor and their types do not match the stock types of the species the names give, so they are not shown to line up", false)
            : filledSpecies > 0
                ? gen == 1 && indexMap is null
                    ? new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, "base rows are attached through the national number each name maps to, not checked against the game's own dex order")
                    : new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, null, true)
                : new HackTableStatus(HackTableStatus.Species, HackTableStatus.Partial, nationalAttachRefused ? "species names only: without a species index map the base rows do not match the stock stats through the national numbers, so none were attached" : "species names only: no base stats were attached to any species"),
            moves.Count > 1
                ? movesReason is null
                    ? new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Read)
                    : new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Partial, movesReason)
                : new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Missing, "no move-name list was found"),
            HackTableStatus.Of(HackTableStatus.Items, items.Count > 1, "no item-name list was found"),
            new HackTableStatus(HackTableStatus.Abilities, HackTableStatus.Missing, "no ability table is read for Game Boy images"),
            levelStatus,
        ];
        return new HackDataResult(HackTables.Finish(new HackData
        {
            RomSha1 = Convert.ToHexStringLower(SHA1.HashData(rom)),
            Reader = reader,
            Species = list,
            Moves = moves,
            Items = items,
            LevelUp = levelUp,
            Notes = [$"Gen {gen}", .. notes],
        }, HackTables.Gb, null, gen == 1 ? HackTables.Gb1Engine : HackTables.Gb2Engine, status), null);
    }
}
