using System;
using System.Threading;

namespace PKHeX.Core;

public sealed record PersonalOverrideSet
{
    public static PersonalOverrideSet Empty { get; } = new();

    public PersonalTable9ZA? ZA { get; init; }
    public PersonalTable9SV? SV { get; init; }
    public PersonalTable8LA? LA { get; init; }
    public PersonalTable8BDSP? BDSP { get; init; }
    public PersonalTable8SWSH? SWSH { get; init; }
    public PersonalTable7GG? GG { get; init; }
    public PersonalTable7? USUM { get; init; }
    public PersonalTable7? SM { get; init; }
    public PersonalTable6AO? AO { get; init; }
    public PersonalTable6XY? XY { get; init; }
    public PersonalTable5B2W2? B2W2 { get; init; }
    public PersonalTable5BW? BW { get; init; }
    public PersonalTable4? HGSS { get; init; }
    public PersonalTable4? Pt { get; init; }
    public PersonalTable4? DP { get; init; }
    public PersonalTable3? LG { get; init; }
    public PersonalTable3? FR { get; init; }
    public PersonalTable3? E { get; init; }
    public PersonalTable3? RS { get; init; }
    public PersonalTable2? C { get; init; }
    public PersonalTable2? GS { get; init; }
    public PersonalTable1? RB { get; init; }
    public PersonalTable1? Y { get; init; }

    public PersonalOverrideSet With(EntityContext context, IPersonalTable table) => (context, table) switch
    {
        (EntityContext.Gen1, PersonalTable1 t) => this with { RB = t, Y = t },
        (EntityContext.Gen2, PersonalTable2 t) => this with { GS = t, C = t },
        (EntityContext.Gen3, PersonalTable3 t) => this with { RS = t, E = t, FR = t, LG = t },
        (EntityContext.Gen4, PersonalTable4 t) => this with { DP = t, Pt = t, HGSS = t },
        (EntityContext.Gen5, PersonalTable5BW t) => this with { BW = t },
        (EntityContext.Gen5, PersonalTable5B2W2 t) => this with { B2W2 = t },
        (EntityContext.Gen6, PersonalTable6XY t) => this with { XY = t },
        (EntityContext.Gen6, PersonalTable6AO t) => this with { AO = t },
        (EntityContext.Gen7, PersonalTable7 t) => this with { SM = t, USUM = t },
        (EntityContext.Gen7b, PersonalTable7GG t) => this with { GG = t },
        (EntityContext.Gen8, PersonalTable8SWSH t) => this with { SWSH = t },
        (EntityContext.Gen8a, PersonalTable8LA t) => this with { LA = t },
        (EntityContext.Gen8b, PersonalTable8BDSP t) => this with { BDSP = t },
        (EntityContext.Gen9, PersonalTable9SV t) => this with { SV = t },
        (EntityContext.Gen9a, PersonalTable9ZA t) => this with { ZA = t },
        _ => throw new ArgumentException($"Context {context} does not take a {table.GetType().Name}.", nameof(table)),
    };
}

public static class PersonalOverride
{
    private static readonly Lock gate = new();
    private static Entry[] entries = [];
    private static PersonalOverrideSet? current;

    public static PersonalOverrideSet? Current => Volatile.Read(ref current);

    public static bool IsActive => Current is not null;

    public static int ScopeCount
    {
        get
        {
            lock (gate)
                return entries.Length;
        }
    }

    public static IDisposable Use(EntityContext context, IPersonalTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        return Push(new Entry(null, context, table));
    }

    public static IDisposable Use(PersonalOverrideSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return Push(new Entry(set, default, null));
    }

    private static Scope Push(Entry entry)
    {
        lock (gate)
        {
            Entry[] next = [.. entries, entry];
            var set = Compose(next);
            entries = next;
            Volatile.Write(ref current, set);
        }
        return new Scope(entry);
    }

    private static void Remove(Entry entry)
    {
        lock (gate)
        {
            var index = Array.IndexOf(entries, entry);
            if (index < 0)
                return;
            Entry[] next = [.. entries.AsSpan(0, index), .. entries.AsSpan(index + 1)];
            entries = next;
            Volatile.Write(ref current, Compose(next));
        }
    }

    private static PersonalOverrideSet? Compose(Entry[] list)
    {
        PersonalOverrideSet? set = null;
        foreach (var entry in list)
            set = entry.Set ?? (set ?? PersonalOverrideSet.Empty).With(entry.Context, entry.Table!);
        return set;
    }

    private sealed class Entry(PersonalOverrideSet? set, EntityContext context, IPersonalTable? table)
    {
        public PersonalOverrideSet? Set { get; } = set;
        public EntityContext Context { get; } = context;
        public IPersonalTable? Table { get; } = table;
    }

    private sealed class Scope(Entry entry) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            Remove(entry);
        }
    }
}
