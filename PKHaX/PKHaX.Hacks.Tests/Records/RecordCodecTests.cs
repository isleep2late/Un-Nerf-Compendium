using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Records;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Records;

public class RecordCodecTests
{
    private static readonly string[] Ids =
        ["pk1", "pk2", "pk3", "pk3-cfru-plain", "pk3-cfru-compact58", "pk3x", "pk3-soulgold96", "pk4", "pk5", "pk6", "pk7", "pb7", "pb8"];

    public static string? FixtureRoot()
    {
        var root = Environment.GetEnvironmentVariable("PKHAX_RECORD_FIXTURES")
            ?? "/home/gyarados/AI/Games/is2l/bundle-2026-10-12/fixtures/record-codecs";
        return Directory.Exists(root) && Directory.EnumerateFiles(root, "vectors*.json").Any() ? root : null;
    }

    [Fact]
    public void IdsAreTheDesignEnumWithNoAliases()
    {
        Assert.Equal(Ids, RecordCodecIds.All);
        foreach (var id in Ids)
        {
            Assert.True(RecordCodecs.TryGet(id, id == "pk3x" ? "1.15" : null, out var codec, out var why), why);
            Assert.Equal(id, codec.Key.Id.Name());
        }
        foreach (var alias in new[] { "PK3", "gen3", "cfru-plain", "pk3-cfru", "expansion-packed/1.9", "soulgold-96", "pk8", "" })
            Assert.False(RecordCodecs.TryGet(alias, null, out _, out _), alias);
    }

