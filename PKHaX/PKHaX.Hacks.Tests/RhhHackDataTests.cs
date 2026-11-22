using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class RhhHackDataTests
{
    private const int SpeciesInfo = 0x20000;
    private const int Moves = 0x40000;
    private const int Items = 0x50000;
    private const int Abilities = 0x60000;
    private const int LongNames = 0x70000;

    private static void Name(byte[] rom, int at, string text, int length)
    {
        rom.AsSpan(at, length).Fill(0xFF);
        StringConverter3.SetString(rom.AsSpan(at, length), text, length - 1, (int)LanguageID.English);
    }

    private static void Ptr(byte[] rom, int at, int offset) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), (uint)(0x08000000 + offset));

    private static void U16(byte[] rom, int at, int value) => BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(at), (ushort)value);

    internal static byte[] Image(int speciesStride = 0xC4, int nameOffset = 0x1F, int moveStride = 0x24, bool movePointers = false, int itemStride = 0x34, int itemNameOffset = 0x10, bool bulbasaurStats = true, string[]? itemNames = null)
    {
        var rom = new byte[0x80000];
        System.Text.Encoding.ASCII.GetBytes("BPEE").CopyTo(rom, 0xAC);
        rom[0xB2] = 0x96;
        Ptr(rom, 0x100 + 0xBC, SpeciesInfo);
        Ptr(rom, 0x100 + 0xC8, Items);
        Ptr(rom, 0x100 + 0xCC, Moves);

        string[] species = ["??????????", "Bulbasaur", "Ivysaur", "Venusaur", "Mr. Mime", "Nidoran♀", "Zorbleflax"];
        for (var i = 0; i < species.Length; i++)
            Name(rom, SpeciesInfo + i * speciesStride + nameOffset, species[i], RhhTables.SpeciesNameLength);
        if (bulbasaurStats)
        {
            byte[] bulba = [45, 49, 49, 45, 65, 65, 12, 3, 45];
            bulba.CopyTo(rom, SpeciesInfo + speciesStride);
        }

        string[] moves = ["-", "Pound", "Karate Chop", "Double Slap", "Fire Punch"];
        for (var i = 0; i < moves.Length; i++)
        {
            if (movePointers)
            {
                Name(rom, LongNames + i * 0x20, moves[i], 0x20);
                Ptr(rom, Moves + i * moveStride + 4, LongNames + i * 0x20);
            }
            else
            {
                Name(rom, Moves + i * moveStride, moves[i], 17);
            }
        }

        var items = itemNames ?? ["????????", "Master Ball", "Ultra Ball", "Great Ball", "Zorble Stone"];
        for (var i = 0; i < items.Length; i++)
            Name(rom, Items + i * itemStride + itemNameOffset, items[i], 20);

        string[] abilities = ["-------", "Stench", "Drizzle", "Speed Boost", "Battle Armor"];
        for (var i = 0; i < abilities.Length; i++)
            Name(rom, Abilities + i * 0x24, abilities[i], 17);

        var h = 0x400;
        "RHHEXP"u8.CopyTo(rom.AsSpan(h));
        rom[h + 6] = 1;
        rom[h + 7] = 9;
        U16(rom, h + 0x0A, moves.Length);
        U16(rom, h + 0x0C, species.Length);
        U16(rom, h + 0x0E, abilities.Length);
        Ptr(rom, h + 0x10, Abilities);
        U16(rom, h + 0x14, items.Length);
        rom[h + 0x16] = 20;
        return rom;
    }

    [Fact]
    public void ExpansionTablesAreFoundByTheirStrideAndNameOffset()
    {
        var r = GbaHackData.Read(Image());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(GbaHackData.ReaderRhh, d.Reader);
        Assert.Contains(d.Notes, n => n.Contains("1.9.0") && n.Contains("stride 196") && n.Contains("+0x1F"));
        Assert.Equal(7, d.Species.Count);
        Assert.Equal("Bulbasaur", d.Species[1].Name);
        Assert.Equal([45, 49, 49, 45, 65, 65], d.Species[1].BaseStats);
        Assert.Equal([12, 3], d.Species[1].Types);
        Assert.Equal(45, d.Species[1].CatchRate);
        Assert.Equal([1, 2, 3, (int)Species.MrMime, (int)Species.NidoranF], d.Species.Skip(1).Take(5).Select(s => s.National ?? 0));
        Assert.Null(d.Species[6].National);
        Assert.Equal([1, 2, 3, 7], d.Moves.Skip(1).Select(m => m.National ?? 0));
        Assert.Equal([1, 2, 3], d.Items.Skip(1).Take(3).Select(i => i.National ?? 0));
        Assert.Null(d.Items[4].National);
        Assert.Equal([1, 2, 3, 4], d.Abilities.Skip(1).Select(a => a.National ?? 0));
    }

    [Fact]
    public void ReorderedBallsStillFindTheItemTable()
    {
        var d = GbaHackData.Read(Image(itemNames: ["????????", "Poke Ball", "Great Ball", "Ultra Ball", "Master Ball", "Premier Ball", "Zorble Stone"])).Data!;
        Assert.Equal(7, d.Items.Count);
        Assert.Contains(d.Notes, n => n.StartsWith("item stride 52, name at +0x10"));
        Assert.Equal("Master Ball", d.Items[4].Name);
        Assert.Equal([4, 3, 2, 1, 12], d.Items.Skip(1).Take(5).Select(i => i.National ?? 0));
    }

    [Fact]
    public void AnItemTableWithoutBothBallsInTheWindowIsNotGuessed()
    {
        var d = GbaHackData.Read(Image(itemNames: ["????????", "Strange Ball", "Poke Ball", "Great Ball", "Heal Ball", "Net Ball", "Dive Ball", "Nest Ball", "Repeat Ball", "Ultra Ball", "Master Ball"])).Data!;
        Assert.Empty(d.Items);
        Assert.Contains(d.Notes, n => n.Contains("Master Ball and Ultra Ball among entries 1 to 8"));
    }

    [Fact]
    public void OtherStridesAndPointerMoveNamesAreFoundToo()
    {
        var d = GbaHackData.Read(Image(speciesStride: 0xA8, nameOffset: 0x20, moveStride: 0x10, movePointers: true, itemStride: 0x2C, itemNameOffset: 0)).Data!;
        Assert.Contains(d.Notes, n => n.Contains("stride 168") && n.Contains("+0x20"));
        Assert.Contains(d.Notes, n => n.StartsWith("move stride 16, name pointer at +0x4"));
        Assert.Equal("Double Slap", d.Moves[3].Name);
        Assert.Equal("Ultra Ball", d.Items[2].Name);
    }

    [Fact]
    public void ExpansionImagesWithoutTheAnchorsAreRefused()
    {
        var noStats = GbaHackData.Read(Image(bulbasaurStats: false));
        Assert.False(noStats.Ok);
        Assert.Contains("45/49/49/45/65/65", noStats.Refusal);
        var rom = Image();
        Name(rom, SpeciesInfo + 2 * 0xC4 + 0x1F, "Ivysore", RhhTables.SpeciesNameLength);
        Assert.Contains("Bulbasaur, Ivysaur and Venusaur", GbaHackData.Read(rom).Refusal);
        var disabled = Image();
        U16(disabled, 0x40C, 9);
        var dd = GbaHackData.Read(disabled).Data!;
        Assert.Contains(dd.Notes, n => n.StartsWith("species 7-8 have no name"));
        Assert.Equal(9, dd.Species.Count);
        var tooMany = Image();
        U16(tooMany, 0x40C, 7 + RhhTables.DisabledTail + 1);
        Assert.Contains("more than 64 short of", GbaHackData.Read(tooMany).Refusal);
        var noCounts = Image();
        U16(noCounts, 0x40C, 0);
        Assert.Contains("outside 1 to", GbaHackData.Read(noCounts).Refusal);
    }

    internal const int ManySpecies = 300;

    internal static byte[] ManySpeciesImage(Func<int, string>? rename = null, bool zeroStats = false, int typeRowOffset = 0, bool emeraldTypes = false)
    {
        var rom = Image();
        var stride = 0xC4;
        var names = GameInfo.GetStrings("en").specieslist;
        U16(rom, 0x40C, ManySpecies + 1);
        for (var i = 1; i <= ManySpecies; i++)
        {
            var b = SpeciesInfo + (i * stride);
            rom.AsSpan(b, stride).Clear();
            Name(rom, b + 0x1F, rename?.Invoke(i) ?? names[i], RhhTables.SpeciesNameLength);
            var p = PersonalTable.USUM[i];
            if (!zeroStats)
            {
                byte[] stats = [(byte)(p.HP + 1), (byte)(p.ATK + 1), (byte)p.DEF, (byte)p.SPE, (byte)p.SPA, (byte)p.SPD];
                stats.CopyTo(rom, b);
            }
            var typeRow = Math.Clamp(i + typeRowOffset, 1, ManySpecies);
            IPersonalType t = emeraldTypes ? PersonalTable.StockE[typeRow] : PersonalTable.USUM[typeRow];
            rom[b + 0x06] = Gen3LayoutFinder.RawType(t.Type1);
            rom[b + 0x07] = Gen3LayoutFinder.RawType(t.Type2);
            rom[b + 0x08] = p.CatchRate;
        }
        return rom;
    }

    [Fact]
    public void AnExpansionImageWithoutBulbasaursStatsReadsOnPlausibleStatsAndAlignedTypes()
    {
        var r = GbaHackData.Read(ManySpeciesImage());
        Assert.True(r.Ok, r.Refusal);
        var d = r.Data!;
        Assert.Equal(ManySpecies + 1, d.Species.Count);
        Assert.Equal((HackTableStatus.Read, (bool?)true), (d.StatusOf(HackTableStatus.Species), d.Table(HackTableStatus.Species)!.Aligned));
        var charizard = PersonalTable.USUM[6];
        Assert.Equal([Gen3LayoutFinder.RawType(charizard.Type1), Gen3LayoutFinder.RawType(charizard.Type2)], d.Species[6].Types);
        Assert.Contains(d.Notes, n => n.Contains("have plausible base stats at +0, so the table is read", StringComparison.Ordinal));
    }

    [Fact]
    public void ABuildThatKeepsTheOldTypingsStillLinesUp()
    {
        var d = GbaHackData.Read(ManySpeciesImage(emeraldTypes: true)).Data!;
        Assert.Equal((HackTableStatus.Read, (bool?)true), (d.StatusOf(HackTableStatus.Species), d.Table(HackTableStatus.Species)!.Aligned));
        var clefairy = PersonalTable.StockE[35];
        Assert.Equal([Gen3LayoutFinder.RawType(clefairy.Type1), Gen3LayoutFinder.RawType(clefairy.Type2)], d.Species[35].Types);
        Assert.Contains(d.Notes, n => n.Contains("vanilla Emerald's types for species 1-386", StringComparison.Ordinal));
        var newest = GbaHackData.Read(ManySpeciesImage()).Data!;
        Assert.Contains(newest.Notes, n => n.Contains("found by agreement with PKHeX's newest types", StringComparison.Ordinal));
    }

    [Fact]
    public void ZeroStatsWithoutBulbasaurAreStillRefused()
    {
        var r = GbaHackData.Read(ManySpeciesImage(zeroStats: true));
        Assert.False(r.Ok);
        Assert.Contains("45/49/49/45/65/65", r.Refusal);
        Assert.Contains("have plausible base stats at +0", r.Refusal);
    }

    [Fact]
    public void TypesOneRowOffTheNamesGiveAPartialTable()
    {
        var d = GbaHackData.Read(ManySpeciesImage(typeRowOffset: 1)).Data!;
        Assert.Equal((HackTableStatus.Partial, (bool?)false), (d.StatusOf(HackTableStatus.Species), d.Table(HackTableStatus.Species)!.Aligned));
        Assert.False(d.SpeciesOverlayable);
    }

    [Fact]
    public void SpeciesNamesWithoutTheFirstThreeAreFoundByTheVote()
    {
        var names = GameInfo.GetStrings("en").specieslist;
        var r = GbaHackData.Read(ManySpeciesImage(rename: i => i <= 3 ? "" : i % 8 == 0 ? $"Zorb{i}" : names[i]));
        Assert.True(r.Ok, r.Refusal);
        Assert.Contains(r.Data!.Notes, n => n.Contains("species info at stride 196, name inline at +0x1F, found by a vote", StringComparison.Ordinal));
        var tooFew = GbaHackData.Read(ManySpeciesImage(rename: i => i <= 3 || i > 39 ? $"Zorb{i}" : names[i]));
        Assert.False(tooFew.Ok);
        Assert.Contains("no stride and name place gives at least 40", tooFew.Refusal);
    }

    [Fact]
    public void TheExpansionReadCarriesEngineStatusAndUnreadGender()
    {
        var d = GbaHackData.Read(Image()).Data!;
        Assert.Equal((HackTables.Gba, Gen3Engine.Expansion), (d.Platform, d.Engine));
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Species));
        Assert.Equal(HackTableStatus.Partial, d.StatusOf(HackTableStatus.Moves));
        Assert.Equal(HackTableStatus.Read, d.StatusOf(HackTableStatus.Items));
        Assert.All(d.Species, s => Assert.False(s.GenderRead));
        Assert.Equal(HackTableKey.Of(d), d.TableKey);
    }
}
