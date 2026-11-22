using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Cfru;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Saves;

public sealed class CfruSaveTests
{
    private const int PartyRecordSize = 100;

    internal static byte[] PlainRecord(ushort species, byte level, uint pid = 0x1234_5679, uint otId = 0x0001_0002)
    {
        var r = new byte[PartyRecordSize];
        BinaryPrimitives.WriteUInt32LittleEndian(r, pid);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(4), otId);
        r.AsSpan(8, 10).Fill(0xFF);
        r[8] = 0xBB;
        r[0x12] = 2;
        r[0x13] = 2;
        r.AsSpan(0x14, 7).Fill(0xFF);
        r[0x14] = 0xBC;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x20), species);
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x22), 13);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x24), 1000);
        r[0x29] = 70;
        r[0x2A] = 4;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x2C), 33);
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x2E), 45);
        r[0x34] = 35;
        r[0x35] = 40;
        r[0x38] = 4;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x48), 31 | (20u << 5) | (10u << 10));
        r[0x54] = level;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x56), 20);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x58 + (i * 2)), (ushort)(i == 0 ? 20 : 12));
        return r;
    }

    internal static byte[] WithParty(byte[] file, uint signature, uint money, params byte[][] records)
    {
        var c = GbaSectorContainer.TryOpen(file, SectionLayout.Cfru, ChecksumPolicy.FromLayout(SectionLayout.Cfru), [signature], out var why) ?? throw new InvalidOperationException(why);
        var edits = new List<BlockEdit>();
        var count = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(count, (uint)records.Length);
        edits.Add(new BlockEdit(SaveBlock.Large, 0x34, count));
        for (var i = 0; i < records.Length; i++)
            edits.Add(new BlockEdit(SaveBlock.Large, 0x38 + (i * PartyRecordSize), records[i]));
        var m = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(m, money ^ BinaryPrimitives.ReadUInt32LittleEndian(c.Block(SaveBlock.Small).AsSpan(0xF20)));
        edits.Add(new BlockEdit(SaveBlock.Large, 0x290, m));
        return Gen3ContainerTests.Patch(c, edits).Bytes;
    }

    private static byte[] Unbound(int seed = 11) =>
        WithParty(Gen3ContainerTests.Cfru(CfruVariant.UnboundSignature, seed), CfruVariant.UnboundSignature, 123456, PlainRecord(1022, 30), PlainRecord(252, 5, pid: 0x0BAD_F00D));

    private static CfruSave Open(byte[] file, WriteEvidence evidence = WriteEvidence.Unproven)
    {
        var s = CfruSave.TryOpen(file, evidence, out var why);
        Assert.True(s is not null, why);
        return s!;
    }

    [Fact]
    public void UnboundOpensByItsSignatureAndReadsTheParty()
    {
        var s = Open(Unbound());
        Assert.Equal(CfruVariant.Unbound.Id, s.Variant);
        Assert.Equal(2, s.PartyCount);
        Assert.Equal((ushort)1022, s.Party[0]!.Species);
        Assert.Equal((byte)30, s.Party[0]!.Level);
        Assert.Equal((ushort)252, s.Party[1]!.Species);
        Assert.Null(s.Party[2]);
        Assert.Equal(123456u, s.Money);
        Assert.Null(s.BoxesReason);
        Assert.Equal(SectionLayout.Cfru.Name + " section sizes", s.PolicyName);
    }

    [Fact]
    public void RadicalRedNeedsTheCfruWindowsAndAVanillaSaveIsNotCfru()
    {
        var rr = WithParty(Gen3ContainerTests.Cfru(GbaSectorContainer.RetailSignature, 12), GbaSectorContainer.RetailSignature, 0, PlainRecord(25, 9));
        Assert.Null(CfruSave.TryOpen(rr, WriteEvidence.Unproven, out _));
        var s = CfruSave.TryOpen(rr, WriteEvidence.Unproven, out var rrWhy, CfruVariant.RadicalRed.Id)!;
        Assert.True(s is not null, rrWhy);
        Assert.Equal(CfruVariant.RadicalRed.Id, s!.Variant);
        Assert.Null(s.Money);
        Assert.Null(CfruSave.TryOpen(TestSaves.Gen3(GameVersion.FR), WriteEvidence.Unproven, out var why, CfruVariant.RadicalRed.Id));
        Assert.Contains("vanilla vanilla:FRLG", why);
        Assert.Null(CfruSave.TryOpen(TestSaves.Gen3(GameVersion.E), WriteEvidence.Unproven, out why, CfruVariant.RadicalRed.Id));
        Assert.Contains("checksums as a vanilla", why);
        Assert.Null(CfruSave.TryOpen(Unbound(), WriteEvidence.Unproven, out _, CfruVariant.RadicalRed.Id));
    }

    [Fact]
    public void TheDesignUnboundWindowIsTheSecondCandidate()
    {
        var policy = CfruSave.CandidatePolicies[1];
        var file = Gen3ContainerTests.Cfru(CfruVariant.UnboundSignature, 13);
        for (var sector = 0; sector < 28; sector++)
        {
            var span = file.AsSpan(sector * SectionLayout.SectorSize, SectionLayout.SectorSize);
            var id = BinaryPrimitives.ReadUInt16LittleEndian(span[GbaSectorContainer.IdOffset..]);
            BinaryPrimitives.WriteUInt16LittleEndian(span[GbaSectorContainer.ChecksumOffset..], policy.Compute(id, span));
        }
        var s = Open(file);
        Assert.Equal(policy.Name, s.PolicyName);
    }

    [Fact]
    public void NoEditPlansTheSameBytes()
    {
        var file = Unbound();
        var r = Open(file).Plan();
        Assert.Equal(file, r.Bytes);
        Assert.Empty(r.DiffRanges);
    }

    [Fact]
    public void APartyEditIsConfinedAndReadsBack()
    {
        var file = Unbound();
        var s = Open(file);
        var mon = s.Party[0]! with
        {
            Moves = [33, 45, 89, 0],
            Pp = [35, 40, 10, 0],
            Evs = new StatSet(4, 252, 0, 0, 0, 252),
            Ivs = new StatSet(31, 31, 31, 31, 31, 31),
            HeldItem = 1,
            Party = s.Party[0]!.Party! with { Level = 50 },
        };
        s.SetPartySlot(0, mon);
        var r = s.Plan();
        Assert.True(r.Confined, string.Join(", ", r.DiffRanges));
        Assert.Equal([1], r.TouchedSections);
        var back = Open(r.Bytes);
        var got = back.Party[0]!;
        Assert.Equal(new ushort[] { 33, 45, 89, 0 }, got.Moves);
        Assert.Equal(new StatSet(4, 252, 0, 0, 0, 252), got.Evs);
        Assert.Equal(new StatSet(31, 31, 31, 31, 31, 31), got.Ivs);
        Assert.Equal((byte)50, got.Level);
        Assert.Equal((ushort)1, got.HeldItem);
        Assert.Equal(s.Party[1]!.Raw.ToArray(), back.Party[1]!.Raw.ToArray());
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(got.Raw.Span[0x1C..]));
    }

    [Fact]
    public void MoneyIsWrittenWhereUnboundKeepsIt()
    {
        var s = Open(Unbound());
        s.SetMoney(999999);
        var r = s.Plan();
        Assert.True(r.Confined);
        Assert.Equal(999999u, Open(r.Bytes).Money);
        var rr = CfruSave.TryOpen(WithParty(Gen3ContainerTests.Cfru(GbaSectorContainer.RetailSignature, 14), GbaSectorContainer.RetailSignature, 0, PlainRecord(25, 9)), WriteEvidence.Unproven, out _, CfruVariant.RadicalRed.Id)!;
        Assert.Throws<FaithfulWriteException>(() => rr.SetMoney(1));
    }

    [Fact]
    public void FieldsTheEncoderCannotWriteAreRefused()
    {
        var s = Open(Unbound());
        var mon = s.Party[0]!;
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Pid = mon.Pid + 1 }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Nickname = "ABCDEFGHIJK" }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Ivs = new StatSet(32, 0, 0, 0, 0, 0) }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Party = mon.Party! with { Level = 0 } }));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.SetPartySlot(3, mon));
        Assert.Equal(Unbound(), s.Plan().Bytes);
    }

    [Fact]
    public void MoneyIsStoredUnderTheSecurityKey()
    {
        var file = Unbound();
        var s = Open(file);
        var c = s.Container;
        var key = BinaryPrimitives.ReadUInt32LittleEndian(c.Block(SaveBlock.Small).AsSpan(0xF20));
        Assert.NotEqual(0u, key);
        Assert.Equal(123456u ^ key, BinaryPrimitives.ReadUInt32LittleEndian(c.Block(SaveBlock.Large).AsSpan(0x290)));
        s.SetMoney(42);
        var back = Open(s.Plan().Bytes);
        Assert.Equal(42u, back.Money);
        Assert.Equal(42u ^ key, BinaryPrimitives.ReadUInt32LittleEndian(back.Container.Block(SaveBlock.Large).AsSpan(0x290)));
    }

    [Fact]
    public void ANewerSlotWithTheRetailSignatureIsStillUnbound()
    {
        var file = Gen3ContainerTests.Cfru(CfruVariant.UnboundSignature, 15, counterA: 4, counterB: 5);
        for (var sector = SectionLayout.SectionCount; sector < 2 * SectionLayout.SectionCount; sector++)
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan((sector * SectionLayout.SectorSize) + GbaSectorContainer.SignatureOffset), GbaSectorContainer.RetailSignature);
        var s = Open(file);
        Assert.Equal(1, s.Container.ActiveSlot);
        Assert.Equal(CfruVariant.Unbound.Id, s.Variant);
    }

    [Fact]
    public void AnImpossiblePartyCountOrSlotOpensReadOnly()
    {
        var bad = Unbound();
        var c = Open(bad).Container;
        var r = FaithfulWriter.Patch(c, [new BlockEdit(SaveBlock.Large, 0x34, [7])], [new BlockWindow(SaveBlock.Large, 0x34, 1)]);
        var s = Open(r.Bytes, WriteEvidence.GameAccepted);
        Assert.Equal(0, s.PartyCount);
        Assert.Contains("party count byte is 7", s.ReadOnlyReason);
        var odd = Open(WithParty(Gen3ContainerTests.Cfru(CfruVariant.UnboundSignature, 16, counterA: 9, counterB: 4), CfruVariant.UnboundSignature, 1, PlainRecord(25, 9)), WriteEvidence.GameAccepted);
        Assert.Equal(0, odd.Container.ActiveSlot);
        Assert.Contains("not the one the game loads", odd.ReadOnlyReason);
    }

    [Fact]
    public void ALevelOnlyEditIsRefused()
    {
        var s = Open(Unbound());
        var mon = s.Party[0]!;
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Level = 60 }));
    }

    [Fact]
    public void AnUnchangedMonEncodesToItsOwnBytes()
    {
        var mon = Open(Unbound()).Party[1]!;
        Assert.Equal(mon.Raw.ToArray(), Pk3CfruPlainEncoder.Encode(mon));
    }

    [Fact]
    public void WritesWaitForGameAccepted()
    {
        var file = Unbound();
        var locked = Open(file, WriteEvidence.Confined);
        Assert.True(locked.ReadOnly);
        Assert.Throws<FaithfulWriteException>(() => locked.Write());
        var open = Open(file, WriteEvidence.GameAccepted);
        Assert.False(open.ReadOnly);
        Assert.Equal(file, open.Write().Bytes);
    }
    internal static byte[] CompactRecord(ushort species, uint pid = 0x1234_5679)
    {
        var r = new byte[58];
        BinaryPrimitives.WriteUInt32LittleEndian(r, pid);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(4), 0x0001_0002);
        r.AsSpan(8, 10).Fill(0xFF);
        r[8] = 0xBB;
        r[0x12] = 2;
        r[0x13] = 2;
        r.AsSpan(0x14, 7).Fill(0xFF);
        r[0x14] = 0xBC;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x1C), species);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x20), 1000);
        r[0x25] = 70;
        r[0x26] = 4;
        r[0x27] = 33;
        r[0x2C] = 6;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x36), 31 | (20u << 5));
        return r;
    }

    private const int BoxBytes = 30 * 58;

    internal static byte[] WithBoxes(byte[] file, uint signature, IEnumerable<(int Box, int Slot, byte[] Record)> mons, bool unbound)
    {
        var c = GbaSectorContainer.TryOpen(file, SectionLayout.Cfru, ChecksumPolicy.FromLayout(SectionLayout.Cfru), [signature], out var why) ?? throw new InvalidOperationException(why);
        var storage = new byte[19 * BoxBytes];
        var sectors = new byte[3 * BoxBytes];
        var sb1 = new byte[2 * BoxBytes];
        var sb2 = new byte[BoxBytes];
        foreach (var (box, slot, record) in mons)
        {
            var within = slot * 58;
            var (area, at) = box <= 19 ? (storage, (box - 1) * BoxBytes) : box <= 22 ? (sectors, (box - 20) * BoxBytes) : box <= 24 ? (sb1, (box - 23) * BoxBytes) : (sb2, 0);
            record.CopyTo(area, at + within);
        }
        var edits = new List<BlockEdit> { new(SaveBlock.Storage, 4, storage) };
        if (unbound)
        {
            edits.Add(new BlockEdit(SaveBlock.Large, 0x1F08, sb1));
            edits.Add(new BlockEdit(SaveBlock.Small, 0xB0, sb2));
        }
        var raw = new List<(int Offset, byte[] Bytes)>();
        for (var o = 0; o < sectors.Length; o++)
        {
            var at = 0xB0C + o;
            raw.Add(((at < 0xFF0 ? (30 * 0x1000) + at : (31 * 0x1000) + (at - 0xFF0)), [sectors[o]]));
        }
        var bytes = edits.Count > 0 ? Gen3ContainerTests.Patch(c, edits).Bytes : file.ToArray();
        foreach (var (offset, b) in raw)
            bytes[offset] = b[0];
        return bytes;
    }

    [Fact]
    public void UnboundBoxesAreReadFromTheStreamSectors30And31AndBothSaveBlocks()
    {
        var file = WithBoxes(Unbound(), CfruVariant.UnboundSignature,
        [
            (1, 0, CompactRecord(1)),
            (19, 29, CompactRecord(19)),
            (20, 0, CompactRecord(20)),
            (21, 7, CompactRecord(21)),
            (22, 29, CompactRecord(22)),
            (23, 3, CompactRecord(23)),
            (24, 29, CompactRecord(24)),
            (25, 12, CompactRecord(25)),
        ], unbound: true);
        var s = Open(file);
        Assert.Null(s.BoxesReason);
        Assert.Equal(25, s.Boxes.Count);
        Assert.Equal(Enumerable.Range(1, 25), s.Boxes.Select(b => b.Number));
        (int Box, int Slot)[] placed = [(1, 0), (19, 29), (20, 0), (21, 7), (22, 29), (23, 3), (24, 29), (25, 12)];
        foreach (var (box, slot) in placed)
            Assert.Equal((ushort)box, s.Boxes[box - 1].Slots[slot]!.Species);
        Assert.Equal(placed.Length, s.Boxes.Sum(b => b.Slots.Count(m => m is not null)));
        Assert.All(s.Boxes, b => Assert.Equal(0, b.Undecodable));
        Assert.Contains("measured on Unbound 2.1.1.1", s.Boxes[22].Source);
        Assert.Equal((byte)70, s.Boxes[0].Slots[0]!.Friendship);
        Assert.Equal((ushort)33, s.Boxes[0].Slots[0]!.Moves[0]);
        Assert.Equal(6, s.Boxes[0].Slots[0]!.Evs.Hp);
    }

    [Fact]
    public void RadicalRedHasTwentyTwoBoxesAndAGarbageRecordIsCountedNotShown()
    {
        var rr = WithParty(Gen3ContainerTests.Cfru(GbaSectorContainer.RetailSignature, 5), GbaSectorContainer.RetailSignature, 0, PlainRecord(25, 9));
        var junk = CompactRecord(7);
        junk[0x12] = 0xEE;
        var file = WithBoxes(rr, GbaSectorContainer.RetailSignature, [(2, 1, CompactRecord(2)), (22, 0, CompactRecord(22)), (3, 4, junk)], unbound: false);
        var s = CfruSave.TryOpen(file, WriteEvidence.Unproven, out var why, CfruVariant.RadicalRed.Id);
        Assert.True(s is not null, why);
        Assert.Equal(22, s!.Boxes.Count);
        Assert.Equal((ushort)2, s.Boxes[1].Slots[1]!.Species);
        Assert.Equal((ushort)22, s.Boxes[21].Slots[0]!.Species);
        Assert.Null(s.Boxes[2].Slots[4]);
        Assert.Equal(1, s.Boxes[2].Undecodable);
    }

    [Fact]
    public void BoxRecordsAtHandComputedFileOffsetsAreRead()
    {
        var file = WithBoxes(Unbound(), CfruVariant.UnboundSignature, [], unbound: true);
        var c = GbaSectorContainer.TryOpen(file, SectionLayout.Cfru, ChecksumPolicy.FromLayout(SectionLayout.Cfru), [CfruVariant.UnboundSignature], out var why) ?? throw new InvalidOperationException(why);
        CompactRecord(101).CopyTo(file, c.SectorOffset(5) + 4);
        CompactRecord(119).CopyTo(file, c.SectorOffset(13) + 0x16E);
        CompactRecord(120).CopyTo(file, 0x1EB0C);
        CompactRecord(122).CopyTo(file, 0x1FF46);
        var policy = ChecksumPolicy.FromLayout(SectionLayout.Cfru);
        foreach (var id in new[] { 5, 13 })
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(c.SectorOffset(id) + GbaSectorContainer.ChecksumOffset), policy.Compute(id, file.AsSpan(c.SectorOffset(id), 0x1000)));
        var s = CfruSave.TryOpen(file, WriteEvidence.Unproven, out why, CfruVariant.Unbound.Id);
        Assert.True(s is not null, why);
        Assert.Equal((ushort)101, s!.Boxes[0].Slots[0]!.Species);
        Assert.Equal((ushort)119, s.Boxes[18].Slots[29]!.Species);
        Assert.Equal((ushort)120, s.Boxes[19].Slots[0]!.Species);
        Assert.Equal((ushort)122, s.Boxes[21].Slots[29]!.Species);
    }

    [Fact]
    public void BlankFlashIsEmptyAndARefusedRecordKeepsItsReason()
    {
        var junk = CompactRecord(7);
        junk[0x12] = 0xEE;
        var file = WithBoxes(Unbound(), CfruVariant.UnboundSignature, [(3, 4, junk)], unbound: true);
        file.AsSpan(0x1EB0C, 0xFF0 - 0xB0C).Fill(0xFF);
        file.AsSpan(0x1F000, 0x1F70 - 0xFF0).Fill(0xFF);
        var s = Open(file);
        Assert.All(s.Boxes.Skip(19).Take(3), b => Assert.Equal((0, 0), (b.Undecodable, b.Slots.Count(m => m is not null))));
        Assert.Equal([4], s.Boxes[2].Refused.Keys);
        Assert.Contains("language", s.Boxes[2].Refused[4]);
    }

    private static HackMon CompactMon(ushort species = 252) =>
        RecordCodecs.For(RecordCodecId.Pk3CfruCompact58).Decode(CompactRecord(species), out var why) ?? throw new InvalidOperationException(why);

    [Fact]
    public void ACompactBoxRecordEncodesUnchangedByteForByte()
    {
        var mon = CompactMon();
        Assert.Equal(mon.Raw.ToArray(), Pk3CfruCompact58Encoder.Encode(mon));
    }

    [Fact]
    public void ACompactBoxEditDecodesBackAndTouchesOnlyItsBytes()
    {
        var mon = CompactMon();
        var edited = mon with
        {
            Species = 1022,
            HeldItem = 300,
            Exp = 123456,
            PpUps = [3, 0, 2, 1],
            Friendship = 200,
            Ball = 11,
            Moves = [1023, 89, 0, 512],
            Evs = new StatSet(252, 1, 2, 3, 4, 5),
            Ivs = new StatSet(31, 30, 29, 28, 27, 26),
            HiddenAbility = true,
        };
        var bytes = Pk3CfruCompact58Encoder.Encode(edited);
        var back = RecordCodecs.For(RecordCodecId.Pk3CfruCompact58).Decode(bytes, out var why);
        Assert.True(back is not null, why);
        Assert.Equal((ushort)1022, back!.Species);
        Assert.Equal((ushort)300, back.HeldItem);
        Assert.Equal(123456u, back.Exp);
        Assert.Equal(new byte[] { 3, 0, 2, 1 }, back.PpUps);
        Assert.Equal((byte?)200, back.Friendship);
        Assert.Equal((byte?)11, back.Ball);
        Assert.Equal(new ushort[] { 1023, 89, 0, 512 }, back.Moves);
        Assert.Equal(new StatSet(252, 1, 2, 3, 4, 5), back.Evs);
        Assert.Equal(new StatSet(31, 30, 29, 28, 27, 26), back.Ivs);
        Assert.True(back.HiddenAbility);
        var was = mon.Raw.ToArray();
        Assert.Empty(Enumerable.Range(0, 58).Where(i => bytes[i] != was[i] && i is < 0x1C or (>= 0x32 and < 0x36)));
    }

    [Fact]
    public void ACompactBoxEditRefusesWhatTheRecordCannotHold()
    {
        var mon = CompactMon();
        Assert.Throws<RecordEncodeException>(() => Pk3CfruCompact58Encoder.Encode(mon with { Moves = [1024, 0, 0, 0] }));
        Assert.Throws<RecordEncodeException>(() => Pk3CfruCompact58Encoder.Encode(mon with { Species = 0 }));
        Assert.Throws<RecordEncodeException>(() => Pk3CfruCompact58Encoder.Encode(mon with { Pid = 1 }));
        Assert.Throws<RecordEncodeException>(() => Pk3CfruCompact58Encoder.Encode(mon with { Nickname = "ABCDEFGHIJK" }));
        Assert.Throws<RecordEncodeException>(() => Pk3CfruCompact58Encoder.Encode(mon with { Codec = new RecordCodecKey(RecordCodecId.Pk3CfruPlain, null) }));
    }

    private static readonly byte[] Moved = [89, 0, 0, 0];

    [Theory]
    [InlineData(1, 0, 5, 0x5004, 58, -1, 0)]
    [InlineData(19, 29, 13, 0xD16E, 58, -1, 0)]
    [InlineData(20, 21, -1, 0x1EFCE, 34, 0x1F000, 24)]
    [InlineData(22, 29, -1, 0x1FF46, 58, -1, 0)]
    [InlineData(23, 3, -2, 0, 58, -1, 0)]
    [InlineData(25, 12, -3, 0, 58, -1, 0)]
    public void ABoxEditLandsOnlyOnItsRecordAndReadsBack(int box, int slot, int section, int fileAt, int fileLength, int secondAt, int secondLength)
    {
        var file = WithBoxes(Unbound(), CfruVariant.UnboundSignature, [(box, slot, CompactRecord(77)), (2, 2, CompactRecord(2)), (24, 0, CompactRecord(24))], unbound: true);
        var s = Open(file);
        var c = s.Container;
        var block = section == -2 ? SaveBlock.Large : SaveBlock.Small;
        var blockAt = (section == -2 ? 0x1F08 : 0xB0) + (slot * 58);
        var inBlock = section is -2 or -3 ? Enumerable.Range(blockAt, 58).Select(o => (At: Locate(c, block, o), Id: Owner(c, block, o))).ToList() : [];
        var mon = s.Boxes[box - 1].Slots[slot]!;
        s.SetBoxSlot(box, slot, mon with { Species = 1022, Moves = [33, 89, 1023, 0], Exp = 54321, Evs = new StatSet(252, 0, 0, 0, 0, 6) });
        var r = s.Plan();
        Assert.True(r.Confined, string.Join(", ", r.Outside));
        var expected = section switch
        {
            -2 or -3 => inBlock.Select(b => new ByteRange(b.At, 1)).ToList(),
            >= 0 => [new ByteRange(c.SectorOffset(section) + (fileAt & 0xFFF), fileLength)],
            _ => new List<ByteRange> { new(fileAt, fileLength) },
        };
        if (secondAt >= 0)
            expected.Add(new ByteRange(secondAt, secondLength));
        Assert.Equal(section == -1 ? (secondAt >= 0 ? [30, 31] : [31]) : Array.Empty<int>(), r.TouchedRawSectors);
        var touched = section switch
        {
            -2 or -3 => inBlock.Select(b => b.Id).Distinct().Order().ToArray(),
            >= 0 => [section],
            _ => Array.Empty<int>(),
        };
        Assert.Equal(touched, r.TouchedSections);
        foreach (var id in touched)
            expected.Add(new ByteRange(c.SectorOffset(id) + GbaSectorContainer.ChecksumOffset, 2));
        Assert.All(r.DiffRanges.SelectMany(d => Enumerable.Range(d.Start, d.Length)), at => Assert.True(expected.Any(e => e.Contains(at)), $"0x{at:X5} is outside {string.Join(", ", expected)}"));
        Assert.Equal(file.AsSpan(0x1EFF0, 0x10).ToArray(), r.Bytes.AsSpan(0x1EFF0, 0x10).ToArray());
        Assert.Equal(file.AsSpan(0x1FFF0, 0x10).ToArray(), r.Bytes.AsSpan(0x1FFF0, 0x10).ToArray());
        var back = Open(r.Bytes);
        var got = back.Boxes[box - 1].Slots[slot]!;
        Assert.Equal((ushort)1022, got.Species);
        Assert.Equal(new ushort[] { 33, 89, 1023, 0 }, got.Moves);
        Assert.Equal(54321u, got.Exp);
        Assert.Equal(new StatSet(252, 0, 0, 0, 0, 6), got.Evs);
        Assert.Equal(mon.Pid, got.Pid);
        Assert.Equal((ushort)2, back.Boxes[1].Slots[2]!.Species);
        Assert.Equal((ushort)24, back.Boxes[23].Slots[0]!.Species);
        Assert.Equal(s.Party.Select(p => p?.Raw.ToArray()), back.Party.Select(p => p?.Raw.ToArray()));
        Assert.Equal((ushort)1022, s.Boxes[box - 1].Slots[slot]!.Species);
    }

    private static int Locate(GbaSectorContainer c, SaveBlock block, int offset) =>
        c.TryLocate(block, offset, out var at, out _) ? at : throw new InvalidOperationException($"{block} 0x{offset:X}");

    private static int Owner(GbaSectorContainer c, SaveBlock block, int offset) =>
        c.TryLocate(block, offset, out _, out var id) ? id : throw new InvalidOperationException($"{block} 0x{offset:X}");

    [Fact]
    public void BoxAndPartyEditsTogetherStayConfined()
    {
        var s = Open(WithBoxes(Unbound(), CfruVariant.UnboundSignature, [(21, 5, CompactRecord(5)), (3, 0, CompactRecord(3))], unbound: true));
        s.SetBoxSlot(21, 5, s.Boxes[20].Slots[5]! with { HeldItem = 7 });
        s.SetBoxSlot(3, 0, s.Boxes[2].Slots[0]! with { Friendship = 255 });
        s.SetPartySlot(0, s.Party[0]! with { HeldItem = 1 });
        s.SetBoxSlot(3, 0, s.Boxes[2].Slots[0]! with { Ball = 2 });
        var r = s.Plan();
        Assert.True(r.Confined, string.Join(", ", r.Outside));
        var back = Open(r.Bytes);
        Assert.Equal((ushort)7, back.Boxes[20].Slots[5]!.HeldItem);
        Assert.Equal((byte?)255, back.Boxes[2].Slots[0]!.Friendship);
        Assert.Equal((byte?)2, back.Boxes[2].Slots[0]!.Ball);
        Assert.Equal((ushort)1, back.Party[0]!.HeldItem);
    }

    [Fact]
    public void BoxWritesRefuseWhatTheyCannotPlace()
    {
        var file = WithBoxes(Unbound(), CfruVariant.UnboundSignature, [(1, 0, CompactRecord(1)), (1, 1, CompactRecord(2, pid: 0x2222_3333))], unbound: true);
        var junk = CompactRecord(7);
        junk[0x12] = 0xEE;
        var c = Open(file).Container;
        junk.CopyTo(file, Locate(c, SaveBlock.Storage, 4 + (2 * 58)));
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(c.SectorOffset(5) + GbaSectorContainer.ChecksumOffset), ChecksumPolicy.FromLayout(SectionLayout.Cfru).Compute(5, file.AsSpan(c.SectorOffset(5), 0x1000)));
        var s = Open(file);
        var first = s.Boxes[0].Slots[0]!;
        Assert.Throws<ArgumentOutOfRangeException>(() => s.SetBoxSlot(26, 0, first));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.SetBoxSlot(0, 0, first));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.SetBoxSlot(1, 30, first));
        Assert.Contains("empty", Assert.Throws<FaithfulWriteException>(() => s.SetBoxSlot(1, 3, first)).Message);
        Assert.Contains("refuses", Assert.Throws<FaithfulWriteException>(() => s.SetBoxSlot(1, 2, first)).Message);
        Assert.Contains("does not start from the bytes", Assert.Throws<FaithfulWriteException>(() => s.SetBoxSlot(1, 1, first)).Message);
        Assert.Throws<RecordEncodeException>(() => s.SetBoxSlot(1, 0, first with { Moves = [1024, 0, 0, 0] }));
        Assert.Equal(file, s.Plan().Bytes);
        var rr = WithBoxes(WithParty(Gen3ContainerTests.Cfru(GbaSectorContainer.RetailSignature, 5), GbaSectorContainer.RetailSignature, 0, PlainRecord(25, 9)), GbaSectorContainer.RetailSignature, [(1, 0, CompactRecord(1)), (20, 0, CompactRecord(20))], unbound: false);
        var r = CfruSave.TryOpen(rr, WriteEvidence.Unproven, out var why, CfruVariant.RadicalRed.Id) ?? throw new InvalidOperationException(why);
        Assert.Contains("not written until", Assert.Throws<FaithfulWriteException>(() => r.SetBoxSlot(1, 0, r.Boxes[0].Slots[0]!)).Message);
        Assert.Contains("not written until", Assert.Throws<FaithfulWriteException>(() => r.SetBoxSlot(20, 0, r.Boxes[19].Slots[0]!)).Message);
    }

    [Fact]
    public void RawEditsStayOutOfTheSaveSlotsAndInsideTheSectorPayload()
    {
        var c = Open(Unbound()).Container;
        Assert.Throws<FaithfulWriteException>(() => FaithfulWriter.Patch(c, [], [], [new RawEdit(27, 0, [1])]));
        Assert.Throws<FaithfulWriteException>(() => FaithfulWriter.Patch(c, [], [], [new RawEdit(28, 0, [1])]));
        Assert.Throws<FaithfulWriteException>(() => FaithfulWriter.Patch(c, [], [], [new RawEdit(29, 0, [1])]));
        Assert.Throws<FaithfulWriteException>(() => FaithfulWriter.Patch(c, [], [], [new RawEdit(32, 0, [1])]));
        Assert.Throws<FaithfulWriteException>(() => FaithfulWriter.Patch(c, [], [], [new RawEdit(30, 0xFEF, [1, 2])]));
        var r = FaithfulWriter.Patch(c, [], [], [new RawEdit(31, 0xFEF, [0x5A])]);
        Assert.Equal([new ByteRange(0x1FFEF, 1)], r.DiffRanges);
        Assert.Equal([31], r.TouchedRawSectors);
        Assert.Empty(r.TouchedSections);
        Assert.True(r.Confined);
    }

    [Fact]
    public void SavesWithoutABoxWriterRefuseBoxWrites()
    {
        var s = Open(Unbound());
        HackSave plain = new NoBoxes(s);
        Assert.Contains("box slots are not written", Assert.Throws<FaithfulWriteException>(() => plain.SetBoxSlot(1, 0, s.Party[0]!)).Message);
    }

    private sealed class NoBoxes(CfruSave inner) : HackSave(inner.Variant, inner.Original, WriteEvidence.Unproven)
    {
        public override IReadOnlyList<HackMon?> Party => inner.Party;
        public override HackTrainer Trainer => inner.Trainer;
        public override uint? Money => inner.Money;
        public override string? BoxesReason => null;
        public override void SetPartySlot(int slot, HackMon mon) => inner.SetPartySlot(slot, mon);
        public override void SetMoney(uint money) => inner.SetMoney(money);
        public override WriteResult Plan() => inner.Plan();
    }

    private static byte[] Rich(ushort species)
    {
        var r = CompactRecord(species);
        r[0x13] = 6;
        r[0x1A] = 0x11;
        r[0x1B] = 0x22;
        r[0x32] = 0xA1;
        r[0x33] = 0xA2;
        r[0x34] = 0xA3;
        r[0x35] = 0xA4;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x36), 31 | (20u << 5) | (1u << 30) | (1u << 31));
        return r;
    }

    [Fact]
    public void RepeatedBoxEditsKeepTheBitsTheEncoderDoesNotOwn()
    {
        (int Box, int Slot)[] at = [(5, 20), (20, 21), (20, 22), (25, 29)];
        var s = Open(WithBoxes(Unbound(), CfruVariant.UnboundSignature, at.Select(p => (p.Box, p.Slot, Rich((ushort)p.Box))), unbound: true));
        foreach (var (box, slot) in at)
        {
            s.SetBoxSlot(box, slot, s.Boxes[box - 1].Slots[slot]! with { Species = 500, Ivs = new StatSet(1, 2, 3, 4, 5, 6) });
            s.SetBoxSlot(box, slot, s.Boxes[box - 1].Slots[slot]! with { HeldItem = 99 });
        }
        var r = s.Plan();
        Assert.True(r.Confined, string.Join(", ", r.Outside));
        Assert.Contains(6, r.TouchedSections);
        Assert.Contains(7, r.TouchedSections);
        var back = Open(r.Bytes);
        foreach (var (box, slot) in at)
        {
            var got = back.Boxes[box - 1].Slots[slot]!;
            Assert.Equal(((ushort)500, (ushort)99, true, (bool?)true), (got.Species, got.HeldItem, got.IsEgg, got.HiddenAbility));
            Assert.Equal(new StatSet(1, 2, 3, 4, 5, 6), got.Ivs);
            var raw = got.Raw.ToArray();
            Assert.Equal(new byte[] { 0x11, 0x22 }, raw[0x1A..0x1C]);
            Assert.Equal(new byte[] { 0xA1, 0xA2, 0xA3, 0xA4 }, raw[0x32..0x36]);
            Assert.Equal(6, raw[0x13]);
        }
        back.SetBoxSlot(20, 22, back.Boxes[19].Slots[22]! with { IsEgg = false });
        var hatched = Open(back.Plan().Bytes).Boxes[19].Slots[22]!;
        Assert.False(hatched.IsEgg);
        Assert.Equal(2, hatched.Raw.Span[0x13]);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(hatched.Raw.Span[0x36..]) & (1u << 30));
    }
}
