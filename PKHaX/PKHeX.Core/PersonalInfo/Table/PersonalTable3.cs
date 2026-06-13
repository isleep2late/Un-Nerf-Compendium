using System;

namespace PKHeX.Core;

/// <summary>
/// Personal Table storing <see cref="PersonalInfo3"/> used in Generation 3 games.
/// </summary>
public sealed class PersonalTable3 : IPersonalTable, IPersonalTable<PersonalInfo3>
{
    private readonly PersonalInfo3[] Table;
    private const int SIZE = PersonalInfo3.SIZE;
    private const ushort MaxSpecies = Legal.MaxSpeciesID_3;
    public ushort MaxSpeciesID => MaxSpecies;
    public int Count => Table.Length;

    public PersonalTable3(Memory<byte> data)
    {
        Table = new PersonalInfo3[data.Length / SIZE];
        var count = data.Length / SIZE;
        for (int i = 0, ofs = 0; i < count; i++, ofs += SIZE)
        {
            var slice = data.Slice(ofs, SIZE);
            Table[i] = new PersonalInfo3(slice);
        }
    }

    public PersonalInfo3 this[int index] => Table[(uint)index < Table.Length ? index : 0];
    public PersonalInfo3 this[ushort species, byte form] => GetFormEntry(species, form);
    public PersonalInfo3 GetFormEntry(ushort species, byte form)
    {
        // PKHaX: Gen-3 personal data has only one Deoxys entry, so GetFormIndex(Deoxys, f) returns the
        // same slot for every form. Return real per-form stat entries so the editor (which reads
        // GetFormEntry(Species, Form) for base stats), sprite, and form-name all reflect the chosen form.
        if (species == (int)Species.Deoxys && form < 4)
            return (_deoxysForms ??= BuildDeoxysForms())[form];
        return Table[GetFormIndex(species, form)];
    }

    private PersonalInfo3[]? _deoxysForms;
    private PersonalInfo3[] BuildDeoxysForms()
    {
        // HP, ATK, DEF, SPE, SPA, SPD  (form order: 0 Normal, 1 Attack, 2 Defense, 3 Speed)
        ReadOnlySpan<byte> n = [50, 150, 50, 150, 150, 50];
        ReadOnlySpan<byte> a = [50, 180, 20, 150, 180, 20];
        ReadOnlySpan<byte> d = [50, 70, 160, 90, 70, 160];
        ReadOnlySpan<byte> s = [50, 95, 90, 180, 95, 90];
        var forms = new[] { n.ToArray(), a.ToArray(), d.ToArray(), s.ToArray() };
        var basePI = Table[(int)Species.Deoxys];
        var arr = new PersonalInfo3[4];
        for (int f = 0; f < 4; f++)
        {
            var clone = new PersonalInfo3(basePI.Write().AsMemory());
            var st = forms[f];
            clone.HP = st[0]; clone.ATK = st[1]; clone.DEF = st[2];
            clone.SPE = st[3]; clone.SPA = st[4]; clone.SPD = st[5];
            clone.FormCount = 4;
            arr[f] = clone;
        }
        return arr;
    }

    public int GetFormIndex(ushort species, byte form) => IsSpeciesInGame(species) ? species : 0;
    public bool IsSpeciesInGame(ushort species) => species <= MaxSpecies;
    public bool IsPresentInGame(ushort species, byte form)
    {
        if (!IsSpeciesInGame(species))
            return false;
        return form == 0 || species switch
        {
            (int)Species.Unown => form < 28,
            (int)Species.Castform => form < 4,
            (int)Species.Deoxys => form < 4,
            _ => false,
        };
    }

    PersonalInfo IPersonalTable.this[int index] => this[index];
    PersonalInfo IPersonalTable.this[ushort species, byte form] => this[species, form];
    PersonalInfo IPersonalTable.GetFormEntry(ushort species, byte form) => GetFormEntry(species, form);

    internal void LoadTables(BinLinkerAccessor machine, BinLinkerAccessor tutors)
    {
        var table = Table;
        for (int i = Legal.MaxSpeciesID_3; i >= 1; i--)
        {
            var entry = table[i];
            entry.AddTMHM(machine[i]);
            entry.AddTypeTutors(tutors[i]);
        }
    }

    internal void CopyTables(PersonalTable3 pt)
    {
        // Copy to other tables
        var other = pt.Table;
        var table = Table;
        for (int i = Legal.MaxSpeciesID_3; i >= 1; i--)
            table[i].CopyFrom(other[i]);
    }
}
