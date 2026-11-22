using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using PKHaX.Hacks.Intake;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests.Intake;

public sealed class DropClassifierTests
{
    private static DropKind Kind(byte[] bytes, string name) => DropClassifier.Classify(bytes, bytes.Length, name).Kind;

    [Fact]
    public void PatchesAreKnownByTheirMagicNotTheirName()
    {
        Assert.Equal(DropKind.Patch, Kind([.. "PATCH"u8.ToArray(), 0, 0, 1, 0, 1, 7, .. "EOF"u8.ToArray()], "x.gba"));
        Assert.Equal(DropKind.Patch, Kind([.. "IPS32"u8.ToArray(), .. "EEOF"u8.ToArray()], "x.sav"));
        Assert.Equal(DropKind.Patch, Kind([.. "UPS1"u8.ToArray(), 1, 2, 3], "x.nds"));
        Assert.Equal(DropKind.Patch, Kind([.. "BPS1"u8.ToArray(), 1, 2, 3], "x.gba"));
        Assert.Equal(DropKind.Patch, Kind([0xD6, 0xC3, 0xC4, 0x00, 0x00], "x.bin"));
    }

    [Fact]
    public void RomsAreKnownByTheirHeaders()
    {
        var gba = IntakeTestData.Gba();
        Assert.Equal(DropKind.GbaRom, Kind(gba, "x.sav"));
        gba[0xBD] ^= 0xFF;
        Assert.NotEqual(DropKind.GbaRom, Kind(gba, "x.gba"));

        var gb = new byte[0x8000];
        "SYNTH"u8.CopyTo(gb.AsSpan(0x134));
        byte sum = 0;
        for (var i = 0x134; i <= 0x14C; i++)
            sum = (byte)(sum - gb[i] - 1);
        gb[0x14D] = sum;
        Assert.Equal(DropKind.GbRom, Kind(gb, "x.bin"));

        var nds = new byte[0x8000];
        "CPUE"u8.CopyTo(nds.AsSpan(0x0C));
        BinaryPrimitives.WriteUInt32LittleEndian(nds.AsSpan(0x40), 0x4000);
        BinaryPrimitives.WriteUInt32LittleEndian(nds.AsSpan(0x44), 8);
        BinaryPrimitives.WriteUInt32LittleEndian(nds.AsSpan(0x48), 0x4100);
        BinaryPrimitives.WriteUInt32LittleEndian(nds.AsSpan(0x4C), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(nds.AsSpan(0x15E), DropClassifier.Crc16(nds.AsSpan(0, 0x15E)));
        Assert.Equal(DropKind.NdsRom, Kind(nds, "x.bin"));

        var ctr = new byte[0x4000];
        "NCSD"u8.CopyTo(ctr.AsSpan(0x100));
        Assert.Equal(DropKind.CtrImage, Kind(ctr, "x.bin"));
    }

    [Fact]
    public void ProfilesBundlesAndSavesAreKnown()
    {
        var profile = Encoding.UTF8.GetBytes(IntakeTestData.Profile(IntakeTestData.Data(IntakeTestData.Key('a'))));
        Assert.Equal(DropKind.Profile, Kind(profile, "x.txt"));
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var s = zip.CreateEntry(ProfileBundleReader.PkhaxMember).Open();
            s.Write(profile);
        }
        Assert.Equal(DropKind.Bundle, Kind(ms.ToArray(), "x.bin"));
        Assert.Equal(DropKind.Save, Kind(TestSaves.Gen3(GameVersion.E), "x.bin"));
        Assert.Equal(DropKind.Save, Kind(Saves.Gen3ContainerTests.Cfru(0x08012025, 5), "x.bin"));
    }

    [Fact]
    public void NoiseIsUnknownWithASentence()
    {
        var noise = new byte[1000];
        new Random(9).NextBytes(noise);
        var verdict = DropClassifier.Classify(noise, noise.Length, "noise.bin");
        Assert.Equal(DropKind.Unknown, verdict.Kind);
        Assert.Contains("BPS1", verdict.Sentence);
        Assert.Contains("pkhax-profile/1", verdict.Sentence);
    }
}
