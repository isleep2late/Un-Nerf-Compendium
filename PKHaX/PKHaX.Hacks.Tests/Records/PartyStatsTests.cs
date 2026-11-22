using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Tests.Saves;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Records;

public sealed class PartyStatsTests
{
    private static readonly Dictionary<int, string> None = [];

    private static HackData Table() => new()
    {
        RomSha1 = new string('0', 40),
        Reader = "synthetic",
        Species =
        [
            Row(277, "Treecko", [40, 45, 35, 70, 65, 55], 3),
            Row(278, "Grovyle", [50, 65, 45, 95, 85, 65], 3),
            Row(1100, "Shedmon", [1, 90, 45, 40, 30, 30], 1),
        ],
        Moves = [new HackMove(1, "Pound", 1, 40, 0, 100, 35), new HackMove(2, "Karate Chop", 2, 50, 1, 100, 25)],
    };

    private static HackSpecies Row(int id, string name, int[] stats, int growth) => new(id, name, null, 0, [12, 12], stats, [], growth, 0, 45, 65);

    private static PK3 Pk(Species species, uint pid, byte level)
    {
        var pk = new PK3
        {
            Species = (ushort)species, PID = pid, TID16 = 1, SID16 = 2, Nickname = "SYNTH", OriginalTrainerName = "PKHAX", Language = (int)LanguageID.English,
            Move1 = 1, Move1_PP = 35, OriginalTrainerFriendship = 70, Ball = 4, IV_HP = 31, IV_ATK = 20, IV_SPE = 7, EV_HP = 8, EV_SPA = 252,
        };
        pk.EXP = Experience.GetEXP(level, 3);
        pk.Stat_Level = level;
        pk.ResetPartyStats();
        return pk;
    }

    private static HackMon Party(PK3 pk)
    {
        var bytes = new byte[pk.SIZE_PARTY];
        pk.WriteEncryptedDataParty(bytes);
        return RecordCodecs.For(RecordCodecId.Pk3x, "1.15").Decode(bytes, out var why) ?? throw new InvalidOperationException(why);
    }

    private static HackMon Write(HackMon mon) => RecordCodecs.For(RecordCodecId.Pk3x, "1.15").Decode(Pk3xEncoder.Encode(mon), out var why) ?? throw new InvalidOperationException(why);

    private static StatSet Stats(PK3 pk) => new(pk.Stat_HPMax, pk.Stat_ATK, pk.Stat_DEF, pk.Stat_SPA, pk.Stat_SPD, pk.Stat_SPE);

    private static HackMonEditModel Edit(HackMon mon, HackData data) =>
        new(HackSlotRef.InParty(0), mon, null, new HackEditContext(data, PartyStats.Check([mon], None, data)));

    [Fact]
    public void ThePublicVectorFollowsTheFormula()
    {
        var garchomp = new PartyStatsRow(445, "Garchomp", new StatSet(108, 130, 95, 80, 85, 102), 5);
        Assert.Equal(new StatSet(289, 278, 193, 135, 171, 171), PartyStats.Compute(garchomp, 78, new StatSet(24, 12, 30, 16, 23, 5), new StatSet(74, 190, 91, 48, 84, 23), 3));
    }

    [Fact]
    public void TheFormulaAgreesWithPkhexAndBaseHpOneGivesOne()
    {
        var data = Table();
        var treecko = PartyStats.Row(data, 277, out _)!;
        var pk = Pk(Species.Treecko, 0x1234_5678, 37);
        Assert.Equal(Stats(pk), PartyStats.Compute(treecko, 37, new StatSet(31, 20, 0, 0, 0, 7), new StatSet(8, 0, 0, 252, 0, 0), 21));
        var shed = PartyStats.Row(data, 1100, out _)!;
        Assert.Equal(1, PartyStats.Compute(shed, 50, new StatSet(31, 31, 31, 31, 31, 31), new StatSet(252, 0, 0, 0, 0, 0), 0).Hp);
        Assert.Null(PartyStats.Row(data, 900, out var why));
        Assert.Equal(PartyStats.NoRow(900), why);
        Assert.Null(PartyStats.Row(null, 277, out why));
        Assert.Equal(PartyStats.NoTable, why);
    }

