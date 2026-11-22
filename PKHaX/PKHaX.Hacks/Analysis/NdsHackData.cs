using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public sealed record NdsLayout(string Family, int Generation, string[] Personal, string[] Learnsets, string[] Text, string[] Moves, int PersonalSize);

public static class NdsHackData
{
    public const string Reader = "nds-narc";

    public static readonly NdsLayout DiamondPearl = new("dp", 4, ["poketool/personal/personal.narc"], ["poketool/personal/wotbl.narc"], ["msgdata/msg.narc"], ["poketool/waza/waza_tbl.narc"], PersonalInfo4.SIZE);
    public static readonly NdsLayout Platinum = new("pt", 4, ["poketool/personal/pl_personal.narc"], ["poketool/personal/wotbl.narc"], ["msgdata/pl_msg.narc"], ["poketool/waza/pl_waza_tbl.narc"], PersonalInfo4.SIZE);
    public static readonly NdsLayout HeartGoldSoulSilver = new("hgss", 4, ["a/0/0/2"], ["a/0/3/3"], ["a/0/2/7"], ["a/0/1/1"], PersonalInfo4.SIZE);
    public static readonly NdsLayout BlackWhite = new("bw", 5, ["a/0/1/6"], ["a/0/1/8"], ["a/0/0/2"], ["a/0/2/1"], PersonalInfo5BW.SIZE);
    public static readonly NdsLayout Black2White2 = new("b2w2", 5, ["a/0/1/6"], ["a/0/1/8"], ["a/0/0/2"], ["a/0/2/1"], PersonalInfo5B2W2.SIZE);

    public static NdsLayout? LayoutFor(string gameCode) => gameCode.Length < 3 ? null : gameCode[..3] switch
    {
        "ADA" or "APA" => DiamondPearl,
        "CPU" => Platinum,
        "IPK" or "IPG" => HeartGoldSoulSilver,
        "IRB" or "IRA" => BlackWhite,
        "IRE" or "IRD" => Black2White2,
        _ => null,
    };

    public static HackDataResult Read(ReadOnlyMemory<byte> bytes, string romSha1, IdMapper? mapper = null)
    {
        NitroFs fs;
        try
        {
            fs = new NitroFs(bytes);
        }
        catch (ArgumentException ex)
        {
            return HackDataResult.Refused(ex.Message);
        }
        if (LayoutFor(fs.GameCode) is not { } layout)
            return HackDataResult.Refused($"game code {fs.GameCode} is not a Diamond, Pearl, Platinum, HeartGold, SoulSilver, Black, White, Black 2 or White 2 base, so no NDS table layout is known for it.");
        try
        {
            return Read(fs, layout, romSha1, mapper ?? IdMapper.English());
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException)
        {
            return HackDataResult.Refused($"the NDS tables could not be read: {ex.Message}");
        }
    }

