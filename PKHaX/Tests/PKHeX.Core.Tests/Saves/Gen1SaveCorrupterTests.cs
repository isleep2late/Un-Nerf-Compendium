using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace PKHeX.Core.Tests.Saves;

/// <summary>
/// PKHaX: Gen 1 save corrupter (Any% 2-swap). Party count 0xFF, terminator 0xFF, checksum recomputed, nothing else,
/// applied to the raw file bytes (never to a re-serialized SAV1).
/// </summary>
public static class Gen1SaveCorrupterTests // PKHaX
{
    private const ushort TrainerID = 0x40AB; // high byte $40 like the route's practice save

    /// <summary>
    /// A full-layout synthetic save: party of <paramref name="partyCount"/>, box 1 and box 3 populated so
    /// BoxesInitialized is set and every box region is written, serialized once through Write() to produce the
    /// bytes "on disk".
    /// </summary>
    private static byte[] GetDiskBytes(LanguageID language, GameVersion version, int partyCount)
    {
        var sav = new SAV1(language, version) { OT = language == LanguageID.Japanese ? "ハクス" : "PKHAX", TID16 = TrainerID };
        PKM Make(int i)
        {
            var pk = sav.BlankPKM;
            pk.Species = (ushort)((int)Species.Bulbasaur + (3 * i));
            pk.CurrentLevel = (byte)(5 + i);
            pk.TID16 = TrainerID;
            pk.OriginalTrainerName = sav.OT;
            pk.Nickname = SpeciesName.GetSpeciesNameGeneration(pk.Species, sav.Language, 1);
            return pk;
        }
        for (int i = 0; i < partyCount; i++)
            sav.SetPartySlotAtIndex(Make(i), i);
        sav.SetBoxSlotAtIndex(Make(10), 0, 0);
        sav.SetBoxSlotAtIndex(Make(11), 0, 1);
        sav.SetBoxSlotAtIndex(Make(12), 2, 0);
        sav.PartyCount.Should().Be(partyCount);

        var disk = sav.Write().ToArray();
        disk.Length.Should().Be(Gen1SaveCorrupter.ExpectedSize);
        var reloaded = new SAV1(disk.ToArray(), language, version);
        reloaded.BoxesInitialized.Should().BeTrue("the synthetic save must exercise the full box layout");
        reloaded.PartyCount.Should().Be(partyCount);
        return disk;
    }

    /// <summary>
    /// The reference the standalone corrupt-save-gen1.py implements, written out from the constants without
    /// calling anything in Gen1SaveCorrupter or SAV1: three bytes, byte-sum checksum, bitwise NOT.
    /// </summary>
    private static byte[] ReferenceCorrupt(byte[] disk, int party, int checksum)
    {
        var expected = disk.ToArray();
        expected[party] = 0xFF;
        expected[party + 1] = 0xFF;
        expected[checksum] = ReferenceChecksum(expected, checksum);
        return expected;
    }

    /// <summary>calculate_gen1_checksum from corrupt-save-gen1.py: byte sum over 0x2598..checksum-1, bitwise NOT.</summary>
    private static byte ReferenceChecksum(byte[] data, int checksum)
    {
        int sum = 0;
        for (int i = 0x2598; i < checksum; i++)
            sum = (sum + data[i]) & 0xFF;
        return (byte)(~sum & 0xFF);
    }

