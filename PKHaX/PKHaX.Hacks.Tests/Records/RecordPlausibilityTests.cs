using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PKHaX.Hacks.Records;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Records;

public class RecordPlausibilityTests
{
    private static readonly string[] Ids =
        ["pk1", "pk2", "pk3", "pk3-cfru-plain", "pk3-cfru-compact58", "pk3x", "pk3-soulgold96", "pk4", "pk5", "pk6", "pk7", "pb7", "pb8"];

    public static IEnumerable<object[]> PlaintextShapes() =>
    [
        ["pk3-cfru-plain", 100],
        ["pk3-cfru-compact58", 58],
        ["pk3-soulgold96", 96],
        ["pk3-soulgold96", 76],
        ["pk1", 44],
        ["pk1", 33],
        ["pk2", 48],
        ["pk2", 32],
    ];

    [Theory]
    [MemberData(nameof(PlaintextShapes))]
    public void PlaintextCodecsDecodeFewerThanOnePercentOfRandomBuffers(string id, int size)
    {
        const int count = 20000;
        var codec = RecordCodecs.For(id);
        var rng = new Random(20261014);
        var buffer = new byte[size];
        var accepted = 0;
        for (var i = 0; i < count; i++)
        {
            rng.NextBytes(buffer);
            if (codec.Decode(buffer, out _) is not null)
                accepted++;
        }
        Assert.True(accepted * 100 < count, $"{id} {size}: {accepted} of {count} random buffers decoded");
    }

    [Fact]
    public void AllFfBuffersAreRefusedByEveryCodec()
    {
        foreach (var id in Ids)
        {
            var codec = RecordCodecs.For(id, id == "pk3x" ? "1.15" : null);
            foreach (var size in new[] { codec.PartySize, codec.StoredSize }.OfType<int>().Distinct())
            {
                Assert.Null(codec.Decode(Enumerable.Repeat((byte)0xFF, size).ToArray(), out var why));
                Assert.False(string.IsNullOrEmpty(why), $"{id} {size}");
            }
        }
    }

    private static byte[] CfruPlain(ushort national = (ushort)Species.Bulbasaur, uint pid = 0x1234_5679)
    {
        var pk = new PK3
        {
            Species = national, PID = pid, TID16 = 12345, SID16 = 54321, EXP = 135, Move1 = 33, Move2 = 45, Move1_PP = 35,
            Move2_PP = 40, IV_HP = 10, IV_ATK = 20, IV_SPE = 30, OriginalTrainerFriendship = 70, Language = (int)LanguageID.English,
            Nickname = "BULBASAUR", OriginalTrainerName = "PKHAX",
        };
        pk.Stat_Level = 5;
        pk.ResetPartyStats();
        var data = new byte[pk.SIZE_PARTY];
        pk.WriteDecryptedDataParty(data);
        data[0x13] = 2;
        data[0x1C] = 0;
        data[0x1D] = 0;
        return data;
    }

    private static byte[] Compact58(ReadOnlySpan<byte> party)
    {
        var compact = new byte[58];
        party[..0x1C].CopyTo(compact);
        party[0x20..0x2B].CopyTo(compact.AsSpan(0x1C));
        ulong packed = 0;
        for (var i = 0; i < 4; i++)
            packed |= (ulong)(BinaryPrimitives.ReadUInt16LittleEndian(party[(0x2C + (i * 2))..]) & 0x3FF) << (10 * i);
        for (var i = 0; i < 5; i++)
            compact[0x27 + i] = (byte)(packed >> (8 * i));
        party[0x38..0x3E].CopyTo(compact.AsSpan(0x2C));
        party[0x44..0x4C].CopyTo(compact.AsSpan(0x32));
        return compact;
    }

    private static void Refused(string id, byte[] record, string reason)
    {
        Assert.Null(RecordCodecs.For(id).Decode(record, out var why));
        Assert.NotNull(why);
        Assert.StartsWith(RecordPlausibility.Prefix, why);
        Assert.Contains(reason, why);
    }

    private static byte[] With(byte[] record, Action<byte[]> edit)
    {
        var copy = record.ToArray();
        edit(copy);
        return copy;
    }

