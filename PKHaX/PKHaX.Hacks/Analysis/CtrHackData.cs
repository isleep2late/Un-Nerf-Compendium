using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public sealed record CtrLayout(string Family, string Personal, string Learnsets, string[] Text, int PersonalSize);

public static class CtrHackData
{
    public const string Reader = "ctr-garc";
    public const string ReaderLgpe = "lgpe-romfs";
    public const string LgpePersonal = "bin/pokelib/personal/personal_total.bin";

    public static readonly CtrLayout XY = new("xy", "a/2/1/8", "a/2/1/4", ["a/0/7/4", "a/0/7/2", "a/0/7/3"], PersonalInfo6XY.SIZE);
    public static readonly CtrLayout ORAS = new("oras", "a/1/9/5", "a/1/9/1", ["a/0/7/3", "a/0/7/1", "a/0/7/2"], PersonalInfo6AO.SIZE);
    public static readonly CtrLayout SMUSUM = new("sm", "a/0/1/7", "a/0/1/3", ["a/0/3/2", "a/0/3/0", "a/0/3/1", "a/0/3/3"], PersonalInfo7.SIZE);

    public static IReadOnlyList<CtrLayout> Layouts => [SMUSUM, ORAS, XY];

    public static HackDataResult Read(CtrRomFs fs, string imageKey, IdMapper? mapper = null)
    {
        mapper ??= IdMapper.English();
        var tried = new List<string>();
        try
        {
            foreach (var layout in Layouts)
            {
                if (!fs.Has(layout.Personal))
                    continue;
                var r = Read(fs, layout, imageKey, mapper);
                if (r.Ok)
                    return r;
                tried.Add($"{layout.Family}: {r.Refusal}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IndexOutOfRangeException or IOException)
        {
            return HackDataResult.Refused($"the romfs could not be read: {ex.Message}");
        }
        return HackDataResult.Refused(tried.Count == 0
            ? $"no personal GARC at {string.Join(", ", Layouts.Select(l => l.Personal))}, so this romfs is not an XY, ORAS, SM or USUM one."
            : string.Join(" ", tried));
    }

    private static HackDataResult Read(CtrRomFs fs, CtrLayout layout, string imageKey, IdMapper mapper)
    {
        var notes = new List<string>();
        var personalFiles = Garc.Read(fs.File(layout.Personal)!.Value);
        if (personalFiles is null || personalFiles.Count == 0)
            return HackDataResult.Refused($"{layout.Personal} is not a GARC.");
        var last = personalFiles[^1];
        Memory<byte> personal;
        if (last.Length >= 2 * layout.PersonalSize && last.Length % layout.PersonalSize == 0)
            personal = last.ToArray();
        else
        {
            personal = new byte[personalFiles.Count * layout.PersonalSize];
            for (var i = 0; i < personalFiles.Count; i++)
                personalFiles[i].Span.Slice(0, Math.Min(layout.PersonalSize, personalFiles[i].Length)).CopyTo(personal.Span.Slice(i * layout.PersonalSize));
            notes.Add($"{layout.Personal}'s last sub-file is not the whole table, so the table is joined from the per-species files");
        }
        var family = layout.Family;
        IPersonalTable table;
        if (layout == XY)
            table = new PersonalTable6XY(personal);
        else if (layout == ORAS)
            table = new PersonalTable6AO(personal);
        else
        {
            family = personal.Length / layout.PersonalSize >= PersonalTable.StockUSUM.Count ? "usum" : "sm";
            table = new PersonalTable7(personal, family == "usum" ? PersonalTable.StockUSUM.MaxSpeciesID : PersonalTable.StockSM.MaxSpeciesID);
        }
        notes.Insert(0, $"3DS {family}{(fs.FromOverlay(layout.Personal) ? ", personal from the overlay" : "")}");
        if (table.Count < 2 || table[1].HP != 45 || table[1].ATK != 49 || table[1].DEF != 49)
            return HackDataResult.Refused($"personal entry 1 in {layout.Personal} is not Bulbasaur's 45/49/49, so the personal GARC layout is not the vanilla one.");

        IReadOnlyList<string> speciesNames = [], moveNames = [], itemNames = [], abilityNames = [];
        Func<ReadOnlyMemory<byte>, IReadOnlyList<string>?> decode = b => GameText.ReadGen6(b.Span);
        string? textPath = null;
        IReadOnlyList<ReadOnlyMemory<byte>>? text = null;
        foreach (var candidate in layout.Text)
        {
            if (!fs.Has(candidate) || Garc.Read(fs.File(candidate)!.Value) is not { } garc)
                continue;
            if (GameText.FindBank(garc, decode, "Bulbasaur", "Ivysaur", out _) is not null)
            {
                (textPath, text) = (candidate, garc);
                break;
            }
        }
        if (text is null)
            notes.Add($"no text GARC at {string.Join(", ", layout.Text)} has a file whose lines 1-2 are Bulbasaur and Ivysaur, so no names are read");
        else
        {
            speciesNames = Bank(text, decode, "Bulbasaur", "Ivysaur", "species", textPath, notes);
            moveNames = Bank(text, decode, "Pound", "Karate Chop", "move", textPath, notes);
            itemNames = Bank(text, decode, "Master Ball", "Ultra Ball", "item", textPath, notes);
            abilityNames = Bank(text, decode, "Stench", "Drizzle", "ability", textPath, notes);
        }

        var lastSpecies = Math.Min(table.Count, table.MaxSpeciesID + 1);
        var speciesCount = speciesNames.Count > 0 ? Math.Min(speciesNames.Count, lastSpecies) : lastSpecies;
        var species = Species(table, speciesCount, i => i < speciesNames.Count ? speciesNames[i] : "", mapper);
        if (table.Count > speciesCount)
            notes.Add($"{table.Count - speciesCount} personal entries past the {speciesCount} named species are form entries and are not listed as species");

        var levelUp = new Dictionary<int, IReadOnlyList<HackLearn>>();
        var learn = fs.Has(layout.Learnsets) ? Garc.Read(fs.File(layout.Learnsets)!.Value) : null;
        if (learn is null)
            notes.Add($"no learnset GARC at {layout.Learnsets}, so level-up moves are not read");
        else
        {
            for (var i = 1; i < Math.Min(learn.Count, speciesCount); i++)
            {
                var list = NdsHackData.Learnset5(learn[i].Span);
                if (list.Count > 0)
                    levelUp[i] = list;
            }
            notes.Add($"level-up learnsets from {layout.Learnsets}");
        }
        notes.Add("move power, type, accuracy and PP are not read for 3DS (the move GARCs are packed per title); items are read by name only");

        HackTableStatus[] status =
        [
            new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, speciesNames.Count > 0 ? null : "the species have stats but no names", true),
            moveNames.Count > 0
                ? new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Partial, "move names only; move power, type, accuracy and PP are not read for 3DS")
                : new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Missing, "no move names were read"),
            HackTableStatus.Of(HackTableStatus.Items, itemNames.Count > 0, "no item names were read"),
            HackTableStatus.Of(HackTableStatus.Abilities, abilityNames.Count > 0, "no ability names were read"),
            HackTableStatus.Of(HackTableStatus.LevelUp, levelUp.Count > 0, "no level-up learnsets were read"),
        ];
        return new HackDataResult(HackTables.Finish(new HackData
        {
            RomSha1 = imageKey,
            Reader = Reader,
            Species = species,
            Moves = [.. moveNames.Select((n, i) => new HackMove(i, n, i == 0 || n.Length == 0 ? null : mapper.Move(i, n), 0, 0, 0, 0))],
            Items = [.. itemNames.Select((n, i) => new HackItem(i, n, i == 0 || n.Length == 0 ? null : mapper.ItemByName(n), 0, 0))],
            Abilities = [.. abilityNames.Select((n, i) => new HackAbility(i, n, i == 0 || n.Length == 0 ? null : mapper.Ability(i, n)))],
            LevelUp = levelUp,
            Notes = notes,
        }, HackTables.Ctr, null, layout == XY || layout == ORAS ? HackTables.CtrGen6Engine : HackTables.CtrGen7Engine, status), null);
    }