    [Fact]
    public void CurrentHpKeepsTheHpLost()
    {
        Assert.Equal(43, PartyStats.CurrentHp(19, 16, 46));
        Assert.Equal(0, PartyStats.CurrentHp(19, 0, 46));
        Assert.Equal(1, PartyStats.CurrentHp(46, 10, 20));
        Assert.Equal(1, PartyStats.CurrentHp(19, 19, 1));
    }

    [Fact]
    public void ALevelEditRecomputesTheStatsAsPkhexDoes()
    {
        var data = Table();
        var mon = Party(Pk(Species.Treecko, 0x1234_5678, 5));
        var edit = Edit(mon, data);
        Assert.True(edit.CanEdit(EditableFields.Level));
        Assert.True(edit.CanEdit("Exp"));
        edit.Level = 30;
        Assert.Equal(Experience.GetEXP(30, 3), edit.Exp);
        var back = Write(edit.ToMon());
        var want = Pk(Species.Treecko, 0x1234_5678, 30);
        Assert.Equal((byte)30, back.Party!.Level);
        Assert.Equal(Stats(want), back.Party.Stats);
        Assert.Equal(back.Party.Stats.Hp, back.Party.CurrentHp);
        Assert.Equal(want.EXP, back.Exp);
    }

    [Fact]
    public void ASpeciesEditKeepsTheLevelAndUsesTheNewCurve()
    {
        var data = Table();
        var mon = Party(Pk(Species.Treecko, 0x1234_5678, 12));
        var edit = Edit(mon, data);
        edit.Species = 1100;
        var back = Write(edit.ToMon());
        Assert.Equal((ushort)1100, back.Species);
        Assert.Equal((byte)12, back.Party!.Level);
        Assert.Equal(Experience.GetEXP(12, 1), back.Exp);
        Assert.Equal(1, back.Party.Stats.Hp);
        Assert.Equal(1, back.Party.CurrentHp);
        Assert.Throws<InvalidOperationException>(() => edit.Species = 900);
    }

    [Fact]
    public void OneMemberOffTheFormulaLocksTheWholeParty()
    {
        var data = Table();
        var good = Party(Pk(Species.Treecko, 0x1234_5678, 5));
        var bad = Write(good with { Party = good.Party! with { Stats = good.Party.Stats with { Def = good.Party.Stats.Def + 2 } } });
        var verdict = PartyStats.Check([good, bad], None, data);
        Assert.False(verdict.Agree);
        Assert.Equal(PartyStats.NotFollowed("Treecko"), verdict.Sentence);
        var edit = new HackMonEditModel(HackSlotRef.InParty(0), good, null, new HackEditContext(data, verdict));
        Assert.False(edit.CanEdit("Species"));
        Assert.Equal(PartyStats.NotFollowed("Treecko"), edit.LockedReason(EditableFields.Level));
        Assert.Throws<InvalidOperationException>(() => edit.Exp = 5000);
        Assert.Equal(EditFieldSpecs.PartyLocked, new HackMonEditModel(HackSlotRef.InParty(0), good).LockedReason("Exp"));
        Assert.Equal(PartyStats.NoParty, PartyStats.Check([], None, data).Sentence);
        Assert.False(PartyStats.Check([good, null], new Dictionary<int, string> { [1] = "checksum" }, data).Agree);
    }

