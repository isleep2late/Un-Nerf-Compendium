using System;
using System.Collections.Generic;
using System.Linq;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public sealed record StatStride(int Stride, int Plausible, int Window);

public sealed record ItemNameSlots(int Slot0, int Stride, bool InHeaderEntry, int Named);

public sealed record SpeciesFieldOffsets(int? Type1, int? Type2, int? CatchRate, int? ExpYield, int? Gender, int? Growth, int Compared, IReadOnlyDictionary<byte, byte>? TypeMap = null, string Scores = "", int BestShift = 0);

public static class Gen3LayoutFinder
{
    public const int MinStride = 26;
    public const int MaxStride = 300;
    public const int Window = 400;
    public const double MinPlausibleShare = 0.95;
    public const double MinFieldShare = 0.6;
    public const int MinCompared = 100;
    private const int LastGen3National = 386;

    public static bool PlausibleStats(ReadOnlySpan<byte> six)
    {
        var sum = 0;
        foreach (var b in six)
        {
            if (b == 0)
                return false;
            sum += b;
        }
        return sum is >= 175 and <= 780;
    }

    public const int MinNonBlank = 300;

    public static StatStride? FindStatStride(GbaImage rom, int table)
    {
        for (var stride = MinStride; stride <= MaxStride; stride++)
        {
            if (!rom.Has(table + (Window * stride), 6))
                continue;
            var plausible = 0;
            var blank = 0;
            for (var i = 1; i <= Window; i++)
            {
                var six = rom.Slice(table + (i * stride), 6);
                if (!six.ContainsAnyExcept((byte)0))
                    blank++;
                else if (PlausibleStats(six))
                    plausible++;
            }
            var filled = Window - blank;
            if (filled >= MinNonBlank && plausible >= MinPlausibleShare * filled)
                return new StatStride(stride, plausible, filled);
        }
        return null;
    }

    public const int MaxItemPointerStride = 256;

    public static ItemNameSlots? FindItemNamePointers(GbaImage rom, int? headerItems)
    {
        var strings = new HashSet<int>();
        foreach (var casing in new[] { "MASTER BALL", "Master Ball" })
        {
            foreach (var hit in FrTableFollower.Hits(rom, FrTableFollower.Encode(casing)))
            {
                if (hit == 0 || rom.U8(hit - 1) is 0xFF or 0x00)
                    strings.Add(hit);
            }
        }
        if (strings.Count == 0)
            return null;
        byte[][] ultra = [FrTableFollower.Encode("ULTRA BALL"), FrTableFollower.Encode("Ultra Ball")];
        byte[][] great = [FrTableFollower.Encode("GREAT BALL"), FrTableFollower.Encode("Great Ball")];
        bool PointsAt(int slot, byte[][] names) => rom.PointerAt(slot) is { } at && names.Any(n => rom.Has(at, n.Length) && rom.Slice(at, n.Length).SequenceEqual(n));
        var found = new List<ItemNameSlots>();
        var candidates = 0;
        for (var slot = 0; slot + 4 <= rom.Length && candidates < FrTableFollower.MaxHits; slot += 4)
        {
            if (rom.PointerAt(slot) is not { } at || !strings.Contains(at))
                continue;
            candidates++;
            for (var stride = 4; stride <= MaxItemPointerStride; stride += 4)
            {
                if (slot - stride < 0 || !PointsAt(slot + stride, ultra) || !PointsAt(slot + (2 * stride), great))
                    continue;
                var slot0 = slot - stride;
                found.Add(new ItemNameSlots(slot0, stride, headerItems is { } h && slot0 >= h && slot0 < h + stride, NamedRun(rom, slot0, stride, int.MaxValue).Skip(1).Count(n => n.Length > 0)));
                break;
            }
        }
        return found.OrderByDescending(f => f.InHeaderEntry).ThenByDescending(f => f.Named).ThenBy(f => f.Stride).ThenBy(f => f.Slot0).FirstOrDefault();
    }

    public const int MaxUnnamedRun = 8;
    public const int MinNamedItems = 300;

    public static List<string> NamedRun(GbaImage rom, int slot0, int stride, int bound)
    {
        var names = new List<string>();
        var lastNamed = 0;
        for (var i = 0; i < Math.Min(bound, Gen3Tables.MaxEntries) && rom.Has(slot0 + (i * stride), 4) && i - lastNamed <= MaxUnnamedRun; i++)
        {
            var name = FrTableFollower.PointerName(rom, slot0 + (i * stride));
            names.Add(name ?? "");
            if (i > 0 && !string.IsNullOrEmpty(name))
                lastNamed = i;
        }
        return names.Take(lastNamed + 1).ToList();
    }

    public static bool IsNamePointerTable(GbaImage rom, int table, IReadOnlyList<string> anchor)
    {
        for (var i = 0; i < anchor.Count; i++)
        {
            if (!FrTableFollower.PointerNameIs(rom, table + ((i + 1) * 4), anchor[i]))
                return false;
        }
        return true;
    }

    public static byte RawType(byte pkhexType) => pkhexType >= 9 ? (byte)(pkhexType + 1) : pkhexType;

