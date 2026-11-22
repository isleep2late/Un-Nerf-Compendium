using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Records;
using PKHaX.Hacks.Saves;
using PKHaX.Hacks.Saves.Expansion;
using PKHaX.Hacks.Saves.Gen3;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Saves;

public sealed class ExpansionSaveTests
{
    private static readonly RecordCodecKey Pk3x115 = new(RecordCodecId.Pk3x, "1.15");

    private static readonly ExpansionLayout Emerald = new(0xF2C, 0x3D88, 0x234, 0x238, 0xA, 0x0, Pk3x115, MoneyOffset: 0x490, MoneyKeyOffset: 0xAC);

    private static byte[] File(bool tails = true)
    {
        var sav = (SAV3E)SaveUtil.GetSaveFile(TestSaves.Gen3(GameVersion.E))!;
        var treecko = new PK3 { Species = (ushort)Species.Treecko, PID = 0x1234_5678, TID16 = 1, SID16 = 2, Nickname = "TREECKO", OriginalTrainerName = "PKHAX", Language = 2, EXP = 135, Move1 = 1, Move1_PP = 35, Move2 = 43, Move2_PP = 30, IV_HP = 31, IV_ATK = 20, OriginalTrainerFriendship = 70, Ball = 4 };
        treecko.Stat_Level = 5;
        treecko.Stat_HPMax = 20;
        treecko.Stat_HPCurrent = 20;
        var mudkip = new PK3 { Species = (ushort)Species.Mudkip, PID = 0x0BAD_F00D, TID16 = 1, SID16 = 2, Nickname = "MUDKIP", OriginalTrainerName = "PKHAX", Language = 2, EXP = 135, Move1 = 33, Move1_PP = 35, OriginalTrainerFriendship = 70, Ball = 4 };
        mudkip.Stat_Level = 5;
        mudkip.Stat_HPMax = 20;
        mudkip.Stat_HPCurrent = 20;
        sav.SetPartySlotAtIndex(treecko, 0, EntityImportSettings.None);
        sav.SetPartySlotAtIndex(mudkip, 1, EntityImportSettings.None);
        sav.Money = 3000;
        sav.Small[..8].Fill(0xFF);
        StringConverter3.SetString(sav.Small[..8], "PKHAX", 7, (int)LanguageID.English, StringConverterOption.None);
        var file = sav.Write().ToArray();
        for (var sector = 0; sector < 14; sector++)
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan((sector * 0x1000) + 0xFFC), 2);
        if (tails)
        {
            var rng = new Random(9);
            for (var sector = 0; sector < 28; sector++)
                rng.NextBytes(file.AsSpan((sector * 0x1000) + 0xF80, 0x74));
        }
        return file;
    }

    private static ExpansionSave Open(byte[] file, WriteEvidence evidence = WriteEvidence.Unproven, ExpansionLayout? layout = null)
    {
        var s = ExpansionSave.TryOpen(file, layout ?? Emerald, evidence, out var why);
        Assert.True(s is not null, why);
        return s!;
    }

    [Fact]
    public void AnExpansionShapedSaveOpensAndDecodesItsParty()
    {
        var file = File();
        var s = Open(file);
        Assert.Equal(2, s.PartyCount);
        Assert.Equal(SpeciesConverter.GetInternal3((ushort)Species.Treecko), s.Party[0]!.Species);
        Assert.Equal(new ushort[] { 1, 43, 0, 0 }, s.Party[0]!.Moves);
        Assert.Equal(3000u, s.Money);
        Assert.Equal("PKHAX", s.Trainer.Name);
        var pkhex = (SAV3E)SaveUtil.GetSaveFile(file)!;
        Assert.Equal(pkhex.TID16, s.Trainer.Tid16);
        Assert.Equal(pkhex.SID16, s.Trainer.Sid16);
        Assert.Null(s.StructureProblem);
    }

    [Fact]
    public void NoEditPlansTheSameBytesAndTheTailsStay()
    {
        var file = File();
        var r = Open(file).Plan();
        Assert.Equal(file, r.Bytes);
    }

    [Fact]
    public void AnEditIsConfinedKeepsTheSaveBlock3TailsAndPkhexReadsTheSameMon()
    {
        var file = File();
        var s = Open(file);
        var before = s.Party[0]!;
        var mon = before with
        {
            Moves = [1, 43, 89, 0],
            Pp = [35, 30, 10, 0],
            Evs = new StatSet(4, 252, 0, 252, 0, 0),
            Ivs = new StatSet(31, 31, 31, 31, 31, 31),
            HeldItem = 13,
            Nature = (byte)((before.PidNature!.Value + 3) % 25),
            ShinyModifier = true,
            Party = before.Party! with { CurrentHp = 7 },
        };
        s.SetPartySlot(0, mon);
        s.SetMoney(999999);
        var r = s.Plan();
        Assert.True(r.Confined, string.Join(", ", r.Outside));
        for (var sector = 0; sector < 28; sector++)
            Assert.Equal(file.AsSpan((sector * 0x1000) + 0xF80, 0x74).ToArray(), r.Bytes.AsSpan((sector * 0x1000) + 0xF80, 0x74).ToArray());
        var back = Open(r.Bytes);
        var got = back.Party[0]!;
        Assert.Equal(new ushort[] { 1, 43, 89, 0 }, got.Moves);
        Assert.Equal(new StatSet(4, 252, 0, 252, 0, 0), got.Evs);
        Assert.Equal(new StatSet(31, 31, 31, 31, 31, 31), got.Ivs);
        Assert.Equal((ushort)13, got.HeldItem);
        Assert.Equal(mon.Nature, got.Nature);
        Assert.True(got.ShinyModifier);
        Assert.Equal(7, got.Party!.CurrentHp);
        Assert.Equal(got.Party.Stats.Hp - 7, BinaryPrimitives.ReadUInt16LittleEndian(got.Raw.Span[0x1E..]) & 0x3FFF);
        Assert.Equal(999999u, back.Money);
        Assert.Equal(s.Party[1]!.Raw.ToArray(), back.Party[1]!.Raw.ToArray());
        var sav = (SAV3E)SaveUtil.GetSaveFile(r.Bytes.ToArray())!;
        Assert.True(sav.ChecksumsValid);
        var pk = sav.GetPartySlotAtIndex(0);
        Assert.True(pk.Valid);
        Assert.Equal(new ushort[] { 1, 43, 89, 0 }, new[] { pk.Move1, pk.Move2, pk.Move3, pk.Move4 });
        Assert.Equal(252, pk.EV_ATK);
        Assert.Equal(13, pk.HeldItem);
        Assert.Equal(999999u, sav.Money);
    }

    [Fact]
    public void AnUnchangedMonEncodesToItsOwnBytes()
    {
        var mon = Open(File()).Party[1]!;
        Assert.Equal(mon.Raw.ToArray(), Pk3xEncoder.Encode(mon));
    }

    [Fact]
    public void FieldsTheEncoderCannotWriteAreRefused()
    {
        var s = Open(File());
        var mon = s.Party[0]!;
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Pid = mon.Pid + 1 }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Species = 0x800 }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Moves = [0x800, 0, 0, 0] }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Nature = 25 }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { IsShiny = true }));
        Assert.Equal(File(), s.Plan().Bytes);
    }

    [Fact]
    public void StatusIsRefusedTheLevelMustMatchThePartyLevelAndStatsMustBeInRange()
    {
        var s = Open(File());
        var mon = s.Party[0]!;
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Party = mon.Party! with { Level = 50 } }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Party = mon.Party! with { Status = 8 } }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Party = mon.Party! with { CurrentHp = mon.Party!.Stats.Hp + 1 } }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Level = 60 }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { HiddenAbility = true }));
    }

    [Fact]
    public void APartyLevelEditThatMatchesTheRecordLevelIsWrittenWithoutTheStatFormula()
    {
        var s = Open(File());
        var mon = s.Party[0]!;
        s.SetPartySlot(0, mon with { Level = 50, Party = mon.Party! with { Level = 50 } });
        Assert.Equal((byte)50, s.Party[0]!.Party!.Level);
        Assert.Equal(mon.Party!.Stats, s.Party[0]!.Party!.Stats);
    }

    [Fact]
    public void BitsTheEncoderDoesNotOwnSurviveAnEdit()
    {
        var pk = new PK3 { Species = (ushort)Species.Treecko, PID = 0x0102_0305, TID16 = 1, SID16 = 2, Nickname = "TREECKO", OriginalTrainerName = "PKHAX", Language = 2, EXP = 135, Move1 = 1, Move1_PP = 35, OriginalTrainerFriendship = 70 };
        pk.Stat_Level = 5;
        pk.Stat_HPMax = 20;
        pk.Stat_HPCurrent = 15;
        var d = pk.Data;
        BinaryPrimitives.WriteUInt16LittleEndian(d[0x20..], (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(d[0x20..]) | (7 << 11)));
        BinaryPrimitives.WriteUInt32LittleEndian(d[0x24..], BinaryPrimitives.ReadUInt32LittleEndian(d[0x24..]) | (0xC3u << 21));
        BinaryPrimitives.WriteUInt16LittleEndian(d[0x2A..], (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(d[0x2A..]) | (0xA5 << 6)));
        BinaryPrimitives.WriteUInt16LittleEndian(d[0x2C..], (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(d[0x2C..]) | (0x1F << 11)));
        d[0x34] |= 0x80;
        BinaryPrimitives.WriteUInt16LittleEndian(d[0x1E..], (ushort)((1 << 15) | 5));
        pk.RefreshChecksum();
        var record = new byte[100];
        pk.WriteEncryptedDataParty(record);
        var mon = RecordCodecs.For(RecordCodecId.Pk3x, "1.15").Decode(record, out var why);
        Assert.True(mon is not null, why);
        Assert.Equal(record, Pk3xEncoder.Encode(mon!));
        var edited = Pk3xEncoder.Encode(mon! with { Evs = mon!.Evs with { Atk = 100 }, Moves = [1, 89, 0, 0], Pp = [35, 10, 0, 0] });
        var back = new PK3(edited);
        var bd = back.Data;
        Assert.Equal(7, BinaryPrimitives.ReadUInt16LittleEndian(bd[0x20..]) >> 11);
        Assert.Equal(0xC3u, (BinaryPrimitives.ReadUInt32LittleEndian(bd[0x24..]) >> 21) & 0xFF);
        Assert.Equal(0xA5, (BinaryPrimitives.ReadUInt16LittleEndian(bd[0x2A..]) >> 6) & 0xFF);
        Assert.Equal(0x1F, BinaryPrimitives.ReadUInt16LittleEndian(bd[0x2C..]) >> 11);
        Assert.Equal(0x80, bd[0x34] & 0x80);
        Assert.Equal((1 << 15) | 5, BinaryPrimitives.ReadUInt16LittleEndian(bd[0x1E..]));
        Assert.Equal(100, back.EV_ATK);
    }

    [Fact]
    public void AMoneyOffsetOutsideTheBlockIsNotUsed()
    {
        var s = Open(File(), WriteEvidence.GameAccepted, Emerald with { MoneyOffset = 0x3D86 });
        Assert.Null(s.Money);
        Assert.Contains("money offset", s.ReadOnlyReason);
        Assert.Throws<FaithfulWriteException>(() => s.SetMoney(1));
    }

    [Fact]
    public void LayoutsFromTheHeaderAreChecked()
    {
        var header = new GfRomHeader { SaveBlock2Size = 0xF34, SaveBlock1Size = 0x3D90, PartyCountOffset = 0x238, PartyOffset = 0x23C, TrainerIdOffset = 0xA, PlayerNameOffset = 0 };
        var ok = ExpansionLayout.FromHeader(header, Pk3x115, out var why);
        Assert.True(ok is not null, why);
        Assert.Equal(0x23C, ok!.PartyOffset);
        Assert.Null(ExpansionLayout.FromHeader(header with { SaveBlock1Size = 0x3F08 }, Pk3x115, out why));
        Assert.Contains("four", why);
        Assert.Null(ExpansionLayout.FromHeader(header with { PartyOffset = 0x3D80 }, Pk3x115, out why));
        Assert.Contains("party offsets", why);
        Assert.Null(ExpansionLayout.FromHeader(header, new RecordCodecKey(RecordCodecId.Pk3, null), out why));
        Assert.Null(ExpansionLayout.FromHeader(header with { SaveBlock2Size = 0x80 }, Pk3x115, out why));
        Assert.Contains("below 0x100", why);
        Assert.Null(ExpansionLayout.FromHeader(header with { SaveBlock1Size = 0xF34 }, Pk3x115, out why));
        Assert.Contains("below 0x1000", why);
        Assert.Null(ExpansionLayout.FromHeader(header with { TrainerIdOffset = 0xFFFFFFFC }, Pk3x115, out why));
        Assert.Contains("trainer offsets", why);
        Assert.Null(ExpansionLayout.FromHeader(header with { PlayerNameOffset = 0xFFFFFFF8 }, Pk3x115, out why));
        Assert.Contains("trainer offsets", why);
        Assert.Null(ExpansionLayout.FromHeader(header with { PartyCountOffset = 0x23C }, Pk3x115, out why));
        Assert.Contains("party count is not before", why);
        Assert.Null(ExpansionLayout.FromHeader(header with { TrainerIdOffset = 4 }, Pk3x115, out why));
        Assert.Contains("overlap", why);
    }

    private static byte[] HeaderImage(int shift, uint sb2 = 0xF34, uint sb1 = 0x3D90, bool rhh = true, uint? wordAt0x188 = null)
    {
        var rom = new byte[0x400];
        if (rhh)
            "RHHEXP"u8.CopyTo(rom.AsSpan(0x204));
        if (wordAt0x188 is { } w0)
            BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x188), w0);
        void W(int field, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(0x100 + shift + field), value);
        W(0x88, sb2);
        W(0x8C, sb1);
        W(0x90, 0x238);
        W(0x94, 0x23C);
        W(0x9C, 0xA);
        W(0xA0, 0);
        return rom;
    }

    [Fact]
    public void AHeaderWithoutNamePointersStillGivesTheSaveLayout()
    {
        var rom = new GbaImage(HeaderImage(0));
        Assert.Null(GfRomHeader.Read(rom, out var refused));
        Assert.Contains("species-info pointer", refused);
        var candidate = Assert.Single(GfRomHeader.ReadAll(rom));
        Assert.Equal((0, true, false, true), (candidate.Shift, candidate.SaneSizes, candidate.SaneTables, candidate.NamesInline));
        var layout = ExpansionLayout.FromRom(rom, Pk3x115, out var why);
        Assert.True(layout is not null, why);
        Assert.Equal((0xF34, 0x3D90, 0x238, 0x23C), (layout!.SaveBlock2Size, layout.SaveBlock1Size, layout.PartyCountOffset, layout.PartyOffset));
        Assert.Equal(0, layout.HeaderShift);
        var shifted = ExpansionLayout.FromRom(new GbaImage(HeaderImage(4, wordAt0x188: 0x200)), Pk3x115, out why);
        Assert.True(shifted is not null, why);
        Assert.Equal((0xF34, 0x3D90, 0x238, 0x23C, 0xA, 0, 4), (shifted!.SaveBlock2Size, shifted.SaveBlock1Size, shifted.PartyCountOffset, shifted.PartyOffset, shifted.TrainerIdOffset, shifted.PlayerNameOffset, shifted.HeaderShift));
        Assert.Null(ExpansionLayout.FromRom(new GbaImage(HeaderImage(0, rhh: false)), Pk3x115, out why));
        Assert.Contains("RHHEXP", why);
        Assert.NotNull(ExpansionLayout.FromRom(new GbaImage(HeaderImage(0, rhh: false)), new RecordCodecKey(RecordCodecId.Pk3SoulGold96, null), out why));
        Assert.Null(ExpansionLayout.FromRom(new GbaImage(HeaderImage(0, sb2: 0, sb1: 0)), Pk3x115, out why));
        Assert.Contains("shift 0:", why);
        Assert.Contains("shift 4:", why);
    }

    private static byte[] SoulGoldRecord(ushort species, byte level, uint pid)
    {
        var r = new byte[96];
        BinaryPrimitives.WriteUInt32LittleEndian(r, pid);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(4), 0x0002_0001);
        r.AsSpan(0x08, 12).Fill(0xFF);
        r[0x08] = 0xBB;
        r[0x14] = 2;
        r[0x15] = 2;
        r.AsSpan(0x16, 7).Fill(0xFF);
        r[0x16] = 0xBC;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x20), (ushort)(species | (3 << 11)));
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x22), 13);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x24), 1000 | (0x55u << 21));
        r[0x2B] = 70;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x2C), (ushort)(33 | (0x1F << 11)));
        r[0x34] = 35 | 0x80;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(0x48), 31 | (20u << 5));
        r[0x50] = level;
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x52), 18);
        BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x1E), 2);
        for (var i = 0; i < 6; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(r.AsSpan(0x54 + (i * 2)), 20);
        return r;
    }

    private static readonly ExpansionLayout SoulGold = Emerald with { Codec = new RecordCodecKey(RecordCodecId.Pk3SoulGold96, null), MoneyOffset = null, MoneyKeyOffset = null };

    private static byte[] SoulGoldFile()
    {
        var file = File();
        var c = ExpansionSave.TryOpen(file, SoulGold, WriteEvidence.Unproven, out var why)!.Container;
        var edits = new[] { new BlockEdit(SaveBlock.Large, 0x238, SoulGoldRecord(152, 12, 0x0101_0203)), new BlockEdit(SaveBlock.Large, 0x238 + 96, SoulGoldRecord(155, 10, 0x0A0B_0C0D)) };
        return FaithfulWriter.Patch(c, edits, [.. edits.Select(e => new BlockWindow(e.Block, e.Offset, e.Bytes.Length))]).Bytes;
    }

    [Fact]
    public void SoulGoldPartiesDecodeAndAnEditKeepsTheBitsItDoesNotOwn()
    {
        var file = SoulGoldFile();
        var s = Open(file, layout: SoulGold);
        Assert.Equal(2, s.PartyCount);
        var mon = s.Party[0]!;
        Assert.Equal((ushort)152, mon.Species);
        Assert.Equal((byte)12, mon.Level);
        Assert.Equal(mon.Raw.ToArray(), Pk3SoulGold96Encoder.Encode(mon));
        s.SetPartySlot(0, mon with { Moves = [33, 89, 0, 0], Pp = [35, 10, 0, 0], Evs = mon.Evs with { SpA = 252 }, Ivs = new StatSet(31, 31, 31, 31, 31, 31), Nature = (byte)((mon.PidNature!.Value + 1) % 25), Friendship = (byte)(mon.Friendship!.Value ^ 0x5A), PpUps = [3, 0, 2, 1], HeldItem = (ushort)(mon.HeldItem == 139 ? 140 : 139), TeraType = 5 });
        var r = s.Plan();
        Assert.True(r.Confined, string.Join(", ", r.Outside));
        var back = Open(r.Bytes, layout: SoulGold);
        var got = back.Party[0]!;
        Assert.Equal(new ushort[] { 33, 89, 0, 0 }, got.Moves);
        Assert.Equal(252, got.Evs.SpA);
        Assert.Equal(new StatSet(31, 31, 31, 31, 31, 31), got.Ivs);
        Assert.Equal((byte)((mon.PidNature!.Value + 1) % 25), got.Nature);
        Assert.Equal((byte)(mon.Friendship!.Value ^ 0x5A), got.Friendship);
        Assert.Equal(new byte[] { 3, 0, 2, 1 }, got.PpUps);
        Assert.Equal(mon.HeldItem == 139 ? 140 : 139, got.HeldItem);
        Assert.Equal((byte?)5, got.TeraType);
        Assert.Equal(mon.Party!.CurrentHp, got.Party!.CurrentHp);
        var raw = got.Raw.Span;
        var owned = new HashSet<int>([0x14, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x28, 0x2B, .. Enumerable.Range(0x2C, 0x12), .. Enumerable.Range(0x48, 4)]);
        var gotBytes = got.Raw.ToArray();
        var wasBytes = mon.Raw.ToArray();
        var changed = Enumerable.Range(0, gotBytes.Length).Where(i => gotBytes[i] != wasBytes[i]).ToList();
        Assert.All(changed, i => Assert.Contains(i, owned));
        Assert.Contains(0x2B, changed);
        Assert.Contains(0x28, changed);
        Assert.Equal(5, BinaryPrimitives.ReadUInt16LittleEndian(raw[0x20..]) >> 11);
        Assert.Equal(0x55u, (BinaryPrimitives.ReadUInt32LittleEndian(raw[0x24..]) >> 21) & 0xFF);
        Assert.Equal(0x1F, BinaryPrimitives.ReadUInt16LittleEndian(raw[0x2C..]) >> 11);
        Assert.Equal(0x80, raw[0x34] & 0x80);
        Assert.Equal(s.Party[1]!.Raw.ToArray(), back.Party[1]!.Raw.ToArray());
    }

    [Fact]
    public void SoulGoldCurrentHpWritesTheBoxSideHpCopyToo()
    {
        var s = Open(SoulGoldFile(), layout: SoulGold);
        var mon = s.Party[0]!;
        Assert.Equal((18, 20), (mon.Party!.CurrentHp, mon.Party.Stats.Hp));
        s.SetPartySlot(0, mon with { Party = mon.Party with { CurrentHp = 7 } });
        var r = s.Plan();
        Assert.True(r.Confined, string.Join(", ", r.Outside));
        var got = Open(r.Bytes, layout: SoulGold).Party[0]!;
        var raw = got.Raw.ToArray();
        Assert.Equal(7, got.Party!.CurrentHp);
        Assert.Equal(13, BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0x1E)));
        var was = mon.Raw.ToArray();
        Assert.Equal([0x1E, 0x52], Enumerable.Range(0, raw.Length).Where(i => raw[i] != was[i]).ToArray());
        var full = Open(r.Bytes, layout: SoulGold);
        full.SetPartySlot(0, got with { Party = got.Party with { CurrentHp = 20 } });
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(Open(full.Plan().Bytes, layout: SoulGold).Party[0]!.Raw.Span[0x1E..]));
    }

    [Fact]
    public void SoulGoldCurrentHpIsRefusedWhenTheHpCopyIsNotWhereItWasMeasured()
    {
        var file = SoulGoldFile();
        var s = Open(file, layout: SoulGold);
        var mon = s.Party[0]!;
        var raw = mon.Raw.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0x1E), 0);
        var odd = mon with { Raw = raw };
        var e = Assert.Throws<RecordEncodeException>(() => Pk3SoulGold96Encoder.Encode(odd with { Party = odd.Party! with { CurrentHp = 7 } }));
        Assert.Contains("0x1E are not max HP minus current HP", e.Message);
    }

    [Fact]
    public void SoulGoldHpLostKeepsTheTopBitsAndHandlesFaintingAndBigStats()
    {
        var s = Open(SoulGoldFile(), layout: SoulGold);
        var mon = s.Party[0]!;
        var raw = mon.Raw.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(0x1E), (ushort)(0x4000 | 2));
        var shiny = mon with { Raw = raw };
        var fainted = Pk3SoulGold96Encoder.Encode(shiny with { Party = shiny.Party! with { CurrentHp = 0 } });
        Assert.Equal(0x4000 | 20, BinaryPrimitives.ReadUInt16LittleEndian(fainted.AsSpan(0x1E)));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(fainted.AsSpan(0x52)));
        var big = mon.Raw.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(big.AsSpan(0x54), 300);
        BinaryPrimitives.WriteUInt16LittleEndian(big.AsSpan(0x52), 300);
        BinaryPrimitives.WriteUInt16LittleEndian(big.AsSpan(0x1E), 0);
        var bigMon = RecordCodecs.For(RecordCodecId.Pk3SoulGold96).Decode(big, out var why) ?? throw new InvalidOperationException(why);
        var half = Pk3SoulGold96Encoder.Encode(bigMon with { Party = bigMon.Party! with { CurrentHp = 23 } });
        Assert.Equal(277, BinaryPrimitives.ReadUInt16LittleEndian(half.AsSpan(0x1E)));
    }

    [Fact]
    public void SoulGoldRefusesWhatItCannotPlace()
    {
        var s = Open(SoulGoldFile(), layout: SoulGold);
        var mon = s.Party[0]!;
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Ball = 4 }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { ShinyModifier = true }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Party = mon.Party! with { Level = 50 } }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Pid = 1 }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Party = mon.Party! with { CurrentHp = mon.Party!.Stats.Hp + 1 } }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { National = (ushort)(mon.National.GetValueOrDefault() + 1) }));
        Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { HiddenNatureModifier = (byte)((mon.HiddenNatureModifier.GetValueOrDefault() + 1) % 25), Nature = mon.Nature }));
        var pp = Assert.Throws<RecordEncodeException>(() => s.SetPartySlot(0, mon with { Pp = [89, 0, 0, 0] }));
        Assert.Contains("above 64", pp.Message);
    }

    [Fact]
    public void WritesWaitForGameAccepted()
    {
        var file = File();
        Assert.Throws<FaithfulWriteException>(() => Open(file, WriteEvidence.Confined).Write());
        Assert.Equal(file, Open(file, WriteEvidence.GameAccepted).Write().Bytes);
    }

    [Fact]
    public void AWrongPartyOffsetOpensReadOnly()
    {
        var s = Open(File(), WriteEvidence.GameAccepted, Emerald with { PartyCountOffset = 0x239 });
        Assert.True(s.ReadOnly);
    }
}
