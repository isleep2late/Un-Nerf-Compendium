using System;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Overlay;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public sealed class MoveOverlayTests
{
    private const ushort Tackle = (ushort)Move.Tackle;
    private const ushort Growl = (ushort)Move.Growl;
    private const ushort Curse = (ushort)Move.Curse;

    private static HackData Data(params HackMove[] moves) => new()
    {
        RomSha1 = new string('0', 40),
        Reader = NdsHackData.Reader,
        Moves = [new HackMove(0, "", null, 0, 0, 0, 0), .. moves],
        Tables = [new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Read)],
    };

    [Fact]
    public void TheHookAnswersFromTheOverrideOnlyForItsOwnContextAndOnlyWhileItIsInUse()
    {
        var stockType = MoveInfo.GetType(Tackle, EntityContext.Gen4);
        var stockPp = MoveInfo.GetPP(EntityContext.Gen4, Tackle);
        var types = new byte[MoveInfo.GetPPTable(EntityContext.Gen4).Length];
        Array.Fill(types, MoveOverrideLayer.NoType);
        var pp = new byte[types.Length];
        types[Tackle] = 17;
        pp[Tackle] = 40;
        Assert.False(MoveOverride.IsActive);
        using (MoveOverride.Use(EntityContext.Gen4, types, pp))
        {
            Assert.Equal(17, MoveInfo.GetType(Tackle, EntityContext.Gen4));
            Assert.Equal(40, MoveInfo.GetPP(EntityContext.Gen4, Tackle));
            Assert.Equal(MoveInfo.GetPP(EntityContext.Gen4, Growl), MoveInfo.GetPPTable(EntityContext.Gen4)[Growl]);
            Assert.Equal(stockType, MoveInfo.GetType(Tackle, EntityContext.Gen3));
            Assert.Equal(MoveInfo.GetPPTable(EntityContext.Gen3)[Tackle], MoveInfo.GetPP(EntityContext.Gen3, Tackle));
        }
        Assert.False(MoveOverride.IsActive);
        Assert.Equal(stockType, MoveInfo.GetType(Tackle, EntityContext.Gen4));
        Assert.Equal(stockPp, MoveInfo.GetPP(EntityContext.Gen4, Tackle));
    }

    [Fact]
    public void TheNewestScopeWinsAndClosingAnOlderOneKeepsIt()
    {
        var size = MoveInfo.GetPPTable(EntityContext.Gen3).Length;
        byte[] Pp(byte value)
        {
            var pp = new byte[size];
            pp[Tackle] = value;
            return pp;
        }
        var outer = MoveOverride.Use(EntityContext.Gen3, ReadOnlyMemory<byte>.Empty, Pp(11));
        var inner = MoveOverride.Use(EntityContext.Gen3, ReadOnlyMemory<byte>.Empty, Pp(22));
        Assert.Equal(22, MoveInfo.GetPP(EntityContext.Gen3, Tackle));
        outer.Dispose();
        Assert.Equal(22, MoveInfo.GetPP(EntityContext.Gen3, Tackle));
        Assert.Equal(1, MoveOverride.ScopeCount);
        inner.Dispose();
        inner.Dispose();
        Assert.Equal(0, MoveOverride.ScopeCount);
        Assert.Equal(MoveInfo.GetPPTable(EntityContext.Gen3)[Tackle], MoveInfo.GetPP(EntityContext.Gen3, Tackle));
    }

    [Fact]
    public void APartialMoveTableGivesNoOverlay()
    {
        var data = Data(new HackMove(Tackle, "TACKLE", Tackle, 40, 1, 100, 40)) with { Tables = [new HackTableStatus(HackTableStatus.Moves, HackTableStatus.Partial, "names only")] };
        var overlay = MoveOverlay.From(data, GameVersion.Pt);
        Assert.True(overlay.IsEmpty);
        Assert.Contains(overlay.Notes, n => n.Contains("every move keeps PKHeX's own type and PP", StringComparison.Ordinal));
        using (overlay.Use())
            Assert.False(MoveOverride.IsActive);
    }

    [Fact]
    public void RowsWithoutPpAndRowsPastPkhexsMovesAreSkippedAndCounted()
    {
        var count = MoveInfo.GetPPTable(EntityContext.Gen4).Length;
        var data = Data(new HackMove(Tackle, "TACKLE", Tackle, 40, 1, 100, 40), new HackMove(Growl, "GROWL", Growl, 0, 0, 0, 0), new HackMove(count + 3, "NEWMOVE", null, 90, 1, 100, 10));
        var overlay = MoveOverlay.From(data, GameVersion.Pt);
        Assert.Equal(1, overlay.Taken);
        Assert.Contains(overlay.Notes, n => n.Contains("1 moves lie past PKHeX's", StringComparison.Ordinal));
        Assert.Contains(overlay.Notes, n => n.Contains("1 moves have no PP read", StringComparison.Ordinal));
        Assert.Equal(MoveOverrideLayer.NoType, overlay.Types.Span[Growl]);
        Assert.Equal(MoveOverrideLayer.NoPP, overlay.PP.Span[Growl]);
    }

    [Fact]
    public void OneMoveOnTheMysteryTypeIsTheBaseGamesCurseAndSeveralAreFairy()
    {
        var one = MoveOverlay.From(Data(new HackMove(Curse, "CURSE", Curse, 0, GameTypes.MysteryRaw34, 0, 10), new HackMove(Tackle, "TACKLE", Tackle, 35, 0, 95, 35)), GameVersion.E);
        Assert.Equal(MoveOverrideLayer.NoType, one.Types.Span[Curse]);
        Assert.Equal(10, one.PP.Span[Curse]);
        Assert.Contains(one.Notes, n => n.Contains("as the base game's Curse does", StringComparison.Ordinal));

        var two = MoveOverlay.From(Data(new HackMove(Tackle, "TACKLE", Tackle, 35, GameTypes.MysteryRaw34, 95, 35), new HackMove(Growl, "GROWL", Growl, 0, GameTypes.MysteryRaw34, 100, 40)), GameVersion.E);
        Assert.Equal(MoveOverlay.PkhexFairy, two.Types.Span[Tackle]);
        Assert.Equal(MoveOverlay.PkhexFairy, two.Types.Span[Growl]);
        Assert.Contains(two.Notes, n => n.Contains("which this hack uses for Fairy", StringComparison.Ordinal));

        var count = MoveInfo.GetPPTable(EntityContext.Gen3).Length;
        var past = MoveOverlay.From(Data(new HackMove(Curse, "CURSE", Curse, 0, GameTypes.MysteryRaw34, 0, 10), new HackMove(count + 2, "NEWMOVE", null, 90, GameTypes.MysteryRaw34, 100, 10)), GameVersion.E);
        Assert.Equal(MoveOverrideLayer.NoType, past.Types.Span[Curse]);
        Assert.Contains(past.Notes, n => n.Contains("as the base game's Curse does", StringComparison.Ordinal));
        Assert.DoesNotContain(past.Notes, n => n.Contains("which this hack uses for Fairy", StringComparison.Ordinal));

        var mixed = MoveOverlay.From(Data(new HackMove(Curse, "CURSE", Curse, 0, GameTypes.MysteryRaw34, 0, 10), new HackMove(Tackle, "TACKLE", Tackle, 35, GameTypes.MysteryRaw34, 95, 35), new HackMove(Growl, "GROWL", Growl, 0, GameTypes.MysteryRaw34, 100, 40)), GameVersion.E);
        Assert.Equal(MoveOverrideLayer.NoType, mixed.Types.Span[Curse]);
        Assert.Equal(10, mixed.PP.Span[Curse]);
        Assert.Equal(MoveOverlay.PkhexFairy, mixed.Types.Span[Tackle]);
        Assert.Contains(mixed.Notes, n => n.Contains("2 moves use the ??? type id", StringComparison.Ordinal));
        Assert.Contains(mixed.Notes, n => n.Contains("Curse also uses the ??? type id", StringComparison.Ordinal));
    }

    [Fact]
    public void GameBoyTypeIdsAreTurnedIntoPkhexTypes()
    {
        Assert.Equal((int)MoveType.Fire, MoveOverlay.TypeOf(1, 0x14));
        Assert.Equal((int)MoveType.Dragon, MoveOverlay.TypeOf(1, 0x1A));
        Assert.Equal((int)MoveType.Bug, MoveOverlay.TypeOf(1, 7));
        Assert.Equal((int)MoveType.Ghost, MoveOverlay.TypeOf(2, 8));
        Assert.Equal((int)MoveType.Steel, MoveOverlay.TypeOf(2, 9));
        Assert.Equal((int)MoveType.Dark, MoveOverlay.TypeOf(2, 0x1B));
        Assert.Null(MoveOverlay.TypeOf(1, 9));
        Assert.Null(MoveOverlay.TypeOf(1, 6));
        Assert.Null(MoveOverlay.TypeOf(2, GbStructure.CurseType));
        Assert.Equal((int)MoveType.Fire, MoveOverlay.TypeOf(3, 10));
        Assert.Null(MoveOverlay.TypeOf(5, 18));
        Assert.Equal(MoveInfo.GetType((ushort)Move.Ember, EntityContext.Gen1), (byte)MoveOverlay.TypeOf(1, 0x14)!.Value);
    }

    [Fact]
    public void TheHackOverlayAppliesItsMovesWithItsStatsAndStrings()
    {
        var data = Data(new HackMove(Tackle, "TACKLE", Tackle, 40, 1, 100, 40), new HackMove(Growl, "GROWL", Growl, 0, 4, 100, 30));
        var overlay = HackOverlay.From(data, GameVersion.Pt);
        Assert.NotNull(overlay.Moves);
        Assert.Contains(overlay.Notes, n => n.Contains("2 moves take the hack's type and PP", StringComparison.Ordinal));
        var stock = MoveInfo.GetPP(EntityContext.Gen4, Tackle);
        using (overlay.Use())
        {
            Assert.Equal((byte)MoveType.Fighting, MoveInfo.GetType(Tackle, EntityContext.Gen4));
            Assert.Equal((byte)MoveType.Ground, MoveInfo.GetType(Growl, EntityContext.Gen4));
            Assert.Equal(40, MoveInfo.GetPP(EntityContext.Gen4, Tackle));
            Assert.True(PersonalOverride.IsActive);
        }
        Assert.False(MoveOverride.IsActive);
        Assert.False(PersonalOverride.IsActive);
        Assert.Equal(stock, MoveInfo.GetPP(EntityContext.Gen4, Tackle));
    }
}
