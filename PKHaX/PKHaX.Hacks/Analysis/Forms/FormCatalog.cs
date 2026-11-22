using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Analysis.Forms;

public enum FormAvailability
{
    Outside,
    BattleOnly,
    EventLocked,
    Unobtainable,
    Unknown,
}

public enum FormStorageKind
{
    GameDecides,
    Field,
    Species,
    Pid,
    Dv,
    Pk3Byte0x1F,
}

public sealed record FormStorage(FormStorageKind Kind, int Value);

public sealed record FormRow(int Form, string Name, FormStorage Storage, int[] Types, int[] BaseStats, int[] Abilities, FormAvailability Availability, string Source);

public sealed record FormGroup(int BaseSpecies, int? National, IReadOnlyList<FormRow> Rows)
{
    public IEnumerable<FormRow> Storable => Rows.Where(r => r.Availability is FormAvailability.Outside or FormAvailability.EventLocked);
}

public sealed record FormCatalog(string ProfileId, IReadOnlyList<FormGroup> Groups)
{
    public const string SourcePkhex = "pkhex";
    public const string SourceGen3Deoxys = "gen3-deoxys";
    public const string SourceNational = "national-id";
    public const string SourceName = "name";
    public const string SourceAmbiguous = "ambiguous";
    public const string SourceUnmatched = "unmatched";

    public FormGroup? For(int baseSpecies) => Groups.FirstOrDefault(g => g.BaseSpecies == baseSpecies);

    public FormGroup? GroupOf(int species) =>
        For(species) ?? Groups.FirstOrDefault(g => g.Rows.Any(r => r.Storage.Kind == FormStorageKind.Species && r.Storage.Value == species));

    public int RowCount => Groups.Sum(g => g.Rows.Count);

    public int BattleOnlyCount => Groups.Sum(g => g.Rows.Count(r => r.Availability == FormAvailability.BattleOnly));
}