    [Fact]
    public void AnExpGateLockAlsoLocksTheLevel()
    {
        var data = Table();
        var mon = Party(Pk(Species.Treecko, 0x1234_5678, 5));
        var edit = new HackMonEditModel(HackSlotRef.InParty(0), mon, new Dictionary<string, string> { ["Exp"] = "locked by the gate" }, new HackEditContext(data, PartyStats.Check([mon], None, data)));
        Assert.False(edit.CanEdit(EditableFields.Level));
        Assert.False(edit.CanEdit("Species"));
        Assert.Equal("locked by the gate", edit.LockedReason(EditableFields.Level));
        Assert.Equal("locked by the gate", edit.LockedReason("Species"));
        var byLevel = new HackMonEditModel(HackSlotRef.InParty(0), mon, new Dictionary<string, string> { [EditableFields.Level] = "level not accepted" }, new HackEditContext(data, PartyStats.Check([mon], None, data)));
        Assert.False(byLevel.CanEdit("Exp"));
        Assert.False(byLevel.CanEdit("Species"));
        Assert.Equal("level not accepted", byLevel.LockedReason("Exp"));
    }

    [Fact]
    public void ThePartyLevelIsInTheWritableListOfAPartyRecord()
    {
        Assert.Contains(EditableFields.Level, EditableFields.Writable(RecordCodecId.Pk3x, RecordShape.Party));
        Assert.Contains(EditableFields.Level, EditableFields.Writable(RecordCodecId.Pk3SoulGold96, RecordShape.Party));
        Assert.DoesNotContain(EditableFields.Level, EditableFields.Writable(RecordCodecId.Pk3x, RecordShape.Stored));
        Assert.DoesNotContain(EditableFields.Level, EditableFields.For(RecordCodecId.Pk3x));
    }

    [Fact]
    public void TheGatesPartyLockGivesWayToThePartySentence()
    {
        var data = Table();
        var good = Party(Pk(Species.Treecko, 0x1234_5678, 5));
        var bad = Write(good with { Party = good.Party! with { Stats = good.Party.Stats with { Def = good.Party.Stats.Def + 2 } } });
        var verdict = PartyStats.Check([good, bad], None, data);
        var gated = EditableFields.PartyLocked.ToDictionary(f => f, _ => EditFieldSpecs.PartyLocked);
        var edit = new HackMonEditModel(HackSlotRef.InParty(0), good, gated, new HackEditContext(data, verdict));
        Assert.Equal(PartyStats.NotFollowed("Treecko"), edit.LockedReason("Species"));
        Assert.Equal(PartyStats.NotFollowed("Treecko"), edit.LockedReason(EditableFields.Level));
        var closed = new HackMonEditModel(HackSlotRef.InParty(0), good, new Dictionary<string, string> { ["Species"] = "the gate is closed" }, new HackEditContext(data, verdict));
        Assert.Equal("the gate is closed", closed.LockedReason("Species"));
    }

    [Fact]
    public void ANaturedStatPastTheGamesU16RangeIsRefused()
    {
        var big = new PartyStatsRow(277, "Bigtree", new StatSet(40, 45, 35, 65, 250, 70), 3);
        var ivs = new StatSet(31, 31, 31, 31, 31, 31);
        var evs = new StatSet(0, 0, 0, 0, 252, 0);
        Assert.Equal(599, PartyStats.PreNature(big, 100, ivs, evs).SpD);
        Assert.Equal(PartyStats.NatureRange, PartyStats.RangeProblem(big, 100, ivs, evs, 21));
        Assert.Throws<InvalidOperationException>(() => PartyStats.Compute(big, 100, ivs, evs, 21));
        Assert.Null(PartyStats.RangeProblem(big, 100, ivs, evs, 0));
        Assert.Null(PartyStats.RangeProblem(big, 99, ivs, evs, 21));
        Assert.Equal(593 * 110 / 100, PartyStats.Compute(big, 99, ivs, evs, 21).SpD);
        var edge = big with { Base = big.Base with { SpD = 248 } };
        Assert.Equal(595 * 110 / 100, PartyStats.Compute(edge, 100, ivs, evs, 21).SpD);
    }

