using System;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHeX.Core;

namespace PKHaX.Hacks.Saves.Cfru;

public static class CfruPk3Bridge
{
    public const int LastGen3Species = 386;
    public const int LastGen3Ball = 12;

    public static PK3? ToPk3(HackMon mon, HackData data, out string? why)
    {
        var problems = new List<string>();
        var species = data.Species.FirstOrDefault(s => s.Id == mon.Species);
        if (species?.National is not { } national || national is < 1 or > LastGen3Species || species.Form != 0)
            problems.Add($"species {mon.Species} ({species?.Name ?? "not in the hack's table"}) has no Gen 3 national id");
        var moves = new ushort[4];
        for (var i = 0; i < 4; i++)
        {
            if (mon.Moves[i] == 0)
                continue;
            var move = data.Moves.FirstOrDefault(m => m.Id == mon.Moves[i]);
            if (move?.National is not { } n || n is < 1 or > IdMapper.Gen3LastMove)
                problems.Add($"move {mon.Moves[i]} ({move?.Name ?? "not in the hack's table"}) is not a Gen 3 move");
            else
                moves[i] = (ushort)n;
        }
        ushort item = 0;
        if (mon.HeldItem != 0)
        {
            var hackItem = data.Items.FirstOrDefault(x => x.Id == mon.HeldItem);
            item = hackItem?.National is { } itemNational ? ItemConverter.GetItemOld3((ushort)itemNational) : (ushort)0;
            if (item == 0)
                problems.Add($"held item {mon.HeldItem} ({hackItem?.Name ?? "not in the hack's table"}) has no Gen 3 item id");
        }
        if (mon.Ball is > LastGen3Ball)
            problems.Add($"ball {mon.Ball} is not a Gen 3 ball");
        if (mon.HiddenAbility == true)
            problems.Add("it has its hidden ability, which a PK3 cannot hold");
        if (problems.Count > 0)
        {
            why = string.Join("; ", problems);
            return null;
        }
        var pk = new PK3
        {
            Species = (ushort)species!.National!.Value,
            PID = mon.Pid,
            TID16 = mon.Tid16,
            SID16 = mon.Sid16,
            Language = mon.Language ?? (int)LanguageID.English,
            EXP = mon.Exp,
            HeldItem = item,
            Ball = mon.Ball ?? 4,
            OriginalTrainerFriendship = mon.Friendship ?? 70,
            IsEgg = mon.IsEgg,
            AbilityBit = (mon.Pid & 1) == 1,
        };
        if (mon.Nickname is { } nick)
            pk.Nickname = nick;
        if (mon.OtName is { } ot)
            pk.OriginalTrainerName = ot;
        pk.Move1 = moves[0];
        pk.Move2 = moves[1];
        pk.Move3 = moves[2];
        pk.Move4 = moves[3];
        if (mon.Pp is { } pp)
        {
            pk.Move1_PP = pp[0];
            pk.Move2_PP = pp[1];
            pk.Move3_PP = pp[2];
            pk.Move4_PP = pp[3];
        }
        if (mon.PpUps is { } ups)
        {
            pk.Move1_PPUps = ups[0];
            pk.Move2_PPUps = ups[1];
            pk.Move3_PPUps = ups[2];
            pk.Move4_PPUps = ups[3];
        }
        pk.EV_HP = mon.Evs.Hp;
        pk.EV_ATK = mon.Evs.Atk;
        pk.EV_DEF = mon.Evs.Def;
        pk.EV_SPA = mon.Evs.SpA;
        pk.EV_SPD = mon.Evs.SpD;
        pk.EV_SPE = mon.Evs.Spe;
        if (mon.Ivs is { } ivs)
        {
            pk.IV_HP = ivs.Hp;
            pk.IV_ATK = ivs.Atk;
            pk.IV_DEF = ivs.Def;
            pk.IV_SPA = ivs.SpA;
            pk.IV_SPD = ivs.SpD;
            pk.IV_SPE = ivs.Spe;
        }
        why = null;
        return pk;
    }

    public static HackMon? FromPk3(PK3 pk, HackMon original, HackData data, out string? why)
    {
        var problems = new List<string>();
        if (ToPk3(original, data, out var refused) is not { } was)
        {
            why = $"the original Pokemon has no PK3 form: {refused}";
            return null;
        }
        if (pk.PID != was.PID)
            problems.Add("PID (nature, shininess, ability slot)");
        if (pk.TID16 != was.TID16 || pk.SID16 != was.SID16)
            problems.Add("Trainer ID / Secret ID");
        if (pk.Nickname != was.Nickname)
            problems.Add("nickname");
        if (pk.OriginalTrainerName != was.OriginalTrainerName)
            problems.Add("OT name");
        if (pk.Language != was.Language)
            problems.Add("language");
        if (pk.AbilityBit != was.AbilityBit)
            problems.Add("ability slot");
        var growth = data.Species.FirstOrDefault(s => s.Id == original.Species)?.Growth;
        if (pk.EXP != was.EXP && original.Party is not null && growth is null)
            problems.Add("EXP (this species' growth rate is not read from the hack, so the level it gives is unknown)");
        if (pk.EXP != was.EXP && original.Party is { } partyStats && growth is { } g && Experience.GetLevel(pk.EXP, (byte)g) != partyStats.Level)
            problems.Add("EXP that changes the level (party stats are not recalculated yet)");
        if (problems.Count > 0)
        {
            why = "these edits cannot be written back to the CFRU record yet: " + string.Join(", ", problems);
            return null;
        }
        var species = Single(data.Species.Where(s => s.National == pk.Species && s.Form == 0).Select(s => s.Id), $"species {pk.Species}", problems);
        var moves = new ushort[4];
        var wanted = new[] { pk.Move1, pk.Move2, pk.Move3, pk.Move4 };
        for (var i = 0; i < 4; i++)
        {
            if (wanted[i] == 0)
                continue;
            moves[i] = (ushort)(Single(data.Moves.Where(m => m.National == wanted[i]).Select(m => m.Id), $"move {wanted[i]}", problems) ?? 0);
        }
        ushort item = 0;
        if (pk.HeldItem != 0)
        {
            var national = ItemConverter.GetItemFuture3((ushort)pk.HeldItem);
            item = (ushort)(Single(data.Items.Where(x => x.National == national).Select(x => x.Id), $"item {pk.HeldItem}", problems) ?? 0);
        }
        if (problems.Count > 0)
        {
            why = string.Join("; ", problems);
            return null;
        }
        why = null;
        return original with
        {
            Species = (ushort)species!.Value,
            National = pk.Species,
            Exp = pk.EXP,
            HeldItem = item,
            Ball = pk.Ball,
            Friendship = pk.OriginalTrainerFriendship,
            IsEgg = pk.IsEgg,
            Moves = moves,
            Pp = [(byte)pk.Move1_PP, (byte)pk.Move2_PP, (byte)pk.Move3_PP, (byte)pk.Move4_PP],
            PpUps = [(byte)pk.Move1_PPUps, (byte)pk.Move2_PPUps, (byte)pk.Move3_PPUps, (byte)pk.Move4_PPUps],
            Evs = new StatSet(pk.EV_HP, pk.EV_ATK, pk.EV_DEF, pk.EV_SPA, pk.EV_SPD, pk.EV_SPE),
            Ivs = new StatSet(pk.IV_HP, pk.IV_ATK, pk.IV_DEF, pk.IV_SPA, pk.IV_SPD, pk.IV_SPE),
        };
    }

    private static int? Single(IEnumerable<int> ids, string what, List<string> problems)
    {
        var list = ids.Distinct().Take(2).ToList();
        if (list.Count == 1)
            return list[0];
        problems.Add(list.Count == 0 ? $"{what} has no id in the hack's table" : $"{what} matches more than one id in the hack's table");
        return null;
    }
}
