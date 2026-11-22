using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Analysis;

public sealed record HackSpecies(int Id, string Name, int? National, int Form, IReadOnlyList<int> Types, IReadOnlyList<int> BaseStats, IReadOnlyList<int> Abilities,
    int? Growth, int GenderRatio, int? CatchRate, int? ExpYield)
{
    private readonly bool? genderRead;

    public int? Dex { get; init; }
    public int? BaseSpecies { get; init; }
    public string? FormName { get; init; }

    public bool GenderRead
    {
        get => genderRead ?? Abilities.Count >= 2;
        init => genderRead = value;
    }
}

public sealed record HackMove(int Id, string Name, int? National, int Power, int Type, int Accuracy, int Pp)
{
    public const int Physical = 0;
    public const int Special = 1;
    public const int StatusCategory = 2;

    public int? Category { get; init; }
    public bool CategoryByType { get; init; }
}

public sealed record HackItem(int Id, string Name, int? National, int Pocket, int Price);

public sealed record HackAbility(int Id, string Name, int? National);

public sealed record HackLearn(int Level, int Move);

public sealed record HackDataCounts(int Species, int Moves, int Items, int Abilities, int Forms = 0, int LevelUp = 0);

public sealed record HackTableStatus(string Table, string Status, string? Reason = null, bool? Aligned = null)
{
    public const string Read = "read";
    public const string Partial = "partial";
    public const string Missing = "missing";

    public const string Species = "species";
    public const string Moves = "moves";
    public const string Items = "items";
    public const string Abilities = "abilities";
    public const string LevelUp = "levelUp";

    public static IReadOnlyList<string> Kinds { get; } = [Species, Moves, Items, Abilities, LevelUp];

    public static IReadOnlyList<string> Statuses { get; } = [Read, Partial, Missing];

    public bool IsRead => Status == Read;

    public static HackTableStatus Of(string table, bool present, string? missingReason = null) =>
        present ? new HackTableStatus(table, Read) : new HackTableStatus(table, Missing, missingReason);
}

public sealed record HackData
{
    public const string Schema = "hackdata/1";
    public const int CurrentReaderVersion = 6;
    public const int MinReaderVersion = 4;
    public const int FirstStatusVersion = 5;

    public required string RomSha1 { get; init; }
    public required string Reader { get; init; }
    public int ReaderVersion { get; init; } = CurrentReaderVersion;
    public string? RhhVersion { get; init; }
    public string? Platform { get; init; }
    public string? GameCode { get; init; }
    public string? Engine { get; init; }
    public string? TableKey { get; init; }
    public IReadOnlyList<HackSpecies> Species { get; init; } = [];
    public IReadOnlyList<HackSpecies> Forms { get; init; } = [];
    public IReadOnlyList<HackMove> Moves { get; init; } = [];
    public IReadOnlyList<HackItem> Items { get; init; } = [];
    public IReadOnlyList<HackAbility> Abilities { get; init; } = [];
    public IReadOnlyDictionary<int, IReadOnlyList<HackLearn>> LevelUp { get; init; } = new Dictionary<int, IReadOnlyList<HackLearn>>();
    public IReadOnlyList<HackTableStatus> Tables { get; init; } = [];
    public IReadOnlyList<string> TypeNames { get; init; } = [];
    public IReadOnlyList<string> Notes { get; init; } = [];

    public HackDataCounts Counts => new(Species.Count, Moves.Count, Items.Count, Abilities.Count, Forms.Count, LevelUp.Count);

    public HackTableStatus? Table(string table) => Tables.FirstOrDefault(t => t.Table == table);

    public string StatusOf(string table) => Table(table)?.Status ?? HackTableStatus.Read;

    public bool SpeciesOverlayable => Table(HackTableStatus.Species) is not { } s || (s.IsRead && s.Aligned != false);

    public IReadOnlyList<int> UnmappedSpecies => Unmapped(Species, s => s.Id, s => s.National);
    public IReadOnlyList<int> UnmappedMoves => Unmapped(Moves, m => m.Id, m => m.National);
    public IReadOnlyList<int> UnmappedItems => Unmapped(Items, i => i.Id, i => i.National);
    public IReadOnlyList<int> UnmappedAbilities => Unmapped(Abilities, a => a.Id, a => a.National);

    private static IReadOnlyList<int> Unmapped<T>(IReadOnlyList<T> list, System.Func<T, int> id, System.Func<T, int?> national)
    {
        var result = new List<int>();
        foreach (var e in list)
        {
            if (id(e) != 0 && national(e) is null)
                result.Add(id(e));
        }
        return result;
    }
}

public sealed record HackDataResult(HackData? Data, string? Refusal)
{
    public bool Ok => Data is not null;

    public static HackDataResult Refused(string why) => new(null, why);
}
