using System;
using PKHeX.Core;

namespace PKHeX.WinForms.Controls;

public partial class PKMEditor
{
    private void PopulateFieldsPK3()
    {
        if (Entity is not G3PKM pk3)
            throw new FormatException(nameof(Entity));

        LoadMisc3(pk3);
        LoadMisc1(pk3);
        LoadMisc2(pk3);

        if (pk3 is PK3 p3a)
        {
            var fullA = new System.Collections.Generic.List<ComboItem>(GameInfo.FilteredSources.Abilities);
            CB_Ability.DataSource = fullA;
            int eff = p3a.AbilityOverride >= 2 ? p3a.AbilityOverride : pk3.Ability;
            int idxA = fullA.FindIndex(z => z.Value == eff);
            CB_Ability.SelectedIndex = idxA >= 0 ? idxA : 0;
        }
        else
        {
            CB_Ability.SelectedIndex = pk3.AbilityBit && CB_Ability.Items.Count > 1 ? 1 : 0;
        }
        if (pk3 is IShadowCapture s)
            LoadShadow3(s);

        LoadPartyStats(pk3);
        UpdateStats();
    }

    private G3PKM PreparePK3()
    {
        if (Entity is not G3PKM pk3)
            throw new FormatException(nameof(Entity));

        SaveMisc3(pk3); // save Language first so that Nickname/etc encode properly
        SaveMisc2(pk3); // save IsEgg prior to setting ^
        SaveMisc1(pk3);

        if (pk3 is PK3 p3b)
        {
            int sel = (CB_Ability.SelectedItem as ComboItem)?.Value ?? pk3.Ability;
            var pi3 = pk3.PersonalInfo;
            int slot0 = pi3.GetAbilityAtIndex(0);
            int slot1 = pi3.AbilityCount > 1 ? pi3.GetAbilityAtIndex(1) : slot0;
            if (sel == slot0) { p3b.AbilityBit = false; p3b.AbilityOverride = 0; }
            else if (sel == slot1) { p3b.AbilityBit = true; p3b.AbilityOverride = 0; }
            else { p3b.AbilityBit = false; p3b.AbilityOverride = sel; }
        }
        else
        {
            pk3.AbilityBit = CB_Ability.SelectedIndex != 0;
        }
        if (Entity is IShadowCapture ck3)
            SaveShadow3(ck3);

        SavePartyStats(pk3);
        pk3.FixMoves();
        pk3.RefreshChecksum();
        return pk3;
    }
}