    private static List<int> Diff(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var list = new List<int>();
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                list.Add(i);
        }
        return list;
    }

    [Theory]
    [InlineData(GameVersion.RB, 2)]
    [InlineData(GameVersion.YW, 2)]
    [InlineData(GameVersion.RB, 6)]
    public static void ApplyInPlaceMatchesIndependentReferenceINT(GameVersion version, int partyCount)
    {
        // The standalone tool hard-codes the INT layout: 0x2F2C, 0x2F2D and 0x3523 over 0x2598..0x3522.
        SAV1Offsets.INT.Party.Should().Be(0x2F2C);
        SAV1Offsets.INT.ChecksumOfs.Should().Be(0x3523);
        SAV1Offsets.INT.OT.Should().Be(0x2598);
        SAV1Offsets.INT.TID16.Should().Be(0x2605);

        var disk = GetDiskBytes(LanguageID.English, version, partyCount);
        disk[0x2F2C].Should().Be((byte)partyCount);
        disk[0x2F2D].Should().NotBe(0xFF, "slot 1 species is stored right after the count");
        var expected = ReferenceCorrupt(disk, 0x2F2C, 0x3523);

        var actual = disk.ToArray();
        Gen1SaveCorrupter.ApplyInPlace(actual, SAV1Offsets.INT);
        actual.Should().Equal(expected);

        Diff(actual, disk).Should().BeEquivalentTo([0x2F2C, 0x2F2D, 0x3523]);
        actual[0x2605].Should().Be(disk[0x2605]);
        actual[0x2606].Should().Be(disk[0x2606]);
        Gen1SaveCorrupter.GetTID16(actual, SAV1Offsets.INT).Should().Be(TrainerID);
        Gen1SaveCorrupter.IsApplied(disk, SAV1Offsets.INT).Should().BeFalse();
        Gen1SaveCorrupter.IsApplied(actual, SAV1Offsets.INT).Should().BeTrue();
        new SAV1(actual.ToArray(), LanguageID.English, version).ChecksumsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(GameVersion.RB, 2)]
    [InlineData(GameVersion.YW, 3)]
    public static void ApplyInPlaceMatchesIndependentReferenceJPN(GameVersion version, int partyCount)
    {
        SAV1Offsets.JPN.Party.Should().Be(0x2ED5);
        SAV1Offsets.JPN.ChecksumOfs.Should().Be(0x3594);
        SAV1Offsets.JPN.OT.Should().Be(0x2598);
        SAV1Offsets.JPN.TID16.Should().Be(0x25FB);

        var disk = GetDiskBytes(LanguageID.Japanese, version, partyCount);
        disk[0x2ED5].Should().Be((byte)partyCount);
        disk[0x2ED6].Should().NotBe(0xFF);
        var expected = ReferenceCorrupt(disk, 0x2ED5, 0x3594);

        var actual = disk.ToArray();
        Gen1SaveCorrupter.ApplyInPlace(actual, SAV1Offsets.JPN);
        actual.Should().Equal(expected);

        Diff(actual, disk).Should().BeEquivalentTo([0x2ED5, 0x2ED6, 0x3594]);
        Gen1SaveCorrupter.GetTID16(actual, SAV1Offsets.JPN).Should().Be(TrainerID);
        Gen1SaveCorrupter.IsApplied(actual, SAV1Offsets.JPN).Should().BeTrue();
        new SAV1(actual.ToArray(), LanguageID.Japanese, version).ChecksumsValid.Should().BeTrue();
    }

    [Fact]
    public static void ApplyInPlaceOnRandomBytesMatchesReference()
    {
        var raw = new byte[Gen1SaveCorrupter.ExpectedSize];
        new Random(1337).NextBytes(raw);
        var expected = ReferenceCorrupt(raw, 0x2F2C, 0x3523);
        Gen1SaveCorrupter.ApplyInPlace(raw, SAV1Offsets.INT);
        raw.Should().Equal(expected);
        Gen1SaveCorrupter.IsApplied(raw, SAV1Offsets.INT).Should().BeTrue();
    }

    [Fact]
    public static void NegativeControlFlippedByteInsideChecksummedRegionBreaksChecksum()
    {
        var corrupted = GetDiskBytes(LanguageID.English, GameVersion.RB, 2);
        var ofs = SAV1Offsets.INT;
        Gen1SaveCorrupter.ApplyInPlace(corrupted, ofs);
        Gen1SaveCorrupter.IsChecksumValid(corrupted, ofs).Should().BeTrue();

        foreach (var index in new[] { 0x2598, 0x2605, 0x2F2E, 0x3000, 0x3522 }) // first, TID, party body, middle, last byte of the region
        {
            var tampered = corrupted.ToArray();
            tampered[index] ^= 0x01;
            Gen1SaveCorrupter.IsChecksumValid(tampered, ofs).Should().BeFalse($"offset 0x{index:X4} is inside 0x2598..0x3522");
            Gen1SaveCorrupter.IsApplied(tampered, ofs).Should().BeFalse();
            new SAV1(tampered, LanguageID.English).ChecksumsValid.Should().BeFalse();
        }

        // Control for the control: a byte outside the region does not affect the checksum.
        var outside = corrupted.ToArray();
        outside[0x2597] ^= 0x01;
        outside[0x3524] ^= 0x01;
        Gen1SaveCorrupter.IsChecksumValid(outside, ofs).Should().BeTrue();

        var uncorrupted = corrupted.ToArray();
        uncorrupted[ofs.Party] = 0x02; // count restored but checksum stale
        Gen1SaveCorrupter.IsApplied(uncorrupted, ofs).Should().BeFalse();
        Gen1SaveCorrupter.IsChecksumValid(uncorrupted, ofs).Should().BeFalse();
    }

    /// <summary>
    /// Regression for the review finding: re-serializing through Write() is NOT byte-identical to the file on disk.
    /// A save whose current-box mirror is stale (the current-box index in the raw bytes points at a box other than
    /// the one mirrored at Offsets.CurrentBox) is re-packed by SAV1 on load + Write(), changing box regions. The
    /// corrupter must therefore never go through Write(): ApplyInPlace on the disk bytes changes exactly three bytes.
    /// </summary>
    [Theory]
    [InlineData(LanguageID.English, GameVersion.RB)]
    [InlineData(LanguageID.Japanese, GameVersion.YW)]
    public static void WriteReserializationDiffersFromDiskButApplyInPlaceDoesNot(LanguageID language, GameVersion version)
    {
        var ofs = language == LanguageID.Japanese ? SAV1Offsets.JPN : SAV1Offsets.INT;
        var disk = GetDiskBytes(language, version, 2);
        // Stale mirror: the mirror at Offsets.CurrentBox still holds box 1's list, but the index now says box 4.
        disk[ofs.CurrentBoxIndex] = (byte)((disk[ofs.CurrentBoxIndex] & 0x80) | 3);
        Gen1SaveCorrupter.IsChecksumValid(disk, ofs).Should().BeFalse("the index is inside the checksummed region");
        disk[ofs.ChecksumOfs] = ReferenceChecksum(disk, ofs.ChecksumOfs);
        Gen1SaveCorrupter.IsChecksumValid(disk, ofs).Should().BeTrue();

        // What the old UI path did: load, Write(), patch. Measured here: the box regions get re-packed.
        var reserialized = new SAV1(disk.ToArray(), language, version).Write().ToArray();
        var writeDiff = Diff(reserialized, disk);
        writeDiff.Should().NotBeEmpty("Write() re-packs the boxes from the stale mirror");
        writeDiff.Should().Contain(i => i >= 0x4000, "box storage regions differ after re-serialization");

        // What the UIs do now: patch the disk bytes. Exactly the three bytes.
        var corrupted = disk.ToArray();
        Gen1SaveCorrupter.ApplyInPlace(corrupted, ofs);
        Diff(corrupted, disk).Should().BeEquivalentTo([ofs.Party, ofs.Party + 1, ofs.ChecksumOfs]);
        corrupted.Should().Equal(ReferenceCorrupt(disk, ofs.Party, ofs.ChecksumOfs));
    }

    [Fact]
    public static void CorruptFileOnDiskWritesBackupThenCorruptsExactlyTheDiskBytes()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pkhax-gen1-corrupter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "Blue.srm");
            var disk = GetDiskBytes(LanguageID.English, GameVersion.RB, 2);
            File.WriteAllBytes(path, disk);
            var now = new DateTime(2026, 9, 10, 20, 37, 5);

            var backup = Gen1SaveCorrupter.CorruptFileOnDisk(path, SAV1Offsets.INT, TrainerID, now);
            backup.Should().Be(path + ".bak-20260910-203705");
            File.ReadAllBytes(backup).Should().Equal(disk, "the backup is the file exactly as it was");
            File.ReadAllBytes(path).Should().Equal(ReferenceCorrupt(disk, 0x2F2C, 0x3523));

            // Same second again: the backup must never be overwritten, and the (now corrupted) file must not change.
            var afterFirst = File.ReadAllBytes(path);
            var again = () => Gen1SaveCorrupter.CorruptFileOnDisk(path, SAV1Offsets.INT, TrainerID, now);
            again.Should().Throw<IOException>();
            File.ReadAllBytes(backup).Should().Equal(disk);
            File.ReadAllBytes(path).Should().Equal(afterFirst);

            // Trainer ID mismatch: refused before anything is written.
            var mismatch = () => Gen1SaveCorrupter.CorruptFileOnDisk(path, SAV1Offsets.INT, (ushort)(TrainerID ^ 1), now.AddSeconds(1));
            mismatch.Should().Throw<InvalidDataException>();
            File.Exists(path + ".bak-20260910-203706").Should().BeFalse();
            File.ReadAllBytes(path).Should().Equal(afterFirst);

            // Wrong size: refused before anything is written.
            var small = Path.Combine(dir, "small.sav");
            File.WriteAllBytes(small, new byte[0x7FFF]);
            var wrongSize = () => Gen1SaveCorrupter.CorruptFileOnDisk(small, SAV1Offsets.INT, null, now);
            wrongSize.Should().Throw<InvalidDataException>();
            File.Exists(small + ".bak-20260910-203705").Should().BeFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// The mobile front-end's contract (SaveManager.Load): the backup / corruption source is a private copy taken BEFORE
    /// the array reaches SaveUtil.GetSaveFile, because SaveFile aliases the array it is given (Buffer = data, no copy).
    /// Measured here: trainer edits (OT, money), a party-slot edit and a Write() all land in the array the SaveFile was
    /// given, while the copy taken first still equals the bytes as opened, so the backup is the file as opened and the
    /// corruption of that copy is the reference result.
    /// </summary>
    [Theory]
    [InlineData(LanguageID.English, GameVersion.RB)]
    [InlineData(LanguageID.English, GameVersion.YW)]
    [InlineData(LanguageID.Japanese, GameVersion.RB)]
    public static void PrivateCopyTakenBeforeLoadIsUnaffectedByEditsAndWrite(LanguageID language, GameVersion version)
    {
        var ofs = language == LanguageID.Japanese ? SAV1Offsets.JPN : SAV1Offsets.INT;
        var disk = GetDiskBytes(language, version, 2);

        var handedOver = disk.ToArray(); // what the platform gateway returns
        var asOpened = handedOver.ToArray(); // SaveManager.Load: the private copy, taken first
        var sav = SaveUtil.GetSaveFile(handedOver, "Blue.srm").Should().BeOfType<SAV1>().Subject;

        // TrainerPage: OT and money go straight into the save's buffer; the party editor replaces a slot; then a Write().
        sav.OT = language == LanguageID.Japanese ? "アカ" : "EDITED";
        sav.Money = 123456;
        var pk = sav.GetPartySlotAtIndex(0);
        pk.CurrentLevel = 77;
        sav.SetPartySlotAtIndex(pk, 0);
        var written = sav.Write().ToArray();

        // Control: without the copy, the "backup" would have been the live buffer.
        Diff(handedOver, disk).Should().NotBeEmpty("SaveFile aliases the array it is given, so the edits and Write() reach it");
        Diff(handedOver, disk).Should().Contain(ofs.OT, "the OT edit is in the aliased array");
        written.Should().Equal(handedOver, "Write() returns the live buffer");

        // The copy is the file exactly as opened, and corrupting it is the reference result.
        asOpened.Should().Equal(disk, "the backup must be the bytes as opened, not the edited buffer");
        Gen1SaveCorrupter.GetTID16(asOpened, ofs).Should().Be(sav.TID16, "the Trainer ID sanity check compares the copy with the loaded save");
        var corrupted = asOpened.ToArray();
        Gen1SaveCorrupter.ApplyInPlace(corrupted, ofs);
        corrupted.Should().Equal(ReferenceCorrupt(disk, ofs.Party, ofs.ChecksumOfs));
        Diff(corrupted, disk).Should().BeEquivalentTo([ofs.Party, ofs.Party + 1, ofs.ChecksumOfs]);
        Diff(corrupted, handedOver).Should().Contain(ofs.OT, "none of the unsaved edits are in the corrupted bytes");
    }

    [Fact]
    public static void RefusesWrongSize()
    {
        var act = () => Gen1SaveCorrupter.ApplyInPlace(new byte[0x8001], SAV1Offsets.INT);
        act.Should().Throw<ArgumentException>();
        Gen1SaveCorrupter.IsApplied(new byte[0x7FFF], SAV1Offsets.INT).Should().BeFalse();
        Gen1SaveCorrupter.IsChecksumValid(new byte[0x7FFF], SAV1Offsets.INT).Should().BeFalse();
        Gen1SaveCorrupter.GetOffsets(new SAV1(LanguageID.Japanese)).Should().BeSameAs(SAV1Offsets.JPN);
        Gen1SaveCorrupter.GetOffsets(new SAV1(LanguageID.English)).Should().BeSameAs(SAV1Offsets.INT);
    }

    [Theory]
    [InlineData(LanguageID.English, GameVersion.RB)]
    [InlineData(LanguageID.Japanese, GameVersion.YW)]
    public static void CorruptedBytesAreNotDetectedAsASaveFile(LanguageID language, GameVersion version)
    {
        // Measured: SaveUtil's Gen 1 list check requires count <= 20/30, so a 0xFF party count is rejected and
        // GetSaveFile returns null. The front-ends therefore keep the pre-corruption save loaded instead of reloading.
        var corrupted = GetDiskBytes(language, version, 1);
        Gen1SaveCorrupter.ApplyInPlace(corrupted, language == LanguageID.Japanese ? SAV1Offsets.JPN : SAV1Offsets.INT);
        SaveUtil.GetSaveFile(corrupted, "corrupted.sav").Should().BeNull();

        // The constructor still accepts the bytes (measured: PartyCount reports the raw 0xFF count byte, while the
        // unpacked slot buffer is clamped to 6 by PokeList1.Unpack), and the checksum the corrupter wrote is valid.
        var direct = new SAV1(corrupted, language, version);
        direct.ChecksumsValid.Should().BeTrue();
        direct.PartyCount.Should().Be(255);
    }
}
