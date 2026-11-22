using System;
using System.Collections.Generic;

namespace PKHaX.Hacks.Analysis;

public sealed record GbLearnsetRun(int Offset, int Count, IReadOnlyList<IReadOnlyList<HackLearn>?> Lists, int Unread, bool DataFollows, int Candidates)
{
    public int Read => Count - Unread;
}

public static class GbLearnsets
{
    public const int BankSize = 0x4000;
    public const double MinParsedShare = 0.9;
    public const double MinDistinctShare = 0.5;
    public const int MaxEvolutions = 8;
    public const int MaxLearns = 64;
    public const int MaxLevel = 100;

    public const byte EvolveLevel = 1;
    public const byte EvolveItem = 2;
    public const byte EvolveTrade = 3;
    public const byte EvolveHappiness = 4;
    public const byte EvolveStat = 5;

    public static int? Target(int tableOffset, int pointer, int romLength)
    {
        var bank = tableOffset / BankSize;
        if (bank == 0 || pointer < BankSize || pointer >= 2 * BankSize)
            return null;
        var at = (bank * BankSize) + pointer - BankSize;
        return at < romLength ? at : null;
    }

    public static int Pointer(ReadOnlySpan<byte> rom, int at) => rom[at] | (rom[at + 1] << 8);

    public static IReadOnlyList<HackLearn>? Parse(ReadOnlySpan<byte> rom, int at, int gen, int maxMove)
    {
        var a = at;
        var evolutions = 0;
        while (true)
        {
            if (a >= rom.Length)
                return null;
            var method = rom[a];
            if (method == 0)
            {
                a++;
                break;
            }
            if (++evolutions > MaxEvolutions)
                return null;
            var size = EvolutionSize(rom, a, gen);
            if (size <= 0)
                return null;
            a += size;
        }
        var learns = new List<HackLearn>();
        while (true)
        {
            if (a >= rom.Length)
                return null;
            var level = rom[a];
            if (level == 0)
                return learns;
            if (a + 1 >= rom.Length || level > MaxLevel || learns.Count >= MaxLearns)
                return null;
            var move = rom[a + 1];
            if (move == 0 || move > maxMove)
                return null;
            learns.Add(new HackLearn(level, move));
            a += 2;
        }
    }

    private static bool Level(byte b) => b is >= 1 and <= MaxLevel;

    private static int EvolutionSize(ReadOnlySpan<byte> rom, int a, int gen)
    {
        var room = rom.Length - a;
        if (room < 4)
            return -1;
        var method = rom[a];
        var b1 = rom[a + 1];
        var b2 = rom[a + 2];
        var b3 = rom[a + 3];
        if (gen == 1)
        {
            return method switch
            {
                EvolveLevel when Level(b1) && b2 != 0 => 3,
                EvolveItem when b1 != 0 && Level(b2) && b3 != 0 => 4,
                EvolveTrade when Level(b1) && b2 != 0 => 3,
                _ => -1,
            };
        }
        return method switch
        {
            EvolveLevel when Level(b1) && b2 != 0 => 3,
            EvolveItem when b1 != 0 && b2 != 0 => 3,
            EvolveTrade when b2 != 0 => 3,
            EvolveHappiness when b1 is >= 1 and <= 3 && b2 != 0 => 3,
            EvolveStat when Level(b1) && b2 is >= 1 and <= 3 && b3 != 0 => 4,
            _ => -1,
        };
    }

    public static GbLearnsetRun? Find(ReadOnlySpan<byte> rom, int gen, int slots, int maxMove, out string why, int? at = null)
    {
        why = "";
        if (slots < GbStructure.MinBaseRows)
        {
            why = $"only {slots} species slots, fewer than the {GbStructure.MinBaseRows} a pointer run must cover";
            return null;
        }
        GbLearnsetRun? best = null;
        var candidates = 0;
        var from = at ?? 0;
        var to = at ?? rom.Length - (2 * slots);
        for (var p = from; p <= to && p >= 0 && p + (2 * slots) <= rom.Length; p++)
        {
            var ok = true;
            for (var i = 0; i < slots; i++)
            {
                if (Target(p, Pointer(rom, p + (2 * i)), rom.Length) is null)
                {
                    ok = false;
                    break;
                }
            }
            if (!ok)
                continue;
            var distinct = new HashSet<int>();
            for (var i = 0; i < slots; i++)
                distinct.Add(Pointer(rom, p + (2 * i)));
            if (distinct.Count < MinDistinctShare * slots)
                continue;
            var lists = new IReadOnlyList<HackLearn>?[slots];
            var unread = 0;
            for (var i = 0; i < slots && unread <= (1 - MinParsedShare) * slots; i++)
            {
                lists[i] = Parse(rom, Target(p, Pointer(rom, p + (2 * i)), rom.Length)!.Value, gen, maxMove);
                if (lists[i] is null)
                    unread++;
            }
            if (unread > (1 - MinParsedShare) * slots)
                continue;
            candidates++;
            var follows = Target(p, Pointer(rom, p), rom.Length) == p + (2 * slots);
            var run = new GbLearnsetRun(p, slots, lists, unread, follows, 0);
            if (best is null || run.Unread < best.Unread || (run.Unread == best.Unread && run.DataFollows && !best.DataFollows))
                best = run;
        }
        if (best is null)
        {
            why = $"no run of {slots} two-byte pointers into one bank has at least {MinParsedShare * 100:0}% of its targets parse as an evolution list ended by 0 followed by (level 1-{MaxLevel}, move 1-{maxMove}) pairs ended by 0";
            return null;
        }
        return best with { Candidates = candidates };
    }
}