    public static HackDataResult ReadLgpe(CtrRomFs fs, string imageKey, IdMapper? mapper = null)
    {
        mapper ??= IdMapper.English();
        if (fs.File(LgpePersonal) is not { } bytes)
            return HackDataResult.Refused($"{LgpePersonal} is not in the romfs or its overlay, so this is not a Let's Go romfs.");
        if (bytes.Length < 2 * PersonalInfo7GG.SIZE || bytes.Length % PersonalInfo7GG.SIZE != 0)
            return HackDataResult.Refused($"{LgpePersonal} is {bytes.Length} bytes, not a whole number of {PersonalInfo7GG.SIZE}-byte entries.");
        var table = new PersonalTable7GG(bytes.ToArray());
        if (table[1].HP != 45 || table[1].ATK != 49 || table[1].DEF != 49)
            return HackDataResult.Refused($"personal entry 1 in {LgpePersonal} is not Bulbasaur's 45/49/49.");
        var en = GameInfo.GetStrings("en").specieslist;
        var count = Math.Min(table.MaxSpeciesID + 1, en.Length);
        var species = Species(table, count, i => en[i], mapper);
        List<string> notes =
        [
            $"Let's Go{(fs.FromOverlay(LgpePersonal) ? ", personal from the overlay" : "")}",
            "names are PKHeX's English names (Let's Go hacks keep the vanilla names; the romfs text is not read)",
            "level-up learnsets, moves, items and abilities are not read for Let's Go",
        ];
        var changed = Enumerable.Range(1, count - 1).Count(i => table[i].BaseEXP != PersonalTable.StockGG[i].BaseEXP);
        if (changed > 0)
            notes.Add($"{changed} species have an EXP yield different from vanilla");
        HackTableStatus[] status =
        [
            new HackTableStatus(HackTableStatus.Species, HackTableStatus.Read, null, true),
            new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Missing, "moves are not read for Let's Go"),
            new HackTableStatus(HackTableStatus.Items, HackTableStatus.Missing, "items are not read for Let's Go"),
            new HackTableStatus(HackTableStatus.Abilities, HackTableStatus.Missing, "abilities are not read for Let's Go"),
            new HackTableStatus(HackTableStatus.LevelUp, HackTableStatus.Missing, "level-up learnsets are not read for Let's Go"),
        ];
        return new HackDataResult(HackTables.Finish(new HackData { RomSha1 = imageKey, Reader = ReaderLgpe, Species = species, Notes = notes }, HackTables.Nx, null, HackTables.LgpeEngine, status), null);
    }

    private static List<HackSpecies> Species(IPersonalTable table, int count, Func<int, string> name, IdMapper mapper)
    {
        var species = new List<HackSpecies>(count);
        for (var i = 0; i < count; i++)
        {
            var p = table[i];
            var n = name(i);
            int[] abilities = [.. Enumerable.Range(0, p.AbilityCount).Select(p.GetAbilityAtIndex)];
            species.Add(new HackSpecies(i, n, i == 0 || n.Length == 0 ? null : mapper.Species(n), 0,
                [p.Type1, p.Type2], [p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], abilities, p.EXPGrowth, p.Gender, p.CatchRate, p.BaseEXP) { GenderRead = true });
        }
        return species;
    }

    private static IReadOnlyList<string> Bank(IReadOnlyList<ReadOnlyMemory<byte>> text, Func<ReadOnlyMemory<byte>, IReadOnlyList<string>?> decode,
        string line1, string line2, string what, string? path, List<string> notes)
    {
        if (GameText.FindBank(text, decode, line1, line2, out var lines) is { } bank)
        {
            notes.Add($"{what} names from {path} file {bank}");
            return lines!;
        }
        notes.Add($"no file in {path} has {line1} and {line2} as lines 1 and 2, so {what} names are not read");
        return [];
    }
}
