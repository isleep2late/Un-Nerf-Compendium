using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public sealed class NameVoteTests
{
    private static readonly GameStrings En = GameInfo.GetStrings("en");

    internal const int ItemTable = 0x40000;
    internal const int ItemStride = 44;
    internal const int ItemRows = 400;
    internal const int Rotate = 3;

    internal static string RotatedItem(int i) => i == 0 ? "????????" : En.itemlist[((i - 1 + Rotate) % ItemRows) + 1];

    internal static byte[] RotatedItems()
    {
        var rom = new byte[0x80000];
        for (var i = 0; i <= ItemRows; i++)
        {
            var at = ItemTable + (i * ItemStride);
            rom.AsSpan(at, Gen3Tables.ItemNameLength).Fill(0xFF);
            StringConverter3.SetString(rom.AsSpan(at, Gen3Tables.ItemNameLength), RotatedItem(i), Gen3Tables.ItemNameLength - 1, (int)LanguageID.English);
            BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(at + 0x0E), (ushort)i);
        }
        return rom;
    }

    [Fact]
    public void AnItemTableWithPokeBallFirstIsFoundByMembership()
    {
        Assert.Equal("Poké Ball", RotatedItem(1));
        var rom = new GbaImage(RotatedItems());
        var hit = NameVote.SearchGba(rom, ItemTable, NameVote.StockItems, NameVoteMode.ByMembership, NameVote.Window, FrTableFollower.ItemNameLengths, FrTableFollower.MaxItemStride);
        Assert.NotNull(hit);
        Assert.Equal((ItemStride, 0, false), (hit!.Layout.Stride, hit.Layout.NameOffset, hit.Layout.Pointer));
        Assert.True(hit.Agree >= NameVote.MinAgree);
        Assert.Null(NameVote.SearchGba(rom, ItemTable, NameVote.StockItems, NameVoteMode.ByIndex, NameVote.Window, FrTableFollower.ItemNameLengths, FrTableFollower.MaxItemStride));
    }

    [Fact]
    public void Gen1NamesInInternalOrderAgreeWithTheInternalListOnly()
    {
        string[] internalOrder = [.. Enumerable.Range(0, 191).Select(i => i == 0 ? "" : SpeciesConverter.GetNational1((byte)i) is var n and > 0 ? En.specieslist[n] : "")];
        string[] names = [.. internalOrder.Select(n => n.ToUpperInvariant())];
        var (agree, nonBlank) = NameVote.Score(names, internalOrder, NameVoteMode.ByIndex, NameVote.Window);
        Assert.True(NameVote.Accepted(agree, nonBlank), $"{agree}/{nonBlank}");
        var (national, _) = NameVote.Score(names, En.specieslist, NameVoteMode.ByIndex, NameVote.Window);
        Assert.False(NameVote.Accepted(national, nonBlank), $"{national}/{nonBlank}");
    }

    [Fact]
    public void ThirtyNineAgreeingRowsAreNotEnough()
    {
        string[] names = [.. Enumerable.Range(0, 100).Select(i => i is >= 1 and <= 39 ? En.specieslist[i] : i == 0 ? "" : $"Zorb{i}")];
        var (agree, nonBlank) = NameVote.Score(names, En.specieslist, NameVoteMode.ByIndex, NameVote.Window);
        Assert.Equal(39, agree);
        Assert.False(NameVote.Accepted(agree, nonBlank));
        Assert.False(NameVote.Accepted(39, 40));
        Assert.True(NameVote.Accepted(40, 66));
        Assert.False(NameVote.Accepted(40, 67));
    }
}
