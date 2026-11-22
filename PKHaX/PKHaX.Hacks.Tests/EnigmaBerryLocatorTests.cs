using System;
using System.Buffers.Binary;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Saves.Gen3;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class EnigmaBerryLocatorTests
{
    private const int SpeciesNames = 0x10000;
    private const int MoveNames = 0x11000;
    private const int AbilityNames = 0x12000;
    private const int SpeciesInfo = 0x13000;
    private const int Moves = 0x14000;
    private const int Items = 0x15000;
    private const int AbilityDescriptions = 0x16000;
    private const int BallGfx = 0x17000;
    private const int Decorations = 0x18000;
    private const int IconPalettes = 0x19000;
    private const int Descriptions = 0x1A000;
    private const int ItemCount = 200;
    private const int QuickClawItem = 180;
    private const int FocusBandItem = 190;
    private const int SectorSize = 0x1000;
    private const int FileSize = 0x20000;
    private const uint BlockSize = 0x3D68;
    private const uint RetailOffset = 0x30EC;

    private static byte[] Text(string text)
    {
        var bytes = new byte[text.Length + 1];
        for (var i = 0; i < text.Length; i++)
            bytes[i] = text[i] == ' ' ? (byte)0x00 : text[i] == '?' ? (byte)0xAC : (byte)(0xBB + (text[i] - 'A'));
        bytes[text.Length] = 0xFF;
        return bytes;
    }

    private static string ItemName(int index) => index switch
    {
        0 => "????????",
        1 => "MASTER BALL",
        EnigmaBerryLocator.EnigmaBerryItem => "ENIGMA BERRY",
        QuickClawItem => "QUICK CLAW",
        FocusBandItem => "FOCUS BAND",
        _ => "ITEM " + (char)('A' + (index / 26)) + (char)('A' + (index % 26)),
    };

    private static byte[] Image(int shift = 0, uint offset = RetailOffset, uint size = 0x34, uint blockSize = BlockSize, int items = ItemCount, string second = "MASTER BALL")
    {
        var rom = new byte[0x20000];
        void Word(int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(rom.AsSpan(at), value);
        void Ptr(int at, int target) => Word(at, GbaImage.RomBase + (uint)target);
        var head = GfRomHeader.At;
        var tail = GfRomHeader.At + shift;
        Ptr(head + 0x40, IconPalettes);
        Ptr(head + 0x4C, Decorations);
        Ptr(tail + 0xC4, AbilityDescriptions);
        Ptr(tail + 0xD0, BallGfx);
        Ptr(head + 0x44, SpeciesNames);
        Ptr(head + 0x48, MoveNames);
        Ptr(tail + 0xC0, AbilityNames);
        Word(head + 0x50, 0xEE0);
        Word(head + 0x54, 0x1000);
        Word(head + 0x58, 0x18);
        Word(head + 0x70, 386);
        Word(tail + 0x88, 0xF24);
        Word(tail + 0x8C, blockSize);
        Word(tail + 0x90, 0x34);
        Word(tail + 0x94, 0x38);
        Word(tail + 0x9C, 0xA);
        Ptr(tail + 0xBC, SpeciesInfo);
        Ptr(tail + 0xC8, Items);
        Ptr(tail + 0xCC, Moves);
        Word(tail + 0xEC, 0x298);
        Word(tail + 0xF4, offset);
        Word(tail + 0xF8, size);
        for (var index = 0; index < items; index++)
        {
            var at = Items + (index * EnigmaBerryLocator.ItemEntrySize);
            Text(index == 1 ? second : ItemName(index)).CopyTo(rom.AsSpan(at));
            BinaryPrimitives.WriteUInt16LittleEndian(rom.AsSpan(at + EnigmaBerryLocator.ItemIdOffset), (ushort)(index % 7 == 3 ? 0 : index));
            rom[at + EnigmaBerryLocator.ItemHoldEffectOffset] = index switch { QuickClawItem => 26, FocusBandItem => 39, _ => 0 };
            rom[at + EnigmaBerryLocator.ItemParameterOffset] = index switch { QuickClawItem => 20, FocusBandItem => 10, _ => 0 };
            Ptr(at + EnigmaBerryLocator.ItemDescriptionOffset, Descriptions + (index * 4));
        }
        return rom;
    }

    private static GfRomHeader Header(byte[] rom)
    {
        var header = GfRomHeader.Read(new GbaImage(rom), out var why);
        Assert.True(header is not null, why);
        return header!;
    }

    private static byte[] Record(byte seed)
    {
        var record = new byte[EnigmaBerryLocator.RecordSize];
        for (var i = 0; i < record.Length; i++)
            record[i] = (byte)(seed + (i * 3));
        return record;
    }

    private static void Slot(byte[] file, int slot, uint counter, int stride, int offset, byte[] record, bool busy, int window = 0, bool breakChecksums = false)
    {
        var random = new Random(1750 + slot + stride);
        var last = (int)BlockSize - (3 * stride);
        for (var id = 0; id < 14; id++)
        {
            var sector = file.AsSpan(((slot * 14) + id) * SectorSize, SectorSize);
            if (busy)
                random.NextBytes(sector[..0xFF0]);
            var index = offset / stride;
            if (id == 1 + index)
                record.CopyTo(sector[(offset - (index * stride))..]);
            var size = id is >= 1 and <= 4 ? (id < 4 ? stride : last) : stride;
            var sum = ChecksumPolicy.Fold(sector[..(window == 0 ? size : window)]);
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.IdOffset..], (ushort)id);
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.ChecksumOffset..], (ushort)(breakChecksums ? sum ^ 0x5A5A : sum));
            BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.SignatureOffset..], GbaSectorContainer.RetailSignature);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[GbaSectorContainer.CounterOffset..], counter);
        }
    }

    private static byte[] Save(int stride, int offset, byte[] record, bool busy, int window = 0, bool breakChecksums = false)
    {
        var file = new byte[FileSize];
        Slot(file, 0, 1, stride, offset, record, busy, window, breakChecksums);
        return file;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    [InlineData(4)]
    public void TheHeaderGivesTheOffsetAtEveryShift(int shift)
    {
        var place = EnigmaBerryLocator.Locate(new GbaImage(Image(shift)), out var header, out var why);
        Assert.True(place is not null, why);
        Assert.NotNull(header);
        Assert.Equal(new EnigmaBerryPlace(0x30EC, 0x34, shift), place);
    }

    [Theory]
    [InlineData(0x2F00u)]
    [InlineData(0x31F8u)]
    [InlineData(0x3D34u)]
    public void TheOffsetIsTheHeaderValueNotAConstant(uint offset)
    {
        var place = EnigmaBerryLocator.Locate(Header(Image(offset: offset)), out var why);
        Assert.True(place is not null, why);
        Assert.Equal((int)offset, place!.Offset);
    }

    [Theory]
    [InlineData(0x30ECu, 0u, "size 0")]
    [InlineData(0x30ECu, 0x530u, "0x530 bytes")]
    [InlineData(0x30ECu, 0x30u, "0x30 bytes")]
    [InlineData(0x3D38u, 0x34u, "runs past")]
    [InlineData(0x7000u, 0x34u, "runs past")]
    [InlineData(0xFFFFFFF0u, 0x34u, "runs past")]
    [InlineData(0x30EEu, 0x34u, "4-byte boundary")]
    public void ADeclarationThatDoesNotFitIsRefusedWithAReason(uint offset, uint size, string reason)
    {
        var place = EnigmaBerryLocator.Locate(Header(Image(offset: offset, size: size)), out var why);
        Assert.Null(place);
        Assert.Contains(reason, why);
    }

    [Fact]
    public void NoHeaderIsRefusedWithAReason()
    {
        Assert.Null(EnigmaBerryLocator.Locate((GfRomHeader?)null, out var why));
        Assert.Contains("no GF header", why);
        Assert.Null(EnigmaBerryLocator.Locate(new GbaImage(new byte[0x20000]), out var header, out var blankWhy));
        Assert.Null(header);
        Assert.False(string.IsNullOrWhiteSpace(blankWhy));
        var insane = Header(Image()) with { SaveBlock1Size = 0x9000 };
        Assert.Null(EnigmaBerryLocator.Locate(insane, out var insaneWhy));
        Assert.Contains("not trusted", insaneWhy);
    }

    [Fact]
    public void TheItemTableGivesItem175sDescriptionAndTheHoldEffects()
    {
        var rom = new GbaImage(Image());
        var header = Header(Image());
        var pointer = EnigmaBerryLocator.DescriptionPointer(rom, header, out var why);
        Assert.True(pointer is not null, why);
        Assert.Equal(GbaImage.RomBase + (uint)Descriptions + (EnigmaBerryLocator.EnigmaBerryItem * 4), pointer);

        var items = EnigmaBerryLocator.HoldItems(rom, header, out var itemsWhy);
        Assert.True(items is not null, itemsWhy);
        Assert.Equal(ItemCount - 1, items!.Count);
        Assert.Equal(Enumerable.Range(1, ItemCount - 1), items.Select(i => i.Index));
        var claw = items.Single(i => i.HoldEffect == 26);
        Assert.Equal((QuickClawItem, "QUICK CLAW", (byte)20), (claw.Index, claw.Name, claw.Parameter));
        var band = items.Single(i => i.HoldEffect == 39);
        Assert.Equal((FocusBandItem, "FOCUS BAND", (byte)10), (band.Index, band.Name, band.Parameter));
    }

    [Fact]
    public void AnItemTableThatIsNotTheRetailLayoutIsRefused()
    {
        var renamed = Image(second: "POKE BALL");
        Assert.Null(EnigmaBerryLocator.HoldItems(new GbaImage(renamed), Header(renamed), out var why));
        Assert.Contains("MASTER BALL", why);
        Assert.Null(EnigmaBerryLocator.DescriptionPointer(new GbaImage(renamed), Header(renamed), out var pointerWhy));
        Assert.Contains("MASTER BALL", pointerWhy);

        var shortTable = Image(items: 100);
        var items = EnigmaBerryLocator.HoldItems(new GbaImage(shortTable), Header(shortTable), out _);
        Assert.Equal(99, items!.Count);
        Assert.Null(EnigmaBerryLocator.DescriptionPointer(new GbaImage(shortTable), Header(shortTable), out var shortWhy));
        Assert.Contains("no item 175", shortWhy);

        var header = Header(Image()) with { Items = null };
        Assert.Null(EnigmaBerryLocator.HoldItems(new GbaImage(Image()), header, out var pointerless));
        Assert.Contains("item pointer", pointerless);
    }

    [Fact]
    public void ARetailStrideSaveGivesTheBytesAtTheHeaderOffset()
    {
        var header = Header(Image());
        var place = EnigmaBerryLocator.Locate(header, out _)!;
        var record = Record(0x21);
        var read = EnigmaBerryLocator.Read(Save(SectionLayout.VanillaStride, place.Offset, record, busy: false), header, place, out var why);
        Assert.True(read is not null, why);
        Assert.Equal(record, read!.Record);
        Assert.Equal((0, 4, SectionLayout.VanillaStride, (4 * SectorSize) + 0x26C), (read.Slot, read.Section, read.Stride, read.FileOffset));
        Assert.False(read.StrideAmbiguous);
    }

    [Fact]
    public void TheRetailSizeIsTakenWhenBothSizesChecksum()
    {
        var header = Header(Image());
        var place = EnigmaBerryLocator.Locate(header, out _)!;
        var record = Record(0x33);
        var file = Save(SectionLayout.VanillaStride, place.Offset, record, busy: false);
        for (var id = 1; id <= 3; id++)
        {
            var sector = file.AsSpan(id * SectorSize, SectorSize);
            BinaryPrimitives.WriteUInt32LittleEndian(sector, 1);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[SectionLayout.VanillaStride..], 0xFFFF);
            var sum = ChecksumPolicy.Fold(sector[..SectionLayout.VanillaStride]);
            Assert.Equal(sum, ChecksumPolicy.Fold(sector[..SectionLayout.CfruStride]));
            BinaryPrimitives.WriteUInt16LittleEndian(sector[GbaSectorContainer.ChecksumOffset..], sum);
        }
        var read = EnigmaBerryLocator.Read(file, header, place, out var why);
        Assert.True(read is not null, why);
        Assert.Equal(record, read!.Record);
        Assert.Equal((SectionLayout.VanillaStride, (4 * SectorSize) + 0x26C), (read.Stride, read.FileOffset));
        Assert.True(read.StrideAmbiguous);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SectionLayout.PayloadCapacity)]
    public void ALongStrideSaveGivesTheBytesFromItsOwnSection(int window)
    {
        var header = Header(Image());
        var place = EnigmaBerryLocator.Locate(header, out _)!;
        var record = Record(0x47);
        var read = EnigmaBerryLocator.Read(Save(SectionLayout.CfruStride, place.Offset, record, busy: true, window), header, place, out var why);
        Assert.True(read is not null, why);
        Assert.Equal(record, read!.Record);
        Assert.Equal((4, SectionLayout.CfruStride, (4 * SectorSize) + 0x11C), (read.Section, read.Stride, read.FileOffset));
        Assert.False(read.StrideAmbiguous);
    }

    [Fact]
    public void TheNewerSlotIsRead()
    {
        var header = Header(Image());
        var place = EnigmaBerryLocator.Locate(header, out _)!;
        var file = new byte[FileSize];
        Slot(file, 0, 7, SectionLayout.VanillaStride, place.Offset, Record(0x10), busy: false);
        Slot(file, 1, 8, SectionLayout.VanillaStride, place.Offset, Record(0x90), busy: false);
        var read = EnigmaBerryLocator.Read(file, header, place, out var why);
        Assert.True(read is not null, why);
        Assert.Equal(1, read!.Slot);
        Assert.Equal(Record(0x90), read.Record);
        Assert.Equal((18 * SectorSize) + 0x26C, read.FileOffset);
    }

    [Fact]
    public void ASaveThatDoesNotChecksumOrFitIsRefusedWithAReason()
    {
        var header = Header(Image());
        var place = EnigmaBerryLocator.Locate(header, out _)!;
        Assert.Null(EnigmaBerryLocator.Read(Save(SectionLayout.VanillaStride, place.Offset, Record(1), busy: true, breakChecksums: true), header, place, out var checksumWhy));
        Assert.Contains("matching checksum", checksumWhy);
        Assert.Null(EnigmaBerryLocator.Read(new byte[FileSize], header, place, out var blankWhy));
        Assert.Contains("section ids", blankWhy);
        Assert.Null(EnigmaBerryLocator.Read(new byte[0x10000], header, place, out var shortWhy));
        Assert.Contains("0x10000", shortWhy);
        Assert.Null(EnigmaBerryLocator.Read(new byte[FileSize + EnigmaBerryLocator.FooterAllowance + 1], header, place, out var longWhy));
        Assert.Contains("footer", longWhy);
    }

    [Fact]
    public void AFooterWithinTheAllowanceIsIgnored()
    {
        var header = Header(Image());
        var place = EnigmaBerryLocator.Locate(header, out _)!;
        var record = Record(0x61);
        var file = Save(SectionLayout.VanillaStride, place.Offset, record, busy: false);
        var padded = new byte[FileSize + 16];
        file.CopyTo(padded, 0);
        padded.AsSpan(FileSize).Fill(0xEE);
        var read = EnigmaBerryLocator.Read(padded, header, place, out var why);
        Assert.True(read is not null, why);
        Assert.Equal(record, read!.Record);
    }

    [Fact]
    public void ReadingChangesNeitherTheSaveNorTheRom()
    {
        var rom = Image();
        var romBefore = rom.ToArray();
        var image = new GbaImage(rom);
        var place = EnigmaBerryLocator.Locate(image, out var header, out _)!;
        var file = Save(SectionLayout.VanillaStride, place.Offset, Record(5), busy: true);
        var fileBefore = file.ToArray();
        Assert.NotNull(EnigmaBerryLocator.Read(file, header!, place, out _));
        Assert.NotNull(EnigmaBerryLocator.HoldItems(image, header!, out _));
        Assert.Equal(romBefore, rom);
        Assert.Equal(fileBefore, file);
    }
}