    [Fact]
    public void AChangedMoveTakesTheRomPp()
    {
        var data = Table();
        var mon = Write(Party(Pk(Species.Treecko, 0x1234_5678, 5)) with { PpUps = [0, 2, 0, 0] });
        var edit = Edit(mon, data);
        edit.SetMove(1, 2);
        edit.SetMove(2, 700);
        var back = Write(edit.ToMon());
        Assert.Equal(new byte[] { 35, 35, MovePp.Unread, 0 }, back.Pp!.ToArray());
        Assert.Equal([MovePp.UnreadNote(2, 700)], edit.Notes);
        edit.SetMove(1, 0);
        Assert.Equal((byte)0, edit.Pp![1]);
    }

    [Fact]
    public void NicknamesAreWrittenInEveryEncoder()
    {
        var plain = RecordCodecs.For(RecordCodecId.Pk3CfruPlain).Decode(CfruSaveTests.PlainRecord(25, 9), out _)!;
        Assert.Equal("LEAF", RecordCodecs.For(RecordCodecId.Pk3CfruPlain).Decode(Pk3CfruPlainEncoder.Encode(plain with { Nickname = "LEAF" }), out _)!.Nickname);
        var compact = RecordCodecs.For(RecordCodecId.Pk3CfruCompact58).Decode(CfruSaveTests.CompactRecord(25), out _)!;
        var compactBytes = Pk3CfruCompact58Encoder.Encode(compact with { Nickname = "ABCDEFGHIJ" });
        Assert.Equal("ABCDEFGHIJ", RecordCodecs.For(RecordCodecId.Pk3CfruCompact58).Decode(compactBytes, out _)!.Nickname);
        Assert.Throws<RecordEncodeException>(() => Pk3CfruCompact58Encoder.Encode(compact with { Nickname = "ABCDEFGHIJK" }));
        Assert.Throws<RecordEncodeException>(() => Pk3CfruPlainEncoder.Encode(plain with { Nickname = "" }));
        Assert.Throws<RecordEncodeException>(() => Pk3CfruPlainEncoder.Encode(plain with { Nickname = "PK一" }));
    }

    [Fact]
    public void TheExpansionTailHoldsCharactersElevenAndTwelveOnly()
    {
        var mon = Party(Pk(Species.Treecko, 0x1234_5678, 5));
        var plainBefore = Pk3xEncoder.Plain(mon.Raw.Span);
        var shortName = Pk3xEncoder.Encode(mon with { Nickname = "LEAF" });
        var plainShort = Pk3xEncoder.Plain(shortName);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(plainBefore.AsSpan(0x24)) >> 21, BinaryPrimitives.ReadUInt32LittleEndian(plainShort.AsSpan(0x24)) >> 21);
        Assert.Equal(BinaryPrimitives.ReadUInt16LittleEndian(plainBefore.AsSpan(0x2A)) >> 6, BinaryPrimitives.ReadUInt16LittleEndian(plainShort.AsSpan(0x2A)) >> 6);
        var twelve = Write(mon with { Nickname = "ABCDEFGHIJKL" });
        Assert.Equal("ABCDEFGHIJKL", twelve.Nickname);
        Assert.Equal("ABCDEFGHIJ", Write(twelve with { Nickname = "ABCDEFGHIJ" }).Nickname);
        Assert.Equal("LEAF", Write(twelve with { Nickname = "LEAF" }).Nickname);
        Assert.Throws<RecordEncodeException>(() => Pk3xEncoder.Encode(mon with { Nickname = "ABCDEFGHIJKLM" }));
        var edit = Edit(mon, Table());
        edit.Nickname = "SPROUT";
        Assert.Equal(["Nickname"], edit.Changed);
        Assert.Equal("SPROUT", Write(edit.ToMon()).Nickname);
        Assert.Throws<InvalidOperationException>(() => edit.Nickname = "ABCDEFGHIJKLM");
    }
}
