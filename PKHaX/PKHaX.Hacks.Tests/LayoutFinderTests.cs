using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public sealed class LayoutFinderTests
{
    private const int Info = 0x20000;
    private const int NamePointers = 0x60000;
    private const int NameText = 0x61000;
    private const int MovePointers = 0x6C000;
    private const int MoveText = 0x6D000;
    private const int Stride = 44;

    private static readonly string[] English = GameInfo.GetStrings("en").specieslist;
    private static readonly string[] EnglishMoves = GameInfo.GetStrings("en").movelist;

    private sealed record Layout(int Type1, int Type2, int Catch, int Exp, int Gender, int Growth);

    private static readonly Layout Wide = new(0x20, 0x21, 0x22, 0x24, 0x26, 0x27);
    private static readonly Layout Vanilla = new(0x06, 0x07, 0x08, 0x09, 0x10, 0x13);

    internal static byte[] Rebalanced(int stride = Stride, int entries = 401, Func<int, bool>? blank = null, bool scramble = false, bool movePointers = false, int seed = 5, Func<byte, byte>? typeCode = null)
    {
        var layout = stride == 28 ? Vanilla : Wide;
        var rom = new byte[0x80000];
        HackDataTests.Image().CopyTo(rom, 0);
        rom.AsSpan(0x10000, 0x1000).Clear();
        rom.AsSpan(0x13000, 0x1000).Clear();
        var rng = new Random(seed);
        for (var i = 0; i < entries; i++)
        {
            var b = Info + (i * stride);
            rng.NextBytes(rom.AsSpan(b, stride));
            if (i == 0 || blank?.Invoke(i) == true)
            {
                rom.AsSpan(b, stride).Clear();
                continue;
            }
            var p = PersonalTable.E[Math.Min(i, 386)];
            byte[] stats = i == 1 ? [50, 55, 55, 50, 70, 70] : [(byte)p.HP, (byte)p.ATK, (byte)p.DEF, (byte)p.SPE, (byte)p.SPA, (byte)p.SPD];
            stats.CopyTo(rom, b);
            if (scramble)
                continue;
            rom[b + layout.Type1] = (typeCode ?? Gen3LayoutFinder.RawType)(p.Type1);
            rom[b + layout.Type2] = (typeCode ?? Gen3LayoutFinder.RawType)(p.Type2);
            rom[b + layout.Catch] = p.CatchRate;
            rom[b + layout.Exp] = (byte)p.BaseEXP;
            rom[b + layout.Gender] = p.Gender;
            rom[b + layout.Growth] = p.EXPGrowth;
        }
        PointerTable(rom, NamePointers, NameText, entries, i => i == 0 ? "??????????" : English[i]);
        BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x100 + 0x44), (uint)(0x08000000 + NamePointers));
        BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x100 + 0xBC), (uint)(0x08000000 + Info));
        if (movePointers)
        {
            rom.AsSpan(0x11000, 0x1000).Clear();
            PointerTable(rom, MovePointers, MoveText, 400, i => i == 0 ? "-" : EnglishMoves[i]);
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x100 + 0x48), (uint)(0x08000000 + MovePointers));
        }
        return rom;
    }

    private static void PointerTable(byte[] rom, int pointers, int text, int count, Func<int, string> name)
    {
        for (var i = 0; i < count; i++)
        {
            var span = rom.AsSpan(text, 16);
            span.Fill(0xFF);
            StringConverter3.SetString(span, name(i), 15, (int)LanguageID.English, StringConverterOption.None);
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(pointers + (i * 4)), (uint)(0x08000000 + text));
            text += 16;
        }
    }

    private const int ItemInfo = 0x70000;
    private const int OtherItemInfo = 0x68000;
    private const int ItemText = 0x7A000;

    private static string ItemName(int i) => i switch
    {
        0 => "????????",
        1 => "MASTER BALL",
        2 => "ULTRA BALL",
        3 => "GREAT BALL",
        4 => "POKE BALL",
        _ => $"ITEM {i}",
    };

    internal static byte[] PointerItems(int stride = 40, int nameAt = 8, int count = 320, Func<int, string?>? rename = null, bool zeroItem0 = false, int? otherStride = null, int? header = ItemInfo)
    {
        var rom = Rebalanced();
        rom.AsSpan(0x15000, 5 * 44).Clear();
        var text = ItemText;
        void Table(int at, int step, int n, int nameOffset)
        {
            for (var i = 0; i < n; i++)
            {
                var entry = at + (i * step);
                new Random(i).NextBytes(rom.AsSpan(entry, step));
                for (var w = 0; w + 4 <= step; w += 4)
                    rom[entry + w + 3] = 0;
                if (i == 0 && zeroItem0)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(entry + nameOffset), 0);
                    continue;
                }
                var span = rom.AsSpan(text, 16);
                span.Fill(0xFF);
                var name = rename?.Invoke(i) ?? ItemName(i);
                if (name.Length > 0)
                    StringConverter3.SetString(span, name, 15, (int)LanguageID.English, StringConverterOption.None);
                BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(entry + nameOffset), (uint)(0x08000000 + text));
                text += 16;
            }
        }
        Table(ItemInfo, stride, count, nameAt);
        if (otherStride is { } o)
            Table(OtherItemInfo, o, 5, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x100 + 0xC8), header is { } h ? (uint)(0x08000000 + h) : 0);
        return rom;
    }

    [Fact]
    public void ItemNamesBehindPointersInsideTheItemEntriesAreRead()
    {
        var r = GbaHackData.Read(PointerItems());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(320, d.Items.Count);
        Assert.Equal(["????????", "MASTER BALL", "ULTRA BALL", "GREAT BALL", "POKE BALL"], d.Items.Take(5).Select(i => i.Name));
        Assert.Equal(new int?[] { 1, 2, 3, 4 }, d.Items.Skip(1).Take(4).Select(i => i.National));
        Assert.True(d.Notes.Any(n => n.Contains("every 40 bytes, item 0's name pointer at 0x70008 (+0x8 from the GF header's item pointer 0x70000), 319 items named", StringComparison.Ordinal)), string.Join(" | ", d.Notes));
        Assert.DoesNotContain(d.Notes, n => n.Contains("(0 references)", StringComparison.Ordinal));
    }

    [Fact]
    public void AnItemNamePointerTableIsReadAtStrideFour()
    {
        var r = GbaHackData.Read(PointerItems(stride: 4, nameAt: 0));
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal("GREAT BALL", r.Data!.Items[3].Name);
        Assert.Equal(320, r.Data.Items.Count);
        Assert.Contains(r.Data.Notes, n => n.Contains("every 4 bytes, item 0's name pointer at 0x70000", StringComparison.Ordinal));
        Assert.Contains(r.Data.Notes, n => n.Contains("item names are behind pointers every 4 bytes", StringComparison.Ordinal));
    }

    [Fact]
    public void PointerNamesAtTheVanillaStrideAreStillReadThroughThePointers()
    {
        var r = GbaHackData.Read(PointerItems(stride: 44, nameAt: 0));
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal("MASTER BALL", r.Data!.Items[1].Name);
        Assert.Equal(320, r.Data.Items.Count);
    }

    [Fact]
    public void AnEmptyNameOrAZeroItemZeroDoesNotEndTheList()
    {
        var r = GbaHackData.Read(PointerItems(rename: i => i == 7 ? "" : null, zeroItem0: true));
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal(320, r.Data!.Items.Count);
        Assert.Equal("", r.Data.Items[0].Name);
        Assert.Equal("", r.Data.Items[7].Name);
        Assert.Equal("ITEM 8", r.Data.Items[8].Name);
    }

    [Fact]
    public void TheTableThatNamesTheMostItemsWinsOverASmallerStride()
    {
        var r = GbaHackData.Read(PointerItems(otherStride: 8, header: 0x7F000));
        Assert.True(r.Ok, r.Refusal + string.Join(" | ", r.Data?.Notes ?? []));
        Assert.Equal(320, r.Data!.Items.Count);
        Assert.Contains(r.Data.Notes, n => n.Contains("every 40 bytes", StringComparison.Ordinal));
        var picked = Gen3LayoutFinder.FindItemNamePointers(new GbaImage(PointerItems(otherStride: 8, header: OtherItemInfo)), OtherItemInfo)!;
        Assert.Equal((8, true), (picked.Stride, picked.InHeaderEntry));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void APointerToMasterBallWithoutUltraOrGreatBallAfterItIsNotAnItemTable(int missing)
    {
        var rom = PointerItems(rename: i => i == missing ? "SOMETHING" : null);
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        Assert.Null(Gen3LayoutFinder.FindItemNamePointers(new GbaImage(rom), ItemInfo));
        if (missing == 2)
        {
            Assert.Equal(HackTableStatus.Missing, r.Data!.StatusOf(HackTableStatus.Items));
            Assert.Contains("name pointers at an equal stride", r.Data.Table(HackTableStatus.Items)!.Reason, StringComparison.Ordinal);
            Assert.Contains("puts Master Ball and Ultra Ball among entries 1 to 8", r.Data.Table(HackTableStatus.Items)!.Reason, StringComparison.Ordinal);
            return;
        }
        Assert.Equal(HackTableStatus.Read, r.Data!.StatusOf(HackTableStatus.Items));
        Assert.Equal("SOMETHING", r.Data.Items[3].Name);
        Assert.Contains(r.Data.Notes, n => n.Contains("with Master Ball and Ultra Ball among entries 1 to 8", StringComparison.Ordinal));
    }

    [Fact]
    public void AFewNamedItemsAtTheHeaderPointerAreReadThroughTheHeader()
    {
        var r = GbaHackData.Read(PointerItems(count: 20));
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal(HackTableStatus.Read, r.Data!.StatusOf(HackTableStatus.Items));
        Assert.Equal(20, r.Data.Items.Count);
        Assert.Contains(r.Data.Notes, n => n.Contains("19 items named), but that is under 300", StringComparison.Ordinal) || n.Contains("items at the GF header's item pointer 0x70000", StringComparison.Ordinal));
    }

    [Fact]
    public void ARebalancedTableIsReadThroughTheHeaderWithItsFieldsFound()
    {
        var r = GbaHackData.Read(Rebalanced());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(401, d.Species.Count);
        Assert.Equal("Bulbasaur", d.Species[1].Name);
        Assert.Equal(1, d.Species[1].National);
        Assert.Equal([50, 55, 55, 50, 70, 70], d.Species[1].BaseStats);
        var torchic = PersonalTable.E[255];
        Assert.Equal([Gen3LayoutFinder.RawType(torchic.Type1), Gen3LayoutFinder.RawType(torchic.Type2)], d.Species[255].Types);
        Assert.Equal(torchic.CatchRate, d.Species[255].CatchRate);
        Assert.Equal(torchic.EXPGrowth, d.Species[255].Growth);
        Assert.Equal(torchic.Gender, d.Species[255].GenderRatio);
        Assert.Equal(torchic.BaseEXP, d.Species[255].ExpYield);
        Assert.Contains(d.Notes, n => n.Contains("stride 44, the smallest where 400 of the 400 non-blank", StringComparison.Ordinal));
        Assert.Contains(d.Notes, n => n.Contains("types +0x20/+0x21, catch rate +0x22, EXP yield +0x24, gender +0x26, growth +0x27", StringComparison.Ordinal));
    }

    [Fact]
    public void BlankEntriesDoNotMakeAMultipleOfTheStrideWin()
    {
        var r = GbaHackData.Read(Rebalanced(entries: 900, blank: i => i < 44 && i % 2 == 1));
        Assert.True(r.Ok, r.Refusal);
        Assert.Contains(r.Data!.Notes, n => n.Contains("read at stride 44,", StringComparison.Ordinal));
        var ivysaur = PersonalTable.E[2];
        Assert.Equal([ivysaur.HP, ivysaur.ATK, ivysaur.DEF, ivysaur.SPE, ivysaur.SPA, ivysaur.SPD], r.Data.Species[2].BaseStats);
    }

    [Fact]
    public void TheOldUnownGapStillReadsAtTheVanillaStride()
    {
        var r = GbaHackData.Read(Rebalanced(stride: 28, blank: i => i is >= 252 and <= 276));
        Assert.True(r.Ok, r.Refusal);
        Assert.Contains(r.Data!.Notes, n => n.Contains("read at stride 28, the smallest where 375 of the 375 non-blank", StringComparison.Ordinal));
        Assert.Contains(r.Data.Notes, n => n.Contains("types +0x6/+0x7, catch rate +0x8", StringComparison.Ordinal));
    }

    [Fact]
    public void FieldsThatAgreeWithNothingGiveAPartialSpeciesTableNotFilledWithZero()
    {
        var r = GbaHackData.Read(Rebalanced(scramble: true));
        Assert.True(r.Ok, r.Refusal);
        var species = r.Data!.Table(HackTableStatus.Species)!;
        Assert.Equal((HackTableStatus.Partial, false), (species.Status, species.Aligned));
        Assert.Contains("no byte agrees with vanilla Emerald", species.Reason);
        Assert.Contains("best per field, 60% needed: type 1", species.Reason);
        Assert.Contains("catch rate +0x", species.Reason);
        Assert.False(r.Data.SpeciesOverlayable);
        Assert.Empty(r.Data.Species[255].Types);
    }

    public static TheoryData<string> Numberings => ["emerald", "no-mystery", "type-none", "swapped"];

    private static Func<byte, byte>? Numbering(string name) => name switch
    {
        "no-mystery" => t => t,
        "type-none" => t => (byte)(Gen3LayoutFinder.RawType(t) + 1),
        "swapped" => t => t switch { 0 => 12, 11 => 0, _ => Gen3LayoutFinder.RawType(t) },
        _ => null,
    };

    [Theory]
    [MemberData(nameof(Numberings))]
    public void TypesInAnyOneToOneNumberingAreLearnedAndReadAsEmeraldIds(string numbering)
    {
        var r = GbaHackData.Read(Rebalanced(typeCode: Numbering(numbering)));
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        for (var i = 1; i <= 386; i++)
        {
            var p = PersonalTable.StockE[i];
            Assert.Equal((p.Type1, p.Type2), ((byte)GameTypes.ToPkhex(3, d.Species[i].Types[0])!, (byte)GameTypes.ToPkhex(3, d.Species[i].Types[1])!));
        }
        Assert.Contains(d.Notes, n => n.Contains("types +0x20/+0x21", StringComparison.Ordinal));
        Assert.Equal(numbering != "emerald", d.Notes.Any(n => n.Contains("numbered differently", StringComparison.Ordinal)));
    }

    [Fact]
    public void AConstantColumnIsNotATypeNumbering()
    {
        var rom = Rebalanced();
        var rng = new Random(9);
        for (var i = 1; i <= 400; i++)
        {
            rom[Info + (i * Stride) + 0x20] = (byte)rng.Next(256);
            rom[Info + (i * Stride) + 0x21] = (byte)rng.Next(256);
            rom[Info + (i * Stride) + 0x2A] = 0;
            rom[Info + (i * Stride) + 0x2B] = 0;
        }
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal(HackTableStatus.Partial, r.Data!.StatusOf(HackTableStatus.Species));
        Assert.Contains("vanilla Emerald's type 1, type 2 over", r.Data.Table(HackTableStatus.Species)!.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadGenderIsLeftUnreadAndTheNoteNamesTheFieldAndTheAlignmentCounts()
    {
        var rom = Rebalanced();
        for (var i = 1; i <= 400; i++)
            rom[Info + (i * Stride) + 0x26] = 3;
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Species));
        Assert.False(d.Species[255].GenderRead);
        var note = d.Notes.Single(n => n.Contains("vanilla Emerald's gender (60% needed)", StringComparison.Ordinal));
        Assert.Contains("type 1 +0x20 386/386 (Emerald's ids)", note, StringComparison.Ordinal);
        Assert.Contains("base stats exactly Emerald's 385/386", note, StringComparison.Ordinal);
        Assert.Contains("best entry shift 0 with 385 exact", note, StringComparison.Ordinal);
    }

    [Fact]
    public void ColumnsTheHackChangedAreLeftUnreadNotFilledWithZero()
    {
        var rom = Rebalanced();
        for (var i = 1; i <= 400; i++)
        {
            rom[Info + (i * Stride) + 0x22] = 0;
            rom[Info + (i * Stride) + 0x24] = 0;
            rom[Info + (i * Stride) + 0x27] = 0;
        }
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        var torchic = PersonalTable.E[255];
        Assert.Null(d.Species[255].CatchRate);
        Assert.Null(d.Species[255].ExpYield);
        Assert.Null(d.Species[255].Growth);
        Assert.Equal(torchic.Gender, d.Species[255].GenderRatio);
        Assert.Equal([Gen3LayoutFinder.RawType(torchic.Type1), Gen3LayoutFinder.RawType(torchic.Type2)], d.Species[255].Types);
        Assert.Contains(d.Notes, n => n.Contains("vanilla Emerald's catch rate, EXP yield, growth (60% needed), so the hack changed or moved them: they are left unread, never filled with 0", StringComparison.Ordinal));
        Assert.Contains(d.Notes, n => n.Contains("catch rate unread, EXP yield unread, gender +0x26, growth unread", StringComparison.Ordinal));
        var back = HackDataCache.FromJson(HackDataCache.ToJson(d), d.RomSha1);
        Assert.NotNull(back);
        Assert.Null(back!.Species[255].CatchRate);
        Assert.Null(back.Species[255].ExpYield);
        Assert.Null(back.Species[255].Growth);
        Assert.Equal(d.Species[255].GenderRatio, back.Species[255].GenderRatio);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(2)]
    public void ATableOffByWholeEntriesIsPartialAndNotOverlaidWithTheCatchColumnsUnread(int shift)
    {
        var rom = Rebalanced();
        var table = rom.AsSpan(Info, 402 * Stride).ToArray();
        for (var i = 1; i <= 400; i++)
        {
            var from = i + shift;
            if (from is >= 0 and <= 401)
                table.AsSpan(from * Stride, Stride).CopyTo(rom.AsSpan(Info + (i * Stride)));
            rom[Info + (i * Stride) + 0x22] = 0;
            rom[Info + (i * Stride) + 0x24] = 0;
            rom[Info + (i * Stride) + 0x27] = 0;
        }
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal((HackTableStatus.Partial, false), (r.Data!.Table(HackTableStatus.Species)!.Status, r.Data.Table(HackTableStatus.Species)!.Aligned));
        Assert.False(r.Data.SpeciesOverlayable);
    }

    [Fact]
    public void BaseStatsThatLineUpWithAnotherEntryArePartialEvenWhenTypesAgree()
    {
        var rom = Rebalanced();
        var table = rom.AsSpan(Info, 402 * Stride).ToArray();
        for (var i = 1; i <= 400; i++)
            table.AsSpan((i + 1) * Stride, 6).CopyTo(rom.AsSpan(Info + (i * Stride)));
        var r = GbaHackData.Read(rom);
        Assert.True(r.Ok, r.Refusal);
        var species = r.Data!.Table(HackTableStatus.Species)!;
        Assert.Equal((HackTableStatus.Partial, false), (species.Status, species.Aligned));
        Assert.True(species.Reason!.Contains("best -1 entries away", StringComparison.Ordinal), species.Reason);
    }

    [Fact]
    public void MoveNamesBehindAPointerTableAreRead()
    {
        var r = GbaHackData.Read(Rebalanced(movePointers: true));
        Assert.True(r.Ok, r.Refusal);
        Assert.Equal("Pound", r.Data!.Moves[1].Name);
        Assert.Equal(1, r.Data.Moves[1].National);
    }

    [Fact]
    public void RandomBytesHaveNoStride()
    {
        var bytes = new byte[0x80000];
        new Random(3).NextBytes(bytes);
        Assert.Null(Gen3LayoutFinder.FindStatStride(new GbaImage(bytes), Info));
    }

    [Fact]
    public void TheVanillaPathIsUnchangedWhenBulbasaurIsVanilla()
    {
        var d = GbaHackData.Read(HackDataTests.Image()).Data!;
        Assert.DoesNotContain(d.Notes, n => n.Contains("plausible base stats", StringComparison.Ordinal));
    }

    [Fact]
    public void TooFewMappedSpeciesFindNoFields()
    {
        var rom = new GbaImage(Rebalanced());
        var fields = Gen3LayoutFinder.FindFields(rom, Info, Stride, Enumerable.Repeat<int?>(null, 401).ToList());
        Assert.Null(fields.Type1);
        Assert.Equal(0, fields.Compared);
    }
}
