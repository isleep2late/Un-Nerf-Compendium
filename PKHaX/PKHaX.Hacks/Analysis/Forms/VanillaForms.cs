using System;
using System.Collections.Generic;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis.Forms;

public static class VanillaForms
{
    public const ushort Deoxys = (ushort)Species.Deoxys;
    public const ushort Unown = (ushort)Species.Unown;
    public const ushort Castform = (ushort)Species.Castform;

    public static byte Gen3DeoxysForm(GameVersion version) => version switch
    {
        GameVersion.E => 3,
        GameVersion.FR => 1,
        GameVersion.LG => 2,
        _ => 0,
    };

    public static FormCatalog Build(GameVersion version, string? profileId = null)
    {
        if (!version.IsValidSavedVersion())
            throw new ArgumentException($"{version} is not a single game; pass one game, such as E, FR or Pt", nameof(version));
        var table = GameData.GetPersonal(version);
        var context = version.GetContextFromSaved();
        var generation = version.Generation;
        var strings = GameInfo.GetStrings("en");
        var groups = new List<FormGroup>();
        for (ushort species = 1; species <= table.MaxSpeciesID; species++)
        {
            if (!table.IsSpeciesInGame(species))
                continue;
            if (generation == 3 && species == Deoxys)
            {
                var form = Gen3DeoxysForm(version);
                var names = FormConverter.GetFormList(species, strings.types, strings.forms, EntityContext.Gen4);
                groups.Add(new FormGroup(species, species,
                [
                    Row(table[species], form, form < names.Length ? names[form] : "Normal", new FormStorage(FormStorageKind.GameDecides, form), FormAvailability.Outside, FormCatalog.SourceGen3Deoxys),
                ]));
                continue;
            }
            var list = FormConverter.GetFormList(species, strings.types, strings.forms, context);
            var rows = new List<FormRow>(list.Length);
            for (byte form = 0; form < list.Length; form++)
            {
                if (!table.IsPresentInGame(species, form))
                    continue;
                var availability = FormInfo.IsTotemForm(species, form, context) ? FormAvailability.Unobtainable
                    : FormInfo.IsBattleOnlyForm(species, form, generation) ? FormAvailability.BattleOnly
                    : FormAvailability.Outside;
                rows.Add(Row(table[species, form], form, list[form], Storage(generation, species, form), availability, FormCatalog.SourcePkhex));
            }
            if (rows.Count > 1)
                groups.Add(new FormGroup(species, species, rows));
        }
        return new FormCatalog(profileId ?? $"vanilla:{version}", groups);
    }

    private static FormStorage Storage(byte generation, ushort species, byte form) => generation switch
    {
        <= 2 => new FormStorage(FormStorageKind.Dv, form),
        3 when species == Unown => new FormStorage(FormStorageKind.Pid, form),
        3 => new FormStorage(FormStorageKind.GameDecides, form),
        _ => new FormStorage(FormStorageKind.Field, form),
    };

    private static FormRow Row(PersonalInfo p, byte form, string name, FormStorage storage, FormAvailability availability, string source)
    {
        var abilities = new int[Math.Min(3, p.AbilityCount)];
        for (var i = 0; i < abilities.Length; i++)
            abilities[i] = p.GetAbilityAtIndex(i);
        return new FormRow(form, name, storage, [p.Type1, p.Type2], [p.HP, p.ATK, p.DEF, p.SPE, p.SPA, p.SPD], abilities, availability, source);
    }
}
