using System;
using System.Linq;
using PKHaX.Hacks.Editing;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Tests.Saves;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Records;

public sealed class Pk3EncoderTests
{
    private static HackMon Decode(byte[] bytes) => RecordCodecs.For(RecordCodecId.Pk3).Decode(bytes, out var why) ?? throw new InvalidOperationException(why);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnUneditedRecordEncodesToItsOwnBytes(bool party)
    {
        var raw = RetailFixture.Record(1100, 0x1234_5678, party);
        Assert.Equal(raw, Pk3Encoder.Encode(Decode(raw)));
    }

    [Fact]
    public void TheChecksumIsRecomputedAndTheKeyKept()
    {
        var raw = RetailFixture.Record(500, 0x89AB_CDEF, true);
        var mon = Decode(raw);
        var bytes = Pk3Encoder.Encode(mon with { HeldItem = 700, Moves = [555, 0, 0, 0], Evs = mon.Evs with { Atk = 252 }, Ball = 1, AbilitySlot = 1 });
        Assert.Equal(raw.AsSpan(0, 0x1C).ToArray(), bytes.AsSpan(0, 0x1C).ToArray());
        Assert.NotEqual(raw.AsSpan(0x1C, 2).ToArray(), bytes.AsSpan(0x1C, 2).ToArray());
        var pk = new PK3(bytes.ToArray());
        Assert.True(pk.ChecksumValid);
        Assert.Equal(700, pk.HeldItem);
        Assert.Equal(555, pk.Move1);
        Assert.Equal(252, pk.EV_ATK);
        Assert.Equal(1, pk.Ball);
        Assert.True(pk.AbilityBit);
        var back = Decode(bytes);
        Assert.Equal((ushort)500, back.Species);
        Assert.Equal(mon.Party, back.Party);
    }

    [Fact]
    public void PidNatureNicknameAndBallPastFourBitsAreRefused()
    {
        var mon = Decode(RetailFixture.Record(500, 0x1234_5678, false));
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Pid = mon.Pid + 1 }));
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Nature = (byte)((mon.Nature!.Value + 1) % 25) }));
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Nickname = "OTHER" }));
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Ball = 16 }));
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Species = 0 }));
    }

    [Fact]
    public void AHeaderEggBitThatDisagreesIsKeptUnlessTheEggFlagChanges()
    {
        var raw = RetailFixture.Record(500, 0x1234_5678, false);
        raw[0x13] |= 4;
        var mon = Decode(raw);
        Assert.False(mon.IsEgg);
        Assert.Equal(raw, Pk3Encoder.Encode(mon));
        Assert.Equal(raw[0x13], Pk3Encoder.Encode(mon with { HeldItem = 3 })[0x13]);
    }

    [Fact]
    public void PartyStatusAndImplausibleStatsAreRefused()
    {
        var mon = Decode(RetailFixture.Record(500, 0x1234_5678, true));
        var party = mon.Party!;
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Party = party with { Status = party.Status ^ 8 } }));
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Party = party with { Stats = party.Stats with { Atk = RecordPlausibility.MaxStat + 1 } } }));
        Assert.Throws<RecordEncodeException>(() => Pk3Encoder.Encode(mon with { Party = party with { CurrentHp = 0, Stats = party.Stats with { Hp = 0 } } }));
    }

    [Fact]
    public void OnlyTheListedBitsChange()
    {
        var raw = RetailFixture.Record(1100, 0x0BAD_F00D, true);
        var mon = Decode(raw);
        var bytes = Pk3Encoder.Encode(mon with { HeldItem = 9 });
        var field = Pk3Encoder.Fields.Single(f => f.Field == "HeldItem");
        var before = Pk3Encoder.Plain(raw);
        var after = Pk3Encoder.Plain(bytes);
        var mask = EncodedField.Mask(before.Length, field.Bits.Concat(Pk3Encoder.Derived));
        for (var i = 0; i < before.Length; i++)
            Assert.Equal(before[i] & ~mask[i], after[i] & ~mask[i]);
    }
}