    [Fact]
    public void CfruPlainRefusesWhatNoGameWrites()
    {
        var good = CfruPlain();
        var mon = RecordCodecs.For("pk3-cfru-plain").Decode(good, out var why);
        Assert.NotNull(mon);
        Assert.Null(why);
        Assert.Equal((ushort)1, mon.Species);
        Assert.Equal((byte)5, mon.Level);
        Assert.Equal((uint)135, mon.Exp);
        Assert.Equal((byte)70, mon.Friendship);
        Assert.Equal(new ushort[] { 33, 45, 0, 0 }, mon.Moves);

        Refused("pk3-cfru-plain", With(good, d => d[0x12] = 0), "language");
        Refused("pk3-cfru-plain", With(good, d => d[0x12] = 8), "language");
        Refused("pk3-cfru-plain", With(good, d => d[0x13] = 3), "bad-egg");
        Refused("pk3-cfru-plain", With(good, d => d[0x13] = 0), "has-species");
        Refused("pk3-cfru-plain", With(good, d => d[0x13] = 6), "egg");
        Refused("pk3-cfru-plain", With(good, d => d[0x1C] = 1), "checksum");
        Refused("pk3-cfru-plain", With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x20), 2048)), "species 2048");
        Refused("pk3-cfru-plain", With(good, d => d[0x54] = 0), "level");
        Refused("pk3-cfru-plain", With(good, d => d[0x54] = 101), "level");
        Refused("pk3-cfru-plain", With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x58), 0)), "max HP 0");
        Refused("pk3-cfru-plain", With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x56), 999)), "current HP");
        Refused("pk3-cfru-plain", With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x5A), 1000)), "stat");
        Assert.NotNull(RecordCodecs.For("pk3-cfru-plain").Decode(With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x20), 1200)), out _));
    }

    [Fact]
    public void CfruPartyWindowsShiftedByTwoOrFourBytesAreRefused()
    {
        var block = CfruPlain().Concat(CfruPlain((ushort)Species.Pikachu, 0x0BAD_F00D)).Concat(CfruPlain((ushort)Species.Mew, 0x0102_0304)).ToArray();
        var codec = RecordCodecs.For("pk3-cfru-plain");
        Assert.NotNull(codec.Decode(block.AsSpan(0, 100), out _));
        Assert.NotNull(codec.Decode(block.AsSpan(100, 100), out _));
        foreach (var shift in new[] { 2, 4, 8, 12, 0x20, 100 - 4, 100 + 4 })
            Assert.Null(codec.Decode(block.AsSpan(shift, 100), out _));
    }

    [Fact]
    public void Compact58DecodesTheBoxedFormOfAPartyRecordAndRefusesWhatNoGameWrites()
    {
        var party = CfruPlain();
        var box = Compact58(party);
        var plain = RecordCodecs.For("pk3-cfru-plain").Decode(party, out _);
        var mon = RecordCodecs.For("pk3-cfru-compact58").Decode(box, out var why);
        Assert.NotNull(plain);
        Assert.NotNull(mon);
        Assert.Null(why);
        Assert.Equal(plain.Species, mon.Species);
        Assert.Equal(plain.Exp, mon.Exp);
        Assert.Equal(plain.Moves, mon.Moves);
        Assert.Equal(plain.Ivs, mon.Ivs);
        Assert.Equal(plain.Evs, mon.Evs);
        Assert.Equal(plain.Friendship, mon.Friendship);
        Assert.Equal(plain.Ball, mon.Ball);
        Assert.Equal(plain.Nickname, mon.Nickname);
        Assert.Equal(plain.OtName, mon.OtName);
        Assert.Null(mon.Level);

        Refused("pk3-cfru-compact58", With(box, d => d[0x12] = 0), "language");
        Refused("pk3-cfru-compact58", With(box, d => d[0x13] = 1), "bad-egg");
        Refused("pk3-cfru-compact58", With(box, d => d[0x13] = 4), "has-species");
        Refused("pk3-cfru-compact58", With(box, d => d[0x13] = 6), "egg");
        Refused("pk3-cfru-compact58", With(box, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x1C), 0xFFFF)), "species 65535");
    }

    private static byte[] SoulGold96()
    {
        var d = new byte[96];
        BinaryPrimitives.WriteUInt32LittleEndian(d, 0x0000_0019);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(4), 0x1111_2222);
        d.AsSpan(0x08, 12).Fill(0xFF);
        d[0x08] = 0xBB;
        d[0x14] = 2;
        d[0x15] = 2;
        d.AsSpan(0x16, 7).Fill(0xFF);
        d[0x16] = 0xCA;
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x20), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(0x24), 135);
        d[0x2B] = 50;
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x2C), 33);
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x2E), 45);
        d[0x34] = 35;
        d[0x35] = 40;
        BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(0x48), 0x0000_7FFF);
        d[0x50] = 5;
        ushort[] stats = [20, 10, 10, 9, 12, 12];
        BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x52), 20);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x54 + (2 * i)), stats[i]);
        return d;
    }

    [Fact]
    public void SoulGold96RefusesWhatNoGameWrites()
    {
        var good = SoulGold96();
        var mon = RecordCodecs.For("pk3-soulgold96").Decode(good, out var why);
        Assert.NotNull(mon);
        Assert.Null(why);
        Assert.Equal((ushort)1, mon.Species);
        Assert.Equal((byte)5, mon.Level);
        Assert.Equal((byte)50, mon.Friendship);
        Assert.Equal((byte)0, mon.Nature);
        Assert.NotNull(RecordCodecs.For("pk3-soulgold96").Decode(good.AsSpan(0, 0x4C), out _));

        Refused("pk3-soulgold96", With(good, d => d[0x14] = 0), "language");
        Refused("pk3-soulgold96", With(good, d => d[0x15] = 3), "bad-egg");
        Refused("pk3-soulgold96", With(good, d => d[0x15] = 0), "has-species");
        Refused("pk3-soulgold96", With(good, d => d[0x15] = 6), "egg");
        Refused("pk3-soulgold96", With(good, d => { d[0] = 24; d[0x14] = 2 | (1 << 3); }), "nature 25");
        Refused("pk3-soulgold96", With(good, d => d[0x35] = 65), "PP");
        Refused("pk3-soulgold96", With(good, d => d[0x50] = 0), "level");
        Refused("pk3-soulgold96", With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x54), 0)), "max HP 0");
        Refused("pk3-soulgold96", With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x52), 21)), "current HP");
        Refused("pk3-soulgold96", With(good, d => BinaryPrimitives.WriteUInt16LittleEndian(d.AsSpan(0x56), 1000)), "stat");
        Refused("pk3-soulgold96", With(good, d => d[0x14] = 0).AsSpan(0, 0x4C).ToArray(), "language");
        var block = good.Concat(good).ToArray();
        Assert.Null(RecordCodecs.For("pk3-soulgold96").Decode(block.AsSpan(4, 96), out _));
    }

    private static byte[] Gb1Party()
    {
        var pk1 = new PK1 { Species = (ushort)Species.Pikachu, CatchRate = 163, EXP = 1000, Move1 = 84, Move1_PP = 30, TID16 = 777 };
        pk1.Stat_Level = 10;
        pk1.ResetPartyStats();
        return pk1.Data[..44].ToArray();
    }

    private static byte[] Gb2Party()
    {
        var pk2 = new PK2 { Species = (ushort)Species.Chikorita, EXP = 2000, HeldItem = 3, Move1 = 33, Move1_PP = 35, OriginalTrainerFriendship = 99, TID16 = 4321 };
        pk2.Stat_Level = 11;
        pk2.ResetPartyStats();
        return pk2.Data[..48].ToArray();
    }

    [Fact]
    public void GbCodecsRefuseWhatNoGameWrites()
    {
        var p1 = Gb1Party();
        Assert.NotNull(RecordCodecs.For("pk1").Decode(p1, out _));
        Assert.NotNull(RecordCodecs.For("pk1").Decode(p1.AsSpan(0, 33), out _));
        Refused("pk1", With(p1, d => d[0x21] = 0), "level 0");
        Refused("pk1", With(p1, d => d[4] = 0x18), "status");
        Refused("pk1", With(p1, d => { d[0x0E] = 0x13; d[0x0F] = 0x12; d[0x10] = 0xD1; }), "EXP");
        Refused("pk1", With(p1, d => { d[0x0E] = 0; d[0x0F] = 0; d[0x10] = 10; }), "below");
        Refused("pk1", With(p1, d => { d[0x22] = 0; d[0x23] = 0; }), "max HP 0");
        Refused("pk1", With(p1, d => { d[1] = 0x03; d[2] = 0xE7; }), "current HP");
        Refused("pk1", With(p1, d => { d[0x24] = 0x03; d[0x25] = 0xE8; }), "stat");
        Refused("pk1", With(p1, d => d[3] = 0).AsSpan(0, 33).ToArray(), "level 0");
        Refused("pk1", With(p1, d => { d[1] = 0x03; d[2] = 0xE8; }).AsSpan(0, 33).ToArray(), "current HP");

        var p2 = Gb2Party();
        Assert.NotNull(RecordCodecs.For("pk2").Decode(p2, out _));
        Assert.NotNull(RecordCodecs.For("pk2").Decode(p2.AsSpan(0, 32), out _));
        Refused("pk2", With(p2, d => d[0x1F] = 0), "level 0");
        Refused("pk2", With(p2, d => d[0x1C] = 0x05), "Pokerus");
        Refused("pk2", With(p2, d => d[0x20] = 0x18), "status");
        Refused("pk2", With(p2, d => { d[0x24] = 0; d[0x25] = 0; }), "max HP 0");
        Refused("pk2", With(p2, d => d[0x1C] = 0x05).AsSpan(0, 32).ToArray(), "Pokerus");
        var glitch = With(p2, d => { d[0x1F] = 255; d[0x08] = 0x13; d[0x09] = 0x12; d[0x0A] = 0xD0; d[0x1C] = 0xF4; });
        var mon = RecordCodecs.For("pk2").Decode(glitch, out var why);
        Assert.True(mon is not null, why);
        Assert.Equal((byte)255, mon.Level);
    }

    [Fact]
    public void ModernCodecsReportTheCurrentHandlersFriendship()
    {
        var pk = new PK7
        {
            Species = (ushort)Species.Eevee, PID = 0xCAFE_BABE, EncryptionConstant = 0x1357_9BDF, TID16 = 11, SID16 = 22, EXP = 4000,
            Move1 = 33, Language = (int)LanguageID.English, Nickname = "EEVEE", OriginalTrainerFriendship = 70, HandlingTrainerFriendship = 200,
        };
        pk.CurrentHandler = 1;
        pk.RefreshChecksum();
        var stored = new byte[pk.SIZE_STORED];
        pk.WriteEncryptedDataStored(stored);
        var mon = RecordCodecs.For("pk7").Decode(stored, out var why);
        Assert.True(mon is not null, why);
        Assert.Equal((byte)200, mon.Friendship);

        pk.CurrentHandler = 0;
        pk.RefreshChecksum();
        pk.WriteEncryptedDataStored(stored);
        Assert.Equal((byte)70, RecordCodecs.For("pk7").Decode(stored, out _)!.Friendship);
    }

    [Fact]
    public void UnknownOraclesAreRefusedByTheLoader()
    {
        var dir = Directory.CreateTempSubdirectory("pkhax-vectors-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "vectors.json"),
                """{"schema":"record-vectors/1","vectors":[{"codec":"pk3","file":"x.bin","oracle":"inferred","expect":{"species":1,"exp":5}}]}""");
            Assert.Throws<InvalidDataException>(() => RecordVectors.Load(dir.FullName));
            File.WriteAllText(Path.Combine(dir.FullName, "vectors.json"),
                """{"schema":"record-vectors/1","vectors":[{"codec":"pk3","file":"x.bin","oracle":"pkhex ","expect":{"species":1,"exp":5}}]}""");
            Assert.Throws<InvalidDataException>(() => RecordVectors.Load(dir.FullName));
            File.WriteAllText(Path.Combine(dir.FullName, "vectors.json"),
                """{"schema":"record-vectors/1","vectors":[{"codec":"pk3","file":"x.bin","oracle":"given","expect":{"species":1,"exp":5}}]}""");
            Assert.Single(RecordVectors.Load(dir.FullName));
        }
        finally
        {
            dir.Delete(true);
        }
    }

    [Theory]
    [InlineData("given", """{"species":1,"exp":5}""", true)]
    [InlineData("pokedaisy", """{"species":1,"ivs":[1,2,3,4,5,6]}""", true)]
    [InlineData("statcheck", """{"stats":[1,2,3,4,5,6]}""", true)]
    [InlineData("nickname", """{"species":1,"nickname":"A"}""", true)]
    [InlineData("pkhex", """{"national":1,"exp":5}""", true)]
    [InlineData("given", """{"species":1}""", false)]
    [InlineData("given", """{"species":1,"heldItem":13}""", false)]
    [InlineData("pkhex", """{"national":1,"heldItem":13}""", false)]
    [InlineData("given", """{}""", false)]
    [InlineData("observed", """{"species":1,"exp":5}""", false)]
    [InlineData("checksum", """{"species":1,"exp":5}""", false)]
    [InlineData("plant", """{"species":1,"exp":5}""", false)]
    public void KnownNeedsAWhitelistedOracleAndAFieldBeyondIdentity(string oracle, string expect, bool known)
    {
        using var doc = JsonDocument.Parse(expect);
        var v = new RecordVector("pk3", null, "x.bin", "", oracle, doc.RootElement.Clone(), null, null);
        Assert.Equal(known, v.Known);
    }
}