    [Theory]
    [InlineData("1.9", "1.9")]
    [InlineData("1.9.2", "1.9")]
    [InlineData("1.9.4", "1.9")]
    [InlineData("1.15", "1.15")]
    [InlineData("1.15.2", "1.15")]
    public void ExpansionVersionsNormalise(string version, string normalized)
    {
        Assert.True(RecordCodecs.TryGet("pk3x", version, out var codec, out var why), why);
        Assert.Equal(normalized, codec.Key.Version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.8")]
    [InlineData("1.16")]
    [InlineData("2.0")]
    [InlineData("v1.9")]
    public void ExpansionVersionsOutsideTheMeasuredLayoutAreRefused(string? version)
    {
        Assert.False(RecordCodecs.TryGet("pk3x", version, out _, out var why));
        Assert.False(string.IsNullOrEmpty(why));
    }

    [Fact]
    public void AVersionOnACodecThatTakesNoneIsRefused() => Assert.False(RecordCodecs.TryGet("pk3", "1.15", out _, out _));

    [Fact]
    public void Pk3DecodesWhatPkhexEncrypts()
    {
        var pk = new PK3
        {
            Species = (ushort)Species.Treecko, PID = 0x8B21_C3D7, TID16 = 12345, SID16 = 54321, EXP = 1059860,
            HeldItem = 13, Ball = 4, Move1 = 1, Move2 = 33, Move3 = 71, Move4 = 98, Move1_PP = 35, Move2_PP = 35,
            Move3_PP = 25, Move4_PP = 30, IV_HP = 31, IV_ATK = 1, IV_DEF = 2, IV_SPA = 3, IV_SPD = 4, IV_SPE = 5,
            EV_HP = 10, EV_ATK = 20, EV_DEF = 30, EV_SPA = 40, EV_SPD = 50, EV_SPE = 60, OriginalTrainerFriendship = 70,
            Language = (int)LanguageID.English, Nickname = "TREECKO", OriginalTrainerName = "PKHAX",
        };
        pk.Stat_Level = 100;
        pk.ResetPartyStats();
        var party = new byte[pk.SIZE_PARTY];
        pk.WriteEncryptedDataParty(party);
        var mon = RecordCodecs.For("pk3").Decode(party, out var why);
        Assert.NotNull(mon);
        Assert.Null(why);
        Assert.Equal(SpeciesConverter.GetInternal3((ushort)Species.Treecko), mon.Species);
        Assert.Equal((ushort)Species.Treecko, mon.National);
        Assert.Equal(pk.PID, mon.Pid);
        Assert.Equal(pk.TID16, mon.Tid16);
        Assert.Equal(pk.SID16, mon.Sid16);
        Assert.Equal(pk.EXP, mon.Exp);
        Assert.Equal((byte)100, mon.Level);
        Assert.Equal(new ushort[] { 1, 33, 71, 98 }, mon.Moves);
        Assert.Equal(new byte[] { 35, 35, 25, 30 }, mon.Pp);
        Assert.Equal(new StatSet(31, 1, 2, 3, 4, 5), mon.Ivs);
        Assert.Equal(new StatSet(10, 20, 30, 40, 50, 60), mon.Evs);
        Assert.Equal((byte)(pk.PID % 25), mon.Nature);
        Assert.Equal((ushort)13, mon.HeldItem);
        Assert.Equal((byte)4, mon.Ball);
        Assert.Equal((byte)70, mon.Friendship);
        Assert.Equal("TREECKO", mon.Nickname);
        Assert.Equal("PKHAX", mon.OtName);
        Assert.Equal(pk.Stat_HPMax, mon.Party!.Stats.Hp);
        Assert.Equal(pk.Stat_SPE, mon.Party.Stats.Spe);

        var stored = new byte[pk.SIZE_STORED];
        pk.WriteEncryptedDataStored(stored);
        var box = RecordCodecs.For("pk3").Decode(stored, out _);
        Assert.NotNull(box);
        Assert.Equal(RecordShape.Stored, box.Shape);
        Assert.Null(box.Party);
        Assert.Null(box.Level);

        stored[0x30] ^= 0x10;
        Assert.Null(RecordCodecs.For("pk3").Decode(stored, out var bad));
        Assert.Contains("checksum", bad);
    }

    [Fact]
    public void Pk3KeepsHackIdsAbove411Raw()
    {
        var pk = new PK3 { PID = 0x1234_5678, TID16 = 1, Species = (ushort)Species.Bulbasaur };
        var data = new byte[pk.SIZE_STORED];
        pk.WriteDecryptedDataStored(data);
        Span<byte> plain = stackalloc byte[48];
        data.AsSpan(0x20, 48).CopyTo(plain);
        BitConverter.GetBytes((ushort)1200).CopyTo(plain);
        Gen3TestCrypt.WriteEncrypted(data, plain);
        var mon = RecordCodecs.For("pk3").Decode(data, out var why);
        Assert.NotNull(mon);
        Assert.Equal((ushort)1200, mon.Species);
        Assert.Null(mon.National);
    }

    [Fact]
    public void GbCodecsDecodeWhatPkhexHolds()
    {
        var pk1 = new PK1 { Species = (ushort)Species.Pikachu, CatchRate = 163, EXP = 1000, Move1 = 84, Move2 = 45, Move1_PP = 30, Move2_PP = 40, IV_ATK = 15, IV_DEF = 14, IV_SPE = 13, IV_SPC = 12, EV_HP = 500, TID16 = 777 };
        pk1.Stat_Level = 10;
        pk1.ResetPartyStats();
        var m1 = RecordCodecs.For("pk1").Decode(pk1.Data[..44], out var why1);
        Assert.NotNull(m1);
        Assert.Null(why1);
        Assert.Equal((ushort)Species.Pikachu, m1.National);
        Assert.Equal(SpeciesConverter.GetInternal1((ushort)Species.Pikachu), m1.Species);
        Assert.Equal(new StatSet(pk1.IV_HP, 15, 14, 12, 12, 13), m1.Ivs);
        Assert.Equal(500, m1.Evs.Hp);
        Assert.Equal((uint)1000, m1.Exp);
        Assert.Equal((ushort)777, m1.Tid16);
        Assert.Equal((byte)163, m1.CatchRate);
        Assert.Equal((ushort)0, m1.HeldItem);
        Assert.Equal(pk1.Stat_HPMax, m1.Party!.Stats.Hp);

        var pk2 = new PK2 { Species = (ushort)Species.Chikorita, EXP = 2000, HeldItem = 3, Move1 = 33, Move1_PP = 35, IV_ATK = 10, IV_DEF = 10, IV_SPE = 10, IV_SPC = 10, OriginalTrainerFriendship = 99, TID16 = 4321 };
        pk2.Stat_Level = 11;
        pk2.ResetPartyStats();
        var m2 = RecordCodecs.For("pk2").Decode(pk2.Data[..48], out var why2);
        Assert.NotNull(m2);
        Assert.Null(why2);
        Assert.Equal((ushort)Species.Chikorita, m2.Species);
        Assert.Equal((byte)99, m2.Friendship);
        Assert.Equal((ushort)3, m2.HeldItem);
        Assert.Equal(true, m2.IsShiny);
        Assert.Equal(pk2.Stat_HPMax, m2.Party!.Stats.Hp);
    }

    [Theory]
    [InlineData("pk4")]
    [InlineData("pk5")]
    [InlineData("pk6")]
    [InlineData("pk7")]
    [InlineData("pb7")]
    [InlineData("pb8")]
    public void ModernCodecsRoundThroughPkhex(string id)
    {
        PKM pk = id switch
        {
            "pk4" => new PK4(),
            "pk5" => new PK5(),
            "pk6" => new PK6(),
            "pk7" => new PK7(),
            "pb7" => new PB7(),
            _ => new PB8(),
        };
        pk.Species = (ushort)Species.Eevee;
        pk.PID = 0xCAFE_BABE;
        pk.EncryptionConstant = 0x1357_9BDF;
        pk.TID16 = 11;
        pk.SID16 = 22;
        pk.EXP = 4000;
        pk.Move1 = 33;
        pk.IV_SPE = 31;
        pk.Language = (int)LanguageID.English;
        pk.Nickname = "EEVEE";
        pk.RefreshChecksum();
        var codec = RecordCodecs.For(id);
        var stored = new byte[pk.SIZE_STORED];
        pk.WriteEncryptedDataStored(stored);
        var mon = codec.Decode(stored, out var why);
        Assert.NotNull(mon);
        Assert.Null(why);
        Assert.Equal((ushort)Species.Eevee, mon.Species);
        Assert.Equal(0xCAFE_BABEu, mon.Pid);
        Assert.Equal(31, mon.Ivs!.Value.Spe);
        Assert.Equal("EEVEE", mon.Nickname);
        stored[0x40] ^= 0x01;
        Assert.Null(codec.Decode(stored, out var bad));
        Assert.NotNull(bad);
    }

    [Fact]
    public void WrongLengthsAndEmptySlotsAreRefused()
    {
        foreach (var id in Ids)
        {
            var codec = RecordCodecs.For(id, id == "pk3x" ? "1.15" : null);
            Assert.Null(codec.Decode(new byte[7], out var why));
            Assert.Contains("bytes", why);
            var size = codec.StoredSize ?? codec.PartySize!.Value;
            Assert.Null(codec.Decode(new byte[size], out var empty));
            Assert.Contains("empty", empty);
        }
    }

    public static IEnumerable<object[]> FixtureCodecs() => Ids.Select(id => new object[] { id });

    [FixtureTheory]
    [MemberData(nameof(FixtureCodecs))]
    public void FixtureVectorsDecodeAsExpected(string id)
    {
        var root = FixtureRoot();
        Assert.NotNull(root);
        var vectors = RecordVectors.Load(root).Where(v => v.Codec == id).ToList();
        var failures = vectors.Select(v => RecordVectors.Check(root, v)).Where(r => !r.Passed)
            .Select(r => $"{r.Vector.File}: {string.Join("; ", r.Failures)}").ToList();
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}

public sealed class FixtureTheoryAttribute : TheoryAttribute
{
    public FixtureTheoryAttribute()
    {
        if (RecordCodecTests.FixtureRoot() is null)
            Skip = "PKHAX_RECORD_FIXTURES not present";
    }
}

internal static class Gen3TestCrypt
{
    private static readonly string[] Orders =
    [
        "GAEM", "GAME", "GEAM", "GEMA", "GMAE", "GMEA", "AGEM", "AGME", "AEGM", "AEMG", "AMGE", "AMEG",
        "EGAM", "EGMA", "EAGM", "EAMG", "EMGA", "EMAG", "MGAE", "MGEA", "MAGE", "MAEG", "MEGA", "MEAG",
    ];

    public static void WriteEncrypted(byte[] record, ReadOnlySpan<byte> gaem)
    {
        var pid = BitConverter.ToUInt32(record, 0);
        var key = pid ^ BitConverter.ToUInt32(record, 4);
        var order = Orders[pid % 24];
        Span<byte> shuffled = stackalloc byte[48];
        for (var slot = 0; slot < 4; slot++)
            gaem.Slice("GAEM".IndexOf(order[slot]) * 12, 12).CopyTo(shuffled.Slice(slot * 12, 12));
        ushort sum = 0;
        for (var i = 0; i < 48; i += 2)
            sum += BitConverter.ToUInt16(shuffled[i..]);
        BitConverter.GetBytes(sum).CopyTo(record, 0x1C);
        for (var i = 0; i < 48; i += 4)
            BitConverter.GetBytes(BitConverter.ToUInt32(shuffled[i..]) ^ key).CopyTo(record, 0x20 + i);
    }
}
