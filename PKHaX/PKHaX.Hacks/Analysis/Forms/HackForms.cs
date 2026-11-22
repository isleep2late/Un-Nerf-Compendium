using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis.Forms;

public static class HackForms
{
    public const string SourcePkhexMatch = "pkhex-match";
    public const int Gen3NameLength = 10;
    public static readonly string[] BattleOnlyWords = ["Mega", "Primal", "Gmax", "Gigantamax", "Eternamax", "Zen", "School", "Blade", "Busted", "Hangry", "Totem"];

    private static readonly (IPersonalTable Table, byte Generation)[] Reference =
    [
        (PersonalTable.USUM, 7),
        (PersonalTable.SWSH, 8),
        (PersonalTable.LA, 8),
        (PersonalTable.SV, 9),
    ];

    private static readonly Lazy<Dictionary<(int National, string Key), List<(byte Form, bool BattleOnly)>>> Known = new(BuildKnown);

    public static FormCatalog Build(HackData data, string profileId, IdMapper? mapper = null)
    {
        mapper ??= IdMapper.English();
        var named = data.Species.Where(s => s.Id > 0 && s.Name.Length > 0).ToList();
        var members = new List<(HackSpecies Species, int National, bool NamedBattle)>();
        foreach (var s in named)
        {
            if (NationalFor(s, mapper) is { } national)
                members.Add((s, national, NamedBattleOnly(s.Name)));
            else if (NamedBattleOnly(s.Name) && BaseOfBattleName(s.Name, mapper) is { } baseNational)
                members.Add((s, baseNational, true));
        }
        var groups = new List<FormGroup>();
        foreach (var g in members.GroupBy(m => m.National).OrderBy(g => g.Min(m => m.Species.Id)))
        {
            var ordered = g.OrderBy(m => m.NamedBattle).ThenBy(m => m.Species.Id).ToList();
            if (ordered.Count > 1)
            {
                var rows = new List<FormRow>(ordered.Count);
                for (var i = 0; i < ordered.Count; i++)
                    rows.Add(Row(ordered[i].Species, g.Key, i, ordered[i].NamedBattle));
                groups.Add(new FormGroup(ordered[0].Species.Id, g.Key, rows));
            }
            else if (g.Key is VanillaForms.Unown or VanillaForms.Castform && VanillaForms.Build(GameVersion.E).For((ushort)g.Key) is { } vanilla)
            {
                groups.Add(new FormGroup(ordered[0].Species.Id, g.Key, [.. vanilla.Rows.Select(r => r with { Source = $"{r.Source}:gen3" })]));
            }
        }
        return new FormCatalog(profileId, groups);
    }

    public static bool NamedBattleOnly(string name) =>
        Words(name).Any(w => BattleOnlyWords.Contains(w, StringComparer.OrdinalIgnoreCase));

    public static int? NationalFor(HackSpecies s, IdMapper mapper)
    {
        if (s.National is { } national)
            return national;
        if (s.Name.Length != Gen3NameLength)
            return null;
        var key = IdMapper.Normalise(s.Name);
        var strings = GameInfo.GetStrings("en").specieslist;
        int? found = null;
        for (var i = 1; i < strings.Length; i++)
        {
            if (!IsAbbreviation(key, IdMapper.Normalise(strings[i])))
                continue;
            if (found is not null)
                return null;
            found = i;
        }
        return found;
    }

    public static bool IsAbbreviation(string key, string full)
    {
        if (key.Length == 0 || full.Length < key.Length || full[0] != key[0])
            return false;
        var j = 0;
        foreach (var c in full)
        {
            if (j < key.Length && c == key[j])
                j++;
        }
        return j == key.Length;
    }

    private static int? BaseOfBattleName(string name, IdMapper mapper)
    {
        var rest = Words(name).Where(w => !BattleOnlyWords.Contains(w, StringComparer.OrdinalIgnoreCase) && w is not ("X" or "Y")).ToList();
        return rest.Count == 0 ? null : mapper.Species(string.Join(" ", rest));
    }

    private static string[] Words(string name) => name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

    private static FormRow Row(HackSpecies s, int national, int index, bool namedBattle)
    {
        var storage = new FormStorage(FormStorageKind.Species, s.Id);
        var types = s.Types.Take(2).ToArray();
        var stats = s.BaseStats.Take(6).ToArray();
        var abilities = s.Abilities.Take(3).ToArray();
        FormRow Make(FormAvailability a, string source) => new(index, s.Name, storage, types, stats, abilities, a, source);
        if (namedBattle)
            return Make(FormAvailability.BattleOnly, FormCatalog.SourceName);
        if (index == 0)
            return Make(FormAvailability.Outside, Known.Value.TryGetValue((national, Key(stats)), out var own) ? $"{SourcePkhexMatch}:{own[0].Form}" : FormCatalog.SourceNational);
        if (!Known.Value.TryGetValue((national, Key(stats)), out var matches))
            return Make(FormAvailability.Unknown, FormCatalog.SourceUnmatched);
        if (matches.FirstOrDefault(m => m.Form != 0 && !m.BattleOnly) is { Form: > 0 } storable)
            return Make(FormAvailability.Outside, $"{SourcePkhexMatch}:{storable.Form}");
        if (matches.FirstOrDefault(m => m.BattleOnly) is { BattleOnly: true } battle)
            return Make(FormAvailability.BattleOnly, $"{SourcePkhexMatch}:{battle.Form}");
        return Make(FormAvailability.Unknown, FormCatalog.SourceAmbiguous);
    }

    private static string Key(IEnumerable<int> stats) => string.Join(",", stats);

    private static Dictionary<(int, string), List<(byte, bool)>> BuildKnown()
    {
        var map = new Dictionary<(int, string), List<(byte, bool)>>();
        foreach (var (table, generation) in Reference)
        {
            for (ushort species = 1; species <= table.MaxSpeciesID; species++)
            {
                var count = Math.Max((byte)1, table[species].FormCount);
                for (byte form = 0; form < count; form++)
                {
                    var p = table[species, form];
                    var key = ((int)species, Key([p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD]));
                    if (!map.TryGetValue(key, out var list))
                        map[key] = list = [];
                    var entry = (form, FormInfo.IsBattleOnlyForm(species, form, generation));
                    if (!list.Contains(entry))
                        list.Add(entry);
                }
            }
        }
        return map;
    }
}
