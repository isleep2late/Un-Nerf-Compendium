using System;
using System.Collections.Generic;
using System.Threading;

namespace PKHeX.Core;

public sealed record MoveOverrideLayer(EntityContext Context, ReadOnlyMemory<byte> Types, ReadOnlyMemory<byte> PP)
{
    public const byte NoType = byte.MaxValue;
    public const byte NoPP = 0;

    public bool TryGetType(ushort move, out byte type)
    {
        var span = Types.Span;
        type = move < span.Length ? span[move] : NoType;
        return type != NoType;
    }

    public bool TryGetPP(ushort move, out byte pp)
    {
        var span = PP.Span;
        pp = move < span.Length ? span[move] : NoPP;
        return pp != NoPP;
    }
}

public sealed class MoveOverrideSet
{
    private readonly MoveOverrideLayer[] layers;

    internal MoveOverrideSet(MoveOverrideLayer[] layers) => this.layers = layers;

    public IReadOnlyList<MoveOverrideLayer> Layers => layers;

    public bool TryGetType(EntityContext context, ushort move, out byte type)
    {
        for (var i = layers.Length - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (layer.Context == context && layer.TryGetType(move, out type))
                return true;
        }
        type = MoveOverrideLayer.NoType;
        return false;
    }

    public bool TryGetPP(EntityContext context, ushort move, out byte pp)
    {
        for (var i = layers.Length - 1; i >= 0; i--)
        {
            var layer = layers[i];
            if (layer.Context == context && layer.TryGetPP(move, out pp))
                return true;
        }
        pp = MoveOverrideLayer.NoPP;
        return false;
    }
}

public static class MoveOverride
{
    private static readonly Lock gate = new();
    private static MoveOverrideLayer[] entries = [];
    private static MoveOverrideSet? current;

    public static MoveOverrideSet? Current => Volatile.Read(ref current);

    public static bool IsActive => Current is not null;

    public static int ScopeCount
    {
        get
        {
            lock (gate)
                return entries.Length;
        }
    }

    public static IDisposable Use(EntityContext context, ReadOnlyMemory<byte> types, ReadOnlyMemory<byte> pp) => Use(new MoveOverrideLayer(context, types, pp));

    public static IDisposable Use(MoveOverrideLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        lock (gate)
        {
            MoveOverrideLayer[] next = [.. entries, layer];
            entries = next;
            Volatile.Write(ref current, new MoveOverrideSet(next));
        }
        return new Scope(layer);
    }

    private static void Remove(MoveOverrideLayer layer)
    {
        lock (gate)
        {
            var index = Array.FindIndex(entries, e => ReferenceEquals(e, layer));
            if (index < 0)
                return;
            MoveOverrideLayer[] next = [.. entries.AsSpan(0, index), .. entries.AsSpan(index + 1)];
            entries = next;
            Volatile.Write(ref current, next.Length == 0 ? null : new MoveOverrideSet(next));
        }
    }

    private sealed class Scope(MoveOverrideLayer layer) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;
            Remove(layer);
        }
    }
}
