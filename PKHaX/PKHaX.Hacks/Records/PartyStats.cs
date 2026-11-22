using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Saves;
using PKHeX.Core;

namespace PKHaX.Hacks.Records;

public sealed record PartyStatsRow(int Species, string Name, StatSet Base, byte Growth);

public sealed record PartyStatsVerdict(bool Agree, string? Sentence, IReadOnlyList<string> Checked)
{
    public override string ToString() => Agree ? $"party stats agree ({string.Join("; ", Checked)})" : Sentence ?? "party stats do not agree";
}

public static class PartyStats
{
    public const int GrowthCurves = 6;
    public const string NoTable = "the hack's species table was not read from its ROM, so a party Pokemon's species, EXP and level stay locked";
    public const string NoParty = "this save has no party Pokemon to check the stat formula against, so level and species stay locked";
    public const string NoIvs = "this record holds no IVs or nature to compute its stats from, so its species, EXP and level stay locked";
    public const int BoostedLimit = 595;
    public const int LoweredLimit = 728;
    public const string NatureRange = "this stat is past the range where the game's nature arithmetic is known, so the level and species edit is not written";

    public static string NotFollowed(string name) => $"this hack's stats do not follow the standard formula for {name}, so level and species stay locked";

    public static string NotRead(int slot, string why) => $"party slot {slot} does not read ({why}), so its stats cannot be checked and level and species stay locked";

    public static string NoRow(int species) => $"species #{species} has no base stats or growth rate read from the ROM, so a party Pokemon's species, EXP and level stay locked";

    public static PartyStatsRow? Row(HackData? data, int species, out string? why)
    {
        if (data is null || data.Species.Count == 0 || !data.SpeciesOverlayable)
        {
            why = NoTable;
            return null;
        }
        var rows = data.Species.Where(s => s.Id == species).Take(2).ToList();
        if (rows.Count != 1 || rows[0] is not { BaseStats.Count: 6, Growth: >= 0 and < GrowthCurves } s || s.BaseStats.Any(b => b is < 1 or > 255))
        {
            why = NoRow(species);
            return null;
        }
        var b = s.BaseStats;
        why = null;
        return new PartyStatsRow(species, s.Name is { Length: > 0 } name ? name : $"#{species}", new StatSet(b[0], b[1], b[2], b[4], b[5], b[3]), (byte)s.Growth.Value);
    }

    public static byte LevelOf(uint exp, byte growth) => Experience.GetLevel(exp, growth);

    public static uint MinExp(byte level, byte growth) => Experience.GetEXP(level, growth);