    public static SpeciesFieldOffsets FindFields(GbaImage rom, int table, int stride, IReadOnlyList<int?> national)
    {
        var rows = new List<(int Entry, PersonalInfo3 Info)>();
        for (var i = 1; i < national.Count; i++)
        {
            if (national[i] is { } n && n is >= 1 and <= LastGen3National && rom.Has(table + (i * stride), stride))
                rows.Add((i, PersonalTable.StockE[n]));
        }
        if (rows.Count < MinCompared)
            return new SpeciesFieldOffsets(null, null, null, null, null, null, rows.Count, Scores: $"only {rows.Count} species map to a Gen 3 national id, {MinCompared} needed");
        var scores = new List<string>();
        byte At(int entry, int o) => rom.U8(table + (entry * stride) + o);
        (int Offset, int Count) Top(Func<PersonalInfo3, byte> expected, ISet<int> taken)
        {
            var bestOffset = -1;
            var bestCount = 0;
            for (var o = 6; o < stride; o++)
            {
                if (taken.Contains(o))
                    continue;
                var count = rows.Count(r => At(r.Entry, o) == expected(r.Info));
                if (count > bestCount)
                {
                    bestCount = count;
                    bestOffset = o;
                }
            }
            return (bestOffset, bestCount);
        }
        int? Best(string field, Func<PersonalInfo3, byte> expected, ISet<int> taken)
        {
            var (o, count) = Top(expected, taken);
            scores.Add(o < 0 ? $"{field} none" : $"{field} +0x{o:X} {count}/{rows.Count}");
            if (o < 0 || count < MinFieldShare * rows.Count)
                return null;
            taken.Add(o);
            return o;
        }
        (int Offset, int Count, Dictionary<byte, byte> Map) LearnTypes(Func<PersonalInfo3, byte> pkhexType, ISet<int> taken)
        {
            var best = (Offset: -1, Count: 0, Map: new Dictionary<byte, byte>());
            for (var o = 6; o < stride; o++)
            {
                if (taken.Contains(o))
                    continue;
                var map = new Dictionary<byte, byte>();
                var count = 0;
                var oneToOne = true;
                foreach (var group in rows.GroupBy(r => pkhexType(r.Info)))
                {
                    var mode = group.GroupBy(r => At(r.Entry, o)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
                    if (!map.TryAdd(mode.Key, RawType(group.Key)))
                    {
                        oneToOne = false;
                        break;
                    }
                    count += mode.Count();
                }
                if (oneToOne && count > best.Count)
                    best = (o, count, map);
            }
            return best;
        }
        string MapNote(Dictionary<byte, byte> map) => map.All(kv => kv.Key == kv.Value) ? "Emerald's ids" : $"renumbered, {map.Count(kv => kv.Key != kv.Value)} of {map.Count} ids differ";
        var used = new HashSet<int>();
        var t1 = LearnTypes(p => p.Type1, used);
        scores.Add(t1.Offset < 0 ? "type 1 none" : $"type 1 +0x{t1.Offset:X} {t1.Count}/{rows.Count} ({MapNote(t1.Map)})");
        int? type1 = t1.Offset >= 0 && t1.Count >= MinFieldShare * rows.Count ? t1.Offset : null;
        if (type1 is { } a)
            used.Add(a);
        var t2 = LearnTypes(p => p.Type2, used);
        scores.Add(t2.Offset < 0 ? "type 2 none" : $"type 2 +0x{t2.Offset:X} {t2.Count}/{rows.Count} ({MapNote(t2.Map)})");
        int? type2 = type1 is not null && t2.Offset >= 0 && t2.Count >= MinFieldShare * rows.Count ? t2.Offset : null;
        if (type2 is { } b)
            used.Add(b);
        var typeMap = new Dictionary<byte, byte>(t1.Map);
        foreach (var kv in t2.Map)
            typeMap.TryAdd(kv.Key, kv.Value);
        var catchRate = Best("catch rate", p => p.CatchRate, used);
        var expYield = Best("EXP yield", p => (byte)p.BaseEXP, used);
        var gender = Best("gender", p => p.Gender, used);
        var growth = Best("growth", p => p.EXPGrowth, used);
        var (alignment, bestShift) = Alignment(rom, table, stride, rows);
        scores.Add(alignment);
        return new SpeciesFieldOffsets(type1, type2, catchRate, expYield, gender, growth, rows.Count, type1 is null ? null : typeMap, string.Join(", ", scores), bestShift);
    }

    public const int MaxEntryShift = 30;

    public const int MinShiftExact = 10;

    private static (string Text, int BestShift) Alignment(GbaImage rom, int table, int stride, List<(int Entry, PersonalInfo3 Info)> rows)
    {
        static bool Exact(ReadOnlySpan<byte> s, PersonalInfo3 p) => s[0] == p.HP && s[1] == p.ATK && s[2] == p.DEF && s[3] == p.SPE && s[4] == p.SPA && s[5] == p.SPD;
        int ExactAt(int step, int add, int shift) => rows.Count(r => r.Entry + shift >= 0 && rom.Has(table + ((r.Entry + shift) * step) + add, 6) && Exact(rom.Slice(table + ((r.Entry + shift) * step) + add, 6), r.Info));
        var near = rows.Count(r =>
        {
            var s = rom.Slice(table + (r.Entry * stride), 6);
            var total = s[0] + s[1] + s[2] + s[3] + s[4] + s[5];
            return Math.Abs(total - r.Info.BST) <= 0.15 * r.Info.BST;
        });
        var shifts = Enumerable.Range(-MaxEntryShift, (2 * MaxEntryShift) + 1).Select(k => (K: k, Count: ExactAt(stride, 0, k))).OrderByDescending(x => x.Count).ThenBy(x => Math.Abs(x.K)).First();
        return ($"base stats exactly Emerald's {ExactAt(stride, 0, 0)}/{rows.Count}, total within 15% {near}/{rows.Count}; at stride {2 * stride}: +0 {ExactAt(2 * stride, 0, 0)}, +0x{stride:X} {ExactAt(2 * stride, stride, 0)}; best entry shift {shifts.K:+0;-0;0} with {shifts.Count} exact", shifts.Count >= Math.Max(MinShiftExact, 2 * ExactAt(stride, 0, 0)) ? shifts.K : 0);
    }
}