    public static HackDataResult Read(NitroFs fs, NdsLayout layout, string romSha1, IdMapper mapper)
    {
        var notes = new List<string> { $"NDS {layout.Family}, game code {fs.GameCode}" };
        var personalFiles = FirstNarc(fs, layout.Personal, out var personalPath);
        if (personalFiles is null)
            return HackDataResult.Refused($"the personal NARC ({string.Join(" or ", layout.Personal)}) is missing or is not a NARC.");
        var personalRows = PersonalRows(personalFiles, out var dropped);
        if (dropped > 0)
            notes.Add($"{dropped} trailing member{(dropped == 1 ? "" : "s")} of {personalPath} {(dropped == 1 ? "is" : "are")} not the size the other members share, so {(dropped == 1 ? "it is" : "they are")} dropped and not read as a personal entry");
        var personal = Concat(personalRows, layout.PersonalSize, out var shortEntries);
        if (shortEntries > 0)
            notes.Add($"{shortEntries} personal entries in {personalPath} are shorter than {layout.PersonalSize} bytes and read as zero-padded");
        IPersonalTable table = layout.PersonalSize switch
        {
            PersonalInfo5BW.SIZE => new PersonalTable5BW(personal),
            PersonalInfo5B2W2.SIZE => new PersonalTable5B2W2(personal),
            _ => new PersonalTable4(personal),
        };
        string? byIndexOnly = null;
        if (table.Count < 2 || table[1].HP != 45 || table[1].ATK != 49 || table[1].DEF != 49)
        {
            var (plausibleRows, filledRows) = Plausible(table);
            if (filledRows < NameVote.MinAgree || plausibleRows < Gen3LayoutFinder.MinPlausibleShare * filledRows)
                return HackDataResult.Refused($"personal entry 1 in {personalPath} is not Bulbasaur's 45/49/49, and only {plausibleRows} of the {filledRows} non-blank entries among 1-{Gen3LayoutFinder.Window} have plausible base stats ({Gen3LayoutFinder.MinPlausibleShare * 100:0}% of at least {NameVote.MinAgree} needed), so the personal NARC layout is not the vanilla one.");
            notes.Add($"personal entry 1 is not Bulbasaur's 45/49/49, but {plausibleRows} of the {filledRows} non-blank entries among 1-{Gen3LayoutFinder.Window} have plausible base stats, so the NARC is read");
            byIndexOnly = "personal entry 1 is not Bulbasaur's, so the rows were taken on plausible stats; they are matched to the names and written by personal index and are not checked against the stock types";
        }

        var text = FirstNarc(fs, layout.Text, out var textPath);
        Func<ReadOnlyMemory<byte>, IReadOnlyList<string>?> decode = layout.Generation == 4 ? b => GameText.ReadGen4(b.Span) : b => GameText.ReadGen5(b.Span);
        IReadOnlyList<string> speciesNames = [], moveNames = [], itemNames = [], abilityNames = [];
        if (text is null)
        {
            notes.Add($"the text NARC ({string.Join(" or ", layout.Text)}) is missing, so no names are read");
        }
        else
        {
            speciesNames = Bank(text, decode, "Bulbasaur", "Ivysaur", "species", textPath, notes);
            moveNames = Bank(text, decode, "Pound", "Karate Chop", "move", textPath, notes);
            itemNames = Bank(text, decode, "Master Ball", "Ultra Ball", "item", textPath, notes);
            abilityNames = Bank(text, decode, "Stench", "Drizzle", "ability", textPath, notes);
        }

        var namedCount = speciesNames.Count;
        while (namedCount > 1 && IdMapper.Normalise(speciesNames[namedCount - 1]) is "EGG" or "BADEGG" or "")
            namedCount--;
        if (namedCount < speciesNames.Count)
            notes.Add($"{speciesNames.Count - namedCount} trailing species-name lines (Egg, Bad Egg or empty) are not species");
        var speciesCount = namedCount > 0 ? Math.Min(namedCount, table.Count) : table.Count;
        var species = new List<HackSpecies>(speciesCount);
        for (var i = 0; i < speciesCount; i++)
        {
            var p = table[i];
            var name = i < speciesNames.Count ? speciesNames[i] : "";
            int[] abilities = [.. Enumerable.Range(0, p.AbilityCount).Select(p.GetAbilityAtIndex)];
            species.Add(new HackSpecies(i, name, i == 0 || name.Length == 0 ? null : mapper.Species(name), 0,
                [p.Type1, p.Type2], [p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], abilities, p.EXPGrowth, p.Gender, p.CatchRate, p.BaseEXP) { GenderRead = true });
        }
        var forms = FormRows(table, layout, speciesCount, species, out var unlinked);
        if (table.Count > speciesCount)
            notes.Add($"{table.Count - speciesCount} personal entries past the {speciesCount} named species are form entries, listed as form rows: {forms.Count - unlinked} linked to their species through {(layout.Generation == 4 ? "PKHeX's fixed Gen 4 form order" : "the form index stored in the hack's own personal rows")}, {unlinked} that no species points at");

        var moveData = FirstNarc(fs, layout.Moves, out _);
        var moveCount = moveNames.Count > 0 ? moveNames.Count : moveData?.Count ?? 0;
        if (moveData is null)
            notes.Add($"the move data NARC ({string.Join(" or ", layout.Moves)}) is missing, so move power, type, accuracy and PP are not read");
        var moves = new List<HackMove>(moveCount);
        for (var i = 0; i < moveCount; i++)
        {
            var name = i < moveNames.Count ? moveNames[i] : "";
            var national = i == 0 || name.Length == 0 ? null : mapper.Move(i, name);
            var m = moveData is not null && i < moveData.Count ? moveData[i].Span : default;
            int? category = m.Length > 2 ? Category(layout.Generation, m[2]) : null;
            moves.Add(layout.Generation == 4
                ? new HackMove(i, name, national, At(m, 3), At(m, 4), At(m, 5), At(m, 6)) { Category = category }
                : new HackMove(i, name, national, At(m, 3), At(m, 0), At(m, 4), At(m, 5)) { Category = category });
        }

        var items = new List<HackItem>(itemNames.Count);
        for (var i = 0; i < itemNames.Count; i++)
            items.Add(new HackItem(i, itemNames[i], i == 0 || itemNames[i].Length == 0 ? null : mapper.ItemByName(itemNames[i]), 0, 0));
        if (items.Count > 0)
            notes.Add("items are read by name only (no pocket or price)");

        var abilityList = new List<HackAbility>(abilityNames.Count);
        for (var i = 0; i < abilityNames.Count; i++)
            abilityList.Add(new HackAbility(i, abilityNames[i], i == 0 || abilityNames[i].Length == 0 ? null : mapper.Ability(i, abilityNames[i])));

        var levelUp = new Dictionary<int, IReadOnlyList<HackLearn>>();
        var learn = FirstNarc(fs, layout.Learnsets, out var learnPath);
        if (learn is null)
            notes.Add($"the learnset NARC ({string.Join(" or ", layout.Learnsets)}) is missing, so level-up moves are not read");
        else
        {
            for (var i = 1; i < Math.Min(learn.Count, speciesCount); i++)
            {
                var list = layout.Generation == 4 ? Learnset4(learn[i].Span) : Learnset5(learn[i].Span);
                if (list.Count > 0)
                    levelUp[i] = list;
            }
            notes.Add($"level-up learnsets from {learnPath}");
        }

        var movesReason = moveNames.Count == 0 ? "no move names were read"
            : moveData is null ? $"move names only: the move data NARC ({string.Join(" or ", layout.Moves)}) is missing"
            : null;
        HackTableStatus[] status =
        [
            new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, speciesNames.Count > 0 ? byIndexOnly : byIndexOnly is null ? "the species have stats but no names" : $"the species have stats but no names; {byIndexOnly}", byIndexOnly is null ? true : null),
            new HackTableStatus(HackTableStatus.Moves, moveNames.Count == 0 ? HackTableStatus.Missing : moveData is null ? HackTableStatus.Partial : HackTableStatus.Read, movesReason),
            HackTableStatus.Of(HackTableStatus.Items, itemNames.Count > 0, "no item names were read"),
            HackTableStatus.Of(HackTableStatus.Abilities, abilityNames.Count > 0, "no ability names were read"),
            HackTableStatus.Of(HackTableStatus.LevelUp, levelUp.Count > 0, "no level-up learnsets were read"),
        ];
        return new HackDataResult(HackTables.Finish(new HackData
        {
            RomSha1 = romSha1,
            Reader = Reader,
            Species = species,
            Forms = forms,
            Moves = moves,
            Items = items,
            Abilities = abilityList,
            LevelUp = levelUp,
            Notes = notes,
        }, HackTables.Nds, fs.GameCode, layout.Generation == 4 ? HackTables.NdsGen4Engine : HackTables.NdsGen5Engine, status), null);
    }

    public const double SharedSizeShare = 0.9;

    public static int? Category(int generation, byte raw) => (generation, raw) switch
    {
        (4, 0) => HackMove.Physical,
        (4, 1) => HackMove.Special,
        (4, 2) => HackMove.StatusCategory,
        (5, 0) => HackMove.StatusCategory,
        (5, 1) => HackMove.Physical,
        (5, 2) => HackMove.Special,
        _ => null,
    };

    public static (int Plausible, int Filled) Plausible(IPersonalTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var plausible = 0;
        var filled = 0;
        Span<byte> six = stackalloc byte[6];
        for (var i = 1; i <= Math.Min(Gen3LayoutFinder.Window, table.Count - 1); i++)
        {
            var p = table[i];
            if (p.HP == 0 && p.ATK == 0 && p.DEF == 0 && p.SPE == 0 && p.SPA == 0 && p.SPD == 0)
                continue;
            filled++;
            six[0] = (byte)p.HP;
            six[1] = (byte)p.ATK;
            six[2] = (byte)p.DEF;
            six[3] = (byte)p.SPE;
            six[4] = (byte)p.SPA;
            six[5] = (byte)p.SPD;
            if (Gen3LayoutFinder.PlausibleStats(six))
                plausible++;
        }
        return (plausible, filled);
    }

    public static IPersonalTable? StockFor(NdsLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout.Family switch
        {
            "dp" => PersonalTable.StockDP,
            "pt" => PersonalTable.StockPt,
            "hgss" => PersonalTable.StockHGSS,
            _ => null,
        };
    }

    public static List<HackSpecies> FormRows(IPersonalTable table, NdsLayout layout, int speciesCount, IReadOnlyList<HackSpecies> species, out int unlinked)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(species);
        var links = new Dictionary<int, (int Species, int Form)>();
        var formTable = layout.Generation == 4 ? StockFor(layout) : table;
        for (var s = 1; formTable is not null && s < Math.Min(speciesCount, formTable.Count); s++)
        {
            var info = formTable[s];
            for (var f = 1; f < info.FormCount; f++)
            {
                var index = info.FormIndex((ushort)s, (byte)f);
                if (index >= speciesCount && index < table.Count)
                    links.TryAdd(index, (s, f));
            }
        }
        var forms = new List<HackSpecies>();
        unlinked = 0;
        for (var i = speciesCount; i < table.Count; i++)
        {
            var p = table[i];
            int[] abilities = [.. Enumerable.Range(0, p.AbilityCount).Select(p.GetAbilityAtIndex)];
            if (links.TryGetValue(i, out var link))
            {
                var baseRow = link.Species < species.Count ? species[link.Species] : null;
                forms.Add(new HackSpecies(i, baseRow?.Name ?? "", baseRow?.National, link.Form,
                    [p.Type1, p.Type2], [p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], abilities, p.EXPGrowth, p.Gender, p.CatchRate, p.BaseEXP) { GenderRead = true, BaseSpecies = link.Species });
                continue;
            }
            unlinked++;
            forms.Add(new HackSpecies(i, "", null, 0,
                [p.Type1, p.Type2], [p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], abilities, p.EXPGrowth, p.Gender, p.CatchRate, p.BaseEXP) { GenderRead = true });
        }
        return forms;
    }

    public static IReadOnlyList<ReadOnlyMemory<byte>> PersonalRows(IReadOnlyList<ReadOnlyMemory<byte>> members, out int dropped)
    {
        dropped = 0;
        if (members.Count == 0)
            return members;
        var bySize = new Dictionary<int, int>();
        foreach (var m in members)
            bySize[m.Length] = bySize.TryGetValue(m.Length, out var n) ? n + 1 : 1;
        var shared = bySize.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First();
        if (shared.Value < members.Count * SharedSizeShare)
            return members;
        var keep = members.Count;
        while (keep > 0 && members[keep - 1].Length != shared.Key)
            keep--;
        dropped = members.Count - keep;
        return dropped == 0 ? members : [.. members.Take(keep)];
    }

    public static IReadOnlyList<HackLearn> Learnset4(ReadOnlySpan<byte> s)
    {
        var list = new List<HackLearn>();
        for (var o = 0; o + 2 <= s.Length; o += 2)
        {
            var v = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(o));
            if (v == 0xFFFF)
                break;
            list.Add(new HackLearn(v >> 9, v & 0x1FF));
        }
        return list;
    }

    public static IReadOnlyList<HackLearn> Learnset5(ReadOnlySpan<byte> s)
    {
        var list = new List<HackLearn>();
        for (var o = 0; o + 4 <= s.Length; o += 4)
        {
            var move = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(o));
            var level = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(o + 2));
            if (move == 0xFFFF)
                break;
            list.Add(new HackLearn(level, move));
        }
        return list;
    }

    private static IReadOnlyList<string> Bank(IReadOnlyList<ReadOnlyMemory<byte>> text, Func<ReadOnlyMemory<byte>, IReadOnlyList<string>?> decode,
        string line1, string line2, string what, string? path, List<string> notes)
    {
        if (GameText.FindBank(text, decode, line1, line2, out var lines) is { } bank)
        {
            notes.Add($"{what} names from {path} bank {bank}");
            return lines!;
        }
        notes.Add($"no bank in {path} has {line1} and {line2} as lines 1 and 2, so {what} names are not read");
        return [];
    }

    private static IReadOnlyList<ReadOnlyMemory<byte>>? FirstNarc(NitroFs fs, string[] paths, out string? path)
    {
        foreach (var p in paths)
        {
            if (Narc.Read(fs, p) is { } files)
            {
                path = p;
                return files;
            }
        }
        path = null;
        return null;
    }

    private static Memory<byte> Concat(IReadOnlyList<ReadOnlyMemory<byte>> files, int size, out int shortEntries)
    {
        var data = new byte[files.Count * size];
        shortEntries = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var f = files[i].Span;
            if (f.Length < size)
                shortEntries++;
            f.Slice(0, Math.Min(size, f.Length)).CopyTo(data.AsSpan(i * size));
        }
        return data;
    }

    private static int At(ReadOnlySpan<byte> s, int i) => i < s.Length ? s[i] : 0;
}