    public static StatSet Compute(PartyStatsRow row, byte level, StatSet ivs, StatSet evs, byte nature)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (level is < Experience.MinLevel or > Experience.MaxLevel)
            throw new ArgumentOutOfRangeException(nameof(level), level, $"a level is {Experience.MinLevel} to {Experience.MaxLevel}");
        if (nature >= 25)
            throw new ArgumentOutOfRangeException(nameof(nature), nature, "a nature is 0 to 24");
        if (RangeProblem(row, level, ivs, evs, nature) is { } range)
            throw new InvalidOperationException(range);
        var raw = PreNature(row, level, ivs, evs);
        return new StatSet(
            raw.Hp,
            Natured(nature, 0, raw.Atk),
            Natured(nature, 1, raw.Def),
            Natured(nature, 3, raw.SpA),
            Natured(nature, 4, raw.SpD),
            Natured(nature, 2, raw.Spe));
    }

    public static StatSet PreNature(PartyStatsRow row, byte level, StatSet ivs, StatSet evs)
    {
        ArgumentNullException.ThrowIfNull(row);
        var hp = row.Base.Hp == 1 ? 1 : ((((2 * row.Base.Hp) + ivs.Hp + (evs.Hp / 4)) * level) / 100) + level + 10;
        int Stat(int baseStat, int iv, int ev) => ((((2 * baseStat) + iv + (ev / 4)) * level) / 100) + 5;
        return new StatSet(
            hp,
            Stat(row.Base.Atk, ivs.Atk, evs.Atk),
            Stat(row.Base.Def, ivs.Def, evs.Def),
            Stat(row.Base.SpA, ivs.SpA, evs.SpA),
            Stat(row.Base.SpD, ivs.SpD, evs.SpD),
            Stat(row.Base.Spe, ivs.Spe, evs.Spe));
    }

    public static string? RangeProblem(PartyStatsRow row, byte level, StatSet ivs, StatSet evs, byte nature)
    {
        ArgumentNullException.ThrowIfNull(row);
        var up = nature / 5;
        var down = nature % 5;
        if (nature >= 25 || up == down)
            return null;
        var raw = PreNature(row, level, ivs, evs);
        int[] byIndex = [raw.Atk, raw.Def, raw.Spe, raw.SpA, raw.SpD];
        return byIndex[up] > BoostedLimit || byIndex[down] > LoweredLimit ? NatureRange : null;
    }

    public static int Natured(byte nature, int index, int stat)
    {
        var up = nature / 5;
        var down = nature % 5;
        if (up == down)
            return stat;
        return index == up ? stat * 110 / 100 : index == down ? stat * 90 / 100 : stat;
    }

    public static int CurrentHp(int oldMax, int oldCurrent, int newMax)
    {
        if (oldCurrent <= 0)
            return 0;
        var lost = Math.Max(0, oldMax - oldCurrent);
        return Math.Clamp(newMax - lost, 1, Math.Max(1, newMax));
    }

    public static string? Mismatch(HackMon mon, PartyStatsRow row)
    {
        ArgumentNullException.ThrowIfNull(mon);
        ArgumentNullException.ThrowIfNull(row);
        if (mon.Party is not { } party)
            return $"{row.Name}: the record has no party stats";
        if (mon.Ivs is not { } ivs || mon.Nature is not { } nature || nature >= 25)
            return $"{row.Name}: {NoIvs}";
        var level = LevelOf(mon.Exp, row.Growth);
        if (level != party.Level)
            return $"{row.Name}: the game stored level {party.Level}, EXP {mon.Exp} on this species' curve gives level {level}";
        if (RangeProblem(row, party.Level, ivs, mon.Evs, nature) is { } range)
            return $"{row.Name}: {range}";
        var stats = Compute(row, party.Level, ivs, mon.Evs, nature);
        return stats == party.Stats ? null : $"{row.Name}: the game stored stats {party.Stats}, the formula gives {stats}";
    }

    public static PartyStatsVerdict Check(HackSave save, HackData? data)
    {
        ArgumentNullException.ThrowIfNull(save);
        return Check(save.Party, save.PartyRefused, data);
    }

    public static PartyStatsVerdict Check(IReadOnlyList<HackMon?> party, IReadOnlyDictionary<int, string> refused, HackData? data)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(refused);
        var checkedRows = new List<string>();
        if (refused.Count > 0)
        {
            var first = refused.OrderBy(r => r.Key).First();
            return new PartyStatsVerdict(false, NotRead(first.Key + 1, first.Value), checkedRows);
        }
        if (!party.Any(m => m is not null))
            return new PartyStatsVerdict(false, NoParty, checkedRows);
        for (var i = 0; i < party.Count; i++)
        {
            if (party[i] is not { } mon)
                continue;
            if (Row(data, mon.Species, out var why) is not { } row)
                return new PartyStatsVerdict(false, why, checkedRows);
            if (Mismatch(mon, row) is { } mismatch)
            {
                checkedRows.Add($"party slot {i + 1} {mismatch}");
                return new PartyStatsVerdict(false, NotFollowed(row.Name), checkedRows);
            }
            checkedRows.Add($"party slot {i + 1} {row.Name}: level {mon.Party!.Level} and stats {mon.Party.Stats} follow the formula");
        }
        return new PartyStatsVerdict(true, null, checkedRows);
    }
}

public static class MovePp
{
    public const byte Unread = 5;

    public static string UnreadNote(int slot, int move) => $"Move {slot + 1}: the ROM's PP for move #{move} was not read, so its PP is set to {Unread}";

    public static byte For(HackData? data, int move, int ppUps, out bool read)
    {
        read = true;
        if (move == 0)
            return 0;
        var ups = Math.Clamp(ppUps, 0, 3);
        var movesRead = data is not null && (data.Table(HackTableStatus.Moves) is not { } table || table.IsRead);
        var rows = movesRead ? data!.Moves.Where(m => m.Id == move).Take(2).ToList() : new List<HackMove>();
        if (rows.Count != 1 || rows[0].Pp is < 1 or > 255)
        {
            read = false;
            return Unread;
        }
        var basePp = rows[0].Pp;
        return (byte)Math.Min(255, basePp + (basePp * 20 * ups / 100));
    }
}
