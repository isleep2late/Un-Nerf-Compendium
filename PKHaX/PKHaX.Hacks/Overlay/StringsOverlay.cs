using System;
using System.Collections.Generic;
using System.Threading;
using PKHeX.Core;

namespace PKHaX.Hacks.Overlay;

public sealed record StringsPatch
{
    private static readonly IReadOnlyDictionary<int, string> None = new Dictionary<int, string>();

    public IReadOnlyDictionary<int, string> Species { get; init; } = None;
    public IReadOnlyDictionary<int, string> Moves { get; init; } = None;
    public IReadOnlyDictionary<int, string> Items { get; init; } = None;
    public IReadOnlyDictionary<int, string> ContextItems { get; init; } = None;
    public IReadOnlyDictionary<int, string> Abilities { get; init; } = None;
    public IReadOnlyDictionary<int, string> Types { get; init; } = None;

    public bool IsEmpty => Species.Count == 0 && Moves.Count == 0 && Items.Count == 0 && ContextItems.Count == 0 && Abilities.Count == 0 && Types.Count == 0;
}

public sealed class StringsOverlay : IDisposable
{
    private static StringsOverlay? active;

    private GameStrings previous;
    private int disposed;

    public GameStrings Strings { get; }

    private StringsOverlay(GameStrings previous, GameStrings strings)
    {
        this.previous = previous;
        Strings = strings;
    }

    public static bool IsActive => Volatile.Read(ref active) is not null;

    public static bool Reassert()
    {
        if (Volatile.Read(ref active) is not { } overlay || ReferenceEquals(GameInfo.Strings, overlay.Strings))
            return false;
        overlay.previous = GameInfo.Strings;
        GameInfo.Strings = overlay.Strings;
        return true;
    }

    public static GameStrings Build(string language, EntityContext context, GameVersion version, StringsPatch patch)
    {
        var strings = GameStrings.CreateDetached(language);
        Apply(strings.specieslist, patch.Species, "species");
        Apply(strings.movelist, patch.Moves, "move");
        Apply(strings.abilitylist, patch.Abilities, "ability");
        Apply(strings.types, patch.Types, "type");
        if (patch.Items.Count != 0)
        {
            Apply(strings.itemlist, patch.Items, "item");
            Apply(strings.GetItemListForEdit(EntityContext.Gen4), patch.Items, "item");
        }
        if (patch.ContextItems.Count != 0)
        {
            if (!HasOwnItemIndex(context))
                throw new ArgumentException($"ContextItems holds Gen 1-3 item indexes; {context} items use national ids, so they go in Items.", nameof(patch));
            Apply(strings.GetItemListForEdit(context, version), patch.ContextItems, "item");
        }
        return strings;
    }

    public static bool HasOwnItemIndex(EntityContext context) => context is EntityContext.Gen1 or EntityContext.Gen2 or EntityContext.Gen3;

    public static StringsOverlay Use(string language, EntityContext context, GameVersion version, StringsPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var strings = Build(language, context, version, patch);
        var overlay = new StringsOverlay(GameInfo.Strings, strings);
        if (Interlocked.CompareExchange(ref active, overlay, null) is not null)
            throw new InvalidOperationException("A strings overlay is already active; dispose it first.");
        GameInfo.Strings = strings;
        return overlay;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;
        if (ReferenceEquals(GameInfo.Strings, Strings))
            GameInfo.Strings = previous;
        Interlocked.CompareExchange(ref active, null, this);
    }

    private static void Apply(string[] target, IReadOnlyDictionary<int, string> names, string what)
    {
        foreach (var (index, name) in names)
        {
            if ((uint)index >= (uint)target.Length)
                throw new ArgumentOutOfRangeException(nameof(names), index, $"The {what} list has {target.Length} entries; the overlay keeps the list's own index and cannot grow it.");
            target[index] = name;
        }
    }
}
