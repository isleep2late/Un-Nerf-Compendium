using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public enum NameVoteMode
{
    ByIndex,
    ByMembership,
}

public sealed record NameVoteHit(NamedLayout Layout, int Agree, int NonBlank, int Rows)
{
    public string Describe(string what) =>
        $"{what} at stride {Layout.Stride}, name {(Layout.Pointer ? "pointer" : "inline")} at +0x{Layout.NameOffset:X}, found by a vote: {Agree} of {NonBlank} named rows among the first {Rows} agree with PKHeX's own list";
}

public static class NameVote
{
    public const int MinAgree = 40;
    public const double MinShare = 0.6;
    public const int Window = 400;
    public const int GbaSpeciesWindow = 251;
    public const int QuickRows = 16;
    public const int QuickNamed = 8;

    private static readonly Lazy<GameStrings> English = new(() => GameInfo.GetStrings("en"));

    public static IReadOnlyList<string> StockSpecies => English.Value.specieslist;
    public static IReadOnlyList<string> StockMoves => English.Value.movelist;
    public static IReadOnlyList<string> StockItems => English.Value.itemlist;
    public static IReadOnlyList<string> StockAbilities => English.Value.abilitylist;

    public static HashSet<string> Keys(IEnumerable<string> names)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in names)
        {
            var key = IdMapper.Normalise(IdMapper.WithoutZSuffix(n));
            if (key.Length > 0)
                set.Add(key);
        }
        return set;
    }

    public static (int Agree, int NonBlank) Score(IReadOnlyList<string> names, IReadOnlyList<string> stock, NameVoteMode mode, int window, ISet<string>? stockKeys = null)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(stock);
        var keys = mode == NameVoteMode.ByMembership ? stockKeys ?? Keys(stock) : null;
        var agree = 0;
        var nonBlank = 0;
        for (var i = 1; i <= window && i < names.Count; i++)
        {
            var key = IdMapper.Normalise(names[i]);
            if (key.Length == 0)
                continue;
            nonBlank++;
            var hit = mode == NameVoteMode.ByIndex
                ? i < stock.Count && IdMapper.Normalise(stock[i]) == key
                : keys!.Contains(key);
            if (hit)
                agree++;
        }
        return (agree, nonBlank);
    }

    public static bool Accepted(int agree, int nonBlank, int minAgree = MinAgree) => agree >= minAgree && agree >= MinShare * nonBlank;

    private static bool Quick(GbaImage rom, int at, bool pointer, int length)
    {
        if (pointer)
        {
            if (rom.PointerAt(at) is not { } target)
                return false;
            at = target;
            length = Math.Min(FrTableFollower.PointerNameLength, rom.Length - at);
        }
        if (length <= 0 || !rom.Has(at, length))
            return false;
        var first = rom.U8(at);
        if (first is 0x00 or 0xFF || !Gen3Tables.IsNameByte(first))
            return false;
        return rom.Slice(at, length).IndexOf((byte)0xFF) > 0;
    }

    public static NameVoteHit? SearchGba(GbaImage rom, int table, IReadOnlyList<string> stock, NameVoteMode mode, int window, IReadOnlyList<int> inlineLengths, int maxStride = RhhTables.MaxStride, int maxNameOffset = 64, int minAgree = MinAgree)
    {
        ArgumentNullException.ThrowIfNull(rom);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(inlineLengths);
        var keys = mode == NameVoteMode.ByMembership ? Keys(stock) : null;
        NameVoteHit? best = null;
        for (var stride = RhhTables.MinStride; stride <= maxStride; stride++)
        {
            if (!rom.Has(table + (QuickRows * stride), 4))
                break;
            for (var offset = 0; offset <= Math.Min(maxNameOffset, stride - 1); offset++)
            {
                foreach (var length in inlineLengths)
                {
                    if (offset + length <= stride)
                        best = Try(new NamedLayout(stride, offset, false, length), best);
                }
                if (offset % 4 == 0 && offset + 4 <= stride)
                    best = Try(new NamedLayout(stride, offset, true, FrTableFollower.PointerNameLength), best);
            }
        }
        return best;

        NameVoteHit? Try(NamedLayout layout, NameVoteHit? current)
        {
            var quick = 0;
            for (var i = 1; i <= QuickRows; i++)
            {
                if (Quick(rom, table + (i * layout.Stride) + layout.NameOffset, layout.Pointer, layout.NameLength))
                    quick++;
            }
            if (quick < QuickNamed)
                return current;
            var rows = Math.Min(window + 1, Math.Max(0, ((rom.Length - table) / layout.Stride) + 1));
            var names = RhhTables.Names(rom, table, layout, rows);
            var (agree, nonBlank) = Score(names, stock, mode, window, keys);
            if (!Accepted(agree, nonBlank, minAgree) || (current is not null && agree <= current.Agree))
                return current;
            return new NameVoteHit(layout, agree, nonBlank, Math.Min(window, names.Count - 1));
        }
    }

    public static List<string> Run(GbaImage rom, int table, NamedLayout layout, int bound, int maxGap)
    {
        ArgumentNullException.ThrowIfNull(rom);
        ArgumentNullException.ThrowIfNull(layout);
        var names = RhhTables.Names(rom, table, layout, Math.Max(0, Math.Min(bound, Gen3Tables.MaxEntries)));
        var last = 0;
        var gap = 0;
        for (var i = 1; i < names.Count; i++)
        {
            if (names[i].Length == 0)
            {
                if (++gap > maxGap)
                    break;
                continue;
            }
            gap = 0;
            last = i;
        }
        return names.Count == 0 ? names : names.GetRange(0, last + 1);
    }
}
