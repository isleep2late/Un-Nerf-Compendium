using System;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Saves;

public sealed class CfruBridgeTests
{
    private static readonly HackData Data = new()
    {
        RomSha1 = new string('0', 40),
        Reader = "test",
        Species = [Sp(1022, "Lairon-like", null), Sp(252, "Treecko", 252), Sp(383, "Lairon", 305)],
        Moves = [new HackMove(33, "Tackle", 33, 40, 0, 100, 35), new HackMove(45, "Growl", 45, 0, 0, 100, 40), new HackMove(89, "Earthquake", 89, 100, 4, 100, 10), new HackMove(700, "New Move", null, 80, 1, 100, 10)],
        Items = [new HackItem(13, "Potion", 17, 2, 300), new HackItem(1, "Master Ball", 1, 3, 0)],
    };

    private static HackSpecies Sp(int id, string name, int? national) => new(id, name, national, 0, [12, 12], [40, 40, 40, 40, 40, 40], [65, 0], 0, 31, 45, 64);

    private static byte[] File(params byte[][] records) =>
        CfruSaveTests.WithParty(Gen3ContainerTests.Cfru(CfruVariant.UnboundSignature, 21), CfruVariant.UnboundSignature, 5, records);

    private static CfruSave Open(byte[] file) => (CfruSave)HackSaves.TryOpen(file, CfruVariant.Unbound.Id, WriteEvidence.Unproven, out var why)! ?? throw new InvalidOperationException(why);

    [Fact]
    public void AMonWhoseIdsAllMapBecomesAPk3()
    {
        var s = Open(File(CfruSaveTests.PlainRecord(252, 5)));
        var pk = CfruPk3Bridge.ToPk3(s.Party[0]!, Data, out var why);
        Assert.True(pk is not null, why);
        Assert.Equal((ushort)252, pk!.Species);
        Assert.Equal((ushort)33, pk.Move1);
        Assert.Equal((ushort)45, pk.Move2);
        Assert.Equal(13, pk.HeldItem);
        Assert.Equal(31, pk.IV_HP);
        Assert.Equal(20, pk.IV_ATK);
        Assert.Equal(1000u, pk.EXP);
    }

    [Fact]
    public void HackIdsAreMappedNotReadAsVanillaIds()
    {
        var s = Open(File(CfruSaveTests.PlainRecord(383, 30)));
        var pk = CfruPk3Bridge.ToPk3(s.Party[0]!, Data, out _);
        Assert.Equal((ushort)305, pk!.Species);
    }

    [Fact]
    public void AnUnmappedSpeciesOrMoveIsRefusedWithTheReason()
    {
        var s = Open(File(CfruSaveTests.PlainRecord(1022, 30)));
        Assert.Null(CfruPk3Bridge.ToPk3(s.Party[0]!, Data, out var why));
        Assert.Contains("species 1022", why);
        var withNewMove = s.Party[0]! with { Species = 252, Moves = [700, 0, 0, 0] };
        Assert.Null(CfruPk3Bridge.ToPk3(withNewMove, Data, out why));
        Assert.Contains("move 700", why);
    }

    [Fact]
    public void AnEditInTheFullEditorComesBackThroughTheCodec()
    {
        var file = File(CfruSaveTests.PlainRecord(252, 5));
        var s = Open(file);
        var pk = CfruPk3Bridge.ToPk3(s.Party[0]!, Data, out _)!;
        pk.Move3 = 89;
        pk.Move3_PP = 10;
        pk.EV_SPE = 100;
        pk.HeldItem = ItemConverter.GetItemOld3(1);
        var back = CfruPk3Bridge.FromPk3(pk, s.Party[0]!, Data, out var why);
        Assert.True(back is not null, why);
        s.SetPartySlot(0, back!);
        var r = s.Plan();
        Assert.True(r.Confined);
        var got = Open(r.Bytes).Party[0]!;
        Assert.Equal(new ushort[] { 33, 45, 89, 0 }, got.Moves);
        Assert.Equal(100, got.Evs.Spe);
        Assert.Equal((ushort)1, got.HeldItem);
        Assert.Equal((ushort)252, got.Species);
    }

    [Fact]
    public void ANationalIdTheHackDoesNotHaveIsRefusedOnTheWayBack()
    {
        var s = Open(File(CfruSaveTests.PlainRecord(252, 5)));
        var pk = CfruPk3Bridge.ToPk3(s.Party[0]!, Data, out _)!;
        pk.Species = 25;
        Assert.Null(CfruPk3Bridge.FromPk3(pk, s.Party[0]!, Data, out var why));
        Assert.Contains("species 25", why);
    }

    [Fact]
    public void EditsTheRecordCannotHoldAreRefusedNotDropped()
    {
        var s = Open(File(CfruSaveTests.PlainRecord(252, 5)));
        var mon = s.Party[0]!;
        Assert.Null(CfruPk3Bridge.ToPk3(mon with { HiddenAbility = true }, Data, out var why));
        Assert.Contains("hidden ability", why);
        var pk = CfruPk3Bridge.ToPk3(mon, Data, out _)!;
        pk.Nickname = "RENAMED";
        Assert.Null(CfruPk3Bridge.FromPk3(pk, mon, Data, out why));
        Assert.Contains("nickname", why);
        pk = CfruPk3Bridge.ToPk3(mon, Data, out _)!;
        pk.PID ^= 1;
        Assert.Null(CfruPk3Bridge.FromPk3(pk, mon, Data, out why));
        Assert.Contains("PID", why);
        pk = CfruPk3Bridge.ToPk3(mon, Data, out _)!;
        pk.EXP = 1_000_000;
        Assert.Null(CfruPk3Bridge.FromPk3(pk, mon, Data, out why));
        Assert.Contains("changes the level", why);
        Assert.Equal((mon.Pid & 1) == 1, CfruPk3Bridge.ToPk3(mon, Data, out _)!.AbilityBit);
    }

    [Fact]
    public void AnExpEditIsRefusedWhenTheHacksGrowthRateIsUnread()
    {
        var unread = Data with { Species = [.. Data.Species.Select(x => x with { Growth = null })] };
        var s = Open(File(CfruSaveTests.PlainRecord(252, 5)));
        var mon = s.Party[0]!;
        var pk = CfruPk3Bridge.ToPk3(mon, unread, out _)!;
        pk.EXP += 1;
        Assert.Null(CfruPk3Bridge.FromPk3(pk, mon, unread, out var why));
        Assert.Contains("growth rate is not read", why);
        pk = CfruPk3Bridge.ToPk3(mon, unread, out _)!;
        pk.EV_SPE = 100;
        Assert.NotNull(CfruPk3Bridge.FromPk3(pk, mon, unread, out why));
    }

    [Fact]
    public void TheFactoryNamesTheVariantsItHandles()
    {
        Assert.Equal(new[] { "gen3-cfru-unbound/1", "gen3-cfru-rr/1", "gen3-expansion/1", "gen3-legacy/1" }, HackSaves.Variants.ToArray());
        Assert.Null(HackSaves.TryOpen(new byte[0x20000], "gen3-legacy/1", WriteEvidence.Unproven, out var why));
        Assert.Contains("no retail Gen 3 section layout", why);
        Assert.Null(HackSaves.TryOpen(new byte[0x20000], "gen3-unknown/1", WriteEvidence.Unproven, out why));
        Assert.Contains("no hack-native save codec", why);
        Assert.Null(HackSaves.TryOpen(new byte[0x20000], "gen3-expansion/1", WriteEvidence.Unproven, out why));
        Assert.Contains("TryOpenExpansion", why);
    }
}
