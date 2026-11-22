using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;

namespace PKHaX.Hacks.Overlay;

public sealed record HackOverlay(GameVersion Version, EntityContext Context, Memory<byte> Personal, StringsPatch Strings, IReadOnlyList<string> Notes)
{
    public const int PkhexFairy = 17;

    public MoveOverlay? Moves { get; init; }

    public static HackOverlay From(HackData data, GameVersion version)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!version.IsValidSavedVersion())
            throw new ArgumentException($"{version} is not a single game; pass the game the hack is built on, such as Pt or B2.", nameof(version));
        var stock = GameData.GetPersonal(version);
        var size = stock[0].Write().Length;
        var bytes = new byte[stock.Count * size];
        for (var i = 0; i < stock.Count; i++)
            stock[i].Write().CopyTo(bytes.AsSpan(i * size));
        var (context, table) = PersonalOverlay.Create(version, bytes);
        var generation = version.Generation;
        var changed = 0;
        var fairy = 0;
        var outside = 0;
        var unmapped = 0;
        var partial = 0;
        var statsTaken = data.SpeciesOverlayable;
        foreach (var s in data.Species)
        {
            if (!statsTaken || s.Id <= 0 || s.BaseStats.Count != 6 || s.BaseStats.All(b => b == 0))
                continue;
            if (SpeciesIndex(generation, s) is not { } species)
            {
                unmapped++;
                continue;
            }
            if (species > table.MaxSpeciesID)
            {
                outside++;
                continue;
            }
            Apply(table[(ushort)species], s, generation, ref fairy);
            if (!s.GenderRead || s.Abilities.Count < 2)
                partial++;
            changed++;
        }
        var formsTaken = 0;
        string? formNote = null;
        var linkedForms = data.Forms.Where(f => f.BaseSpecies is not null && f.BaseStats.Count == 6 && f.BaseStats.Any(b => b != 0)).ToList();
        if (statsTaken && generation >= 4 && linkedForms.Count > 0)
        {
            var hackRows = data.Species.Count + data.Forms.Count;
            if (hackRows != table.Count)
                formNote = $"the hack has {hackRows} personal rows and the stock {version} table {table.Count}, so its {linkedForms.Count} form rows are not written and the alternate forms keep the stock stats";
            else
            {
                foreach (var f in linkedForms)
                {
                    if (f.Id <= table.MaxSpeciesID || f.Id >= table.Count)
                        continue;
                    Apply(table[f.Id], f, generation, ref fairy);
                    formsTaken++;
                }
                formNote = $"{formsTaken} alternate-form rows take the hack's stats by personal index";
            }
        }
        var notes = new List<string> { $"{changed} species take the hack's stats, types and abilities over the stock {version} table" };
        if (!statsTaken)
        {
            var status = data.Table(HackTableStatus.Species);
            notes.Add($"the species table is {status?.Status ?? HackTableStatus.Partial}{(status?.Aligned == false ? ", not aligned with the names" : "")}{(status?.Reason is { Length: > 0 } reason ? $" ({reason})" : "")}, so the stock stats stay and only the names are taken");
        }
        if (fairy > 0)
            notes.Add($"{fairy} type slots use the ??? id ({GameTypes.MysteryRaw34}), which this hack uses for Fairy; they show as Fairy");
        if (outside > 0)
            notes.Add($"{outside} species lie past the stock table's {table.MaxSpeciesID} and keep no personal entry here");
        if (unmapped > 0)
            notes.Add($"{unmapped} species have no national id, so no stock entry takes their stats");
        if (partial > 0)
            notes.Add($"{partial} species were read without their gender or abilities, which keep the stock values");
        if (formNote is not null)
            notes.Add(formNote);
        else if (table.Count > table.MaxSpeciesID + 1)
            notes.Add($"the {table.Count - table.MaxSpeciesID - 1} alternate-form entries keep the stock stats (the reader reads named species only)");
        var en = GameInfo.GetStrings("en");
        var beyond = 0;
        var ownItems = StringsOverlay.HasOwnItemIndex(context);
        var items = Renamed(data.Items.Select(x => (x.Id, x.Name)), ownItems ? en.GetItemStrings(context, version) : en.itemlist, ref beyond);
        var strings = new StringsPatch
        {
            Species = Renamed(data.Species.Select(x => (SpeciesIndex(generation, x) ?? 0, x.Name)), en.specieslist, ref beyond),
            Moves = Renamed(data.Moves.Select(x => (x.Id, x.Name)), en.movelist, ref beyond),
            Items = ownItems ? new Dictionary<int, string>() : items,
            ContextItems = ownItems ? items : new Dictionary<int, string>(),
            Abilities = Renamed(data.Abilities.Select(x => (generation <= 3 ? x.National ?? 0 : x.Id, x.Name)), en.abilitylist, ref beyond),
        };
        if (beyond > 0)
            notes.Add($"{beyond} names lie past PKHeX's own lists and are not shown by name");
        var moves = MoveOverlay.From(data, context, generation);
        notes.AddRange(moves.Notes);
        return new HackOverlay(version, context, bytes, strings, notes) { Moves = moves };
    }

    public IDisposable Use(string language = "en")
    {
        var personal = PersonalOverlay.Use(Version, Personal);
        IDisposable? strings = null;
        try
        {
            strings = StringsOverlay.Use(language, Context, Version, Strings);
            var moves = Moves?.Use();
            return moves is null ? new Both(personal, strings) : new Both(new Both(personal, strings), moves);
        }
        catch
        {
            strings?.Dispose();
            personal.Dispose();
            throw;
        }
    }

    private static void Apply(PersonalInfo p, HackSpecies s, byte generation, ref int fairy)
    {
        (p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD) = (s.BaseStats[0], s.BaseStats[1], s.BaseStats[2], s.BaseStats[3], s.BaseStats[4], s.BaseStats[5]);
        if (s.Types.Count == 2)
        {
            p.Type1 = Type(generation, s.Types[0], ref fairy);
            p.Type2 = Type(generation, s.Types[1], ref fairy);
        }
        if (s.CatchRate is int catchRate and > 0 and <= byte.MaxValue)
            p.CatchRate = (byte)catchRate;
        if (s.ExpYield is int expYield and > 0)
            p.BaseEXP = expYield;
        if (s.GenderRead && s.GenderRatio is >= 0 and <= byte.MaxValue)
            p.Gender = (byte)s.GenderRatio;
        if (s.Growth is int growth and >= 0 and <= byte.MaxValue)
            p.EXPGrowth = (byte)growth;
        if (s.Abilities.Count >= 2 && p is IPersonalAbility12 a)
        {
            a.Ability1 = Ability(generation, s.Abilities[0]);
            a.Ability2 = Ability(generation, s.Abilities[1]);
            if (p is IPersonalAbility12H h && s.Abilities.Count >= 3)
                h.AbilityH = Ability(generation, s.Abilities[2]);
        }
    }

    public static int? SpeciesIndex(byte generation, HackSpecies s) => generation <= 3 ? s.National : s.Id;

    public static int Ability(byte generation, int raw) => generation != 3 ? raw : raw switch
    {
        < IdMapper.Gen3LastAbility - 1 => raw,
        IdMapper.Gen3LastAbility => IdMapper.Gen3LastAbility - 1,
        _ => 0,
    };

    private static byte Type(byte generation, int raw, ref int fairy)
    {
        if (GameTypes.ToPkhex(generation, raw) is { } pkhex)
            return (byte)pkhex;
        fairy++;
        return PkhexFairy;
    }

    private static IReadOnlyDictionary<int, string> Renamed(IEnumerable<(int Id, string Name)> rows, IReadOnlyList<string> stock, ref int beyond)
    {
        var result = new Dictionary<int, string>();
        foreach (var (id, name) in rows)
        {
            if (id <= 0 || name.Length == 0)
                continue;
            if (id >= stock.Count)
            {
                beyond++;
                continue;
            }
            if (IdMapper.Normalise(stock[id]) == IdMapper.Normalise(name))
                continue;
            result[id] = name;
        }
        return result;
    }

    private sealed class Both(IDisposable first, IDisposable second) : IDisposable
    {
        public void Dispose()
        {
            second.Dispose();
            first.Dispose();
        }
    }
}
