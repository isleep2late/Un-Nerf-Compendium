using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Intake;
using Xunit;

namespace PKHaX.Hacks.Tests.Intake;

public sealed class HackIntakeTests
{
    private static PatchProbe Probe(PatchKind kind, byte[]? source = null) =>
        new(kind, source?.Length, source is null ? null : IntakeHash.Crc32(source), null, null, []);

    [Fact]
    public void AFormatThatStatesItsSourceIsRefusedOnAWrongBaseWithBothCrcs()
    {
        using var temp = new TempRoot();
        var good = IntakeTestData.Gba();
        var wrong = (byte[])good.Clone();
        wrong[0x30000] ^= 1;
        var patcher = new FakePatcher(Probe(PatchKind.Bps, good), b => b);
        var result = IntakeTestData.Counting(new HackKitStore(temp.Path), []).FromPatch(new byte[4], wrong, patcher, "x.bps");
        Assert.False(result.Ok);
        Assert.Contains(IntakeHash.Crc32(good).ToString("x8"), result.Refusal);
        Assert.Contains(IntakeHash.Crc32(wrong).ToString("x8"), result.Refusal);
    }

    [Fact]
    public void AnIpsResultRecordsThatItChecksNothingAndMustStayARom()
    {
        using var temp = new TempRoot();
        var rom = IntakeTestData.Gba();
        var intake = IntakeTestData.Counting(new HackKitStore(temp.Path), []);
        var ok = intake.FromPatch(new byte[4], rom, new FakePatcher(Probe(PatchKind.Ips), b => { b[0x30000] ^= 1; return b; }), "x.ips");
        Assert.True(ok.Ok, ok.Refusal);
        Assert.Equal(IntakeText.IpsNote, ok.Kit!.Source.Note);
        Assert.Equal("IPS", ok.Kit.Source.PatchFormat);
        var bad = intake.FromPatch(new byte[4], rom, new FakePatcher(Probe(PatchKind.Ips), b => { b[0xB2] = 0; return b; }), "y.ips");
        Assert.Equal(IntakeText.IpsNotARom, bad.Refusal);
    }

    [Fact]
    public void ThePatchedImageIsTheKitKey()
    {
        using var temp = new TempRoot();
        var rom = IntakeTestData.Gba();
        var patched = (byte[])rom.Clone();
        patched[0x30000] ^= 0x5A;
        var result = IntakeTestData.Counting(new HackKitStore(temp.Path), []).FromPatch(new byte[4], rom, new FakePatcher(Probe(PatchKind.Ups, rom), _ => (byte[])patched.Clone()), "x.ups");
        Assert.True(result.Ok, result.Refusal);
        Assert.Equal(IntakeHash.Sha1(patched), result.Kit!.Key);
        Assert.Equal(IntakeHash.Sha1(rom), result.Kit.Source.BaseSha1);
    }

    [Fact]
    public void AProfileWithTablesBecomesAKitAndABundleIsCapped()
    {
        using var temp = new TempRoot();
        var key = IntakeTestData.Key('b');
        var profile = Encoding.UTF8.GetBytes(IntakeTestData.Profile(IntakeTestData.Data(key)));
        var intake = new HackIntake(new HackKitStore(temp.Path));
        var fromJson = intake.FromProfile(profile, "x.pkhaxprofile.json");
        Assert.True(fromJson.Ok, fromJson.Refusal);
        Assert.Equal(key, fromJson.Kit!.Key);
        Assert.True(fromJson.Kit.Source.FromProfile);
        Assert.Equal(SaveShape.UnknownFamily, fromJson.Kit.SaveFamily);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var s = zip.CreateEntry(ProfileBundleReader.PkhaxMember).Open())
                s.Write(profile);
            foreach (var i in Enumerable.Range(0, ProfileBundleReader.MaxMembers + 1))
            {
                using var e = zip.CreateEntry($"derived/{i}.json").Open();
                e.Write("{}"u8);
            }
        }
        var many = ProfileBundleReader.Read(ms.ToArray());
        Assert.False(many.Ok);
        Assert.Contains("members", many.Refusal);
    }

    [Fact]
    public void AProfileDroppedOverARomKitLeavesTheRomKit()
    {
        using var temp = new TempRoot();
        var key = IntakeTestData.Key('8');
        var store = new HackKitStore(temp.Path);
        store.Save(IntakeTestData.Kit(key), IntakeTestData.Data(key), null);
        var result = new HackIntake(store).FromProfile(Encoding.UTF8.GetBytes(IntakeTestData.Profile(IntakeTestData.Data(key))), "x.pkhaxprofile.json");
        Assert.True(result.FromStore);
        Assert.False(store.Load(key)!.Kit.Source.FromProfile);
        Assert.Equal(SaveShape.Retail, store.Load(key)!.Kit.SaveFamily);
    }

    [Fact]
    public void APatchStatingATargetPastTheCapIsRefusedBeforeItIsApplied()
    {
        using var temp = new TempRoot();
        var rom = IntakeTestData.Gba();
        var applied = false;
        var patcher = new FakePatcher(new PatchProbe(PatchKind.Bps, rom.Length, IntakeHash.Crc32(rom), 128L * 1024 * 1024, null, []), b => { applied = true; return b; });
        var result = new HackIntake(new HackKitStore(temp.Path), maxRomBytes: 64L * 1024 * 1024).FromPatch(new byte[4], rom, patcher, "x.bps", Path.Combine(temp.Path, "base.gba"));
        Assert.False(result.Ok);
        Assert.False(applied);
        Assert.False(File.Exists(new HackKitStore(temp.Path).BasesPath));
    }

    [Fact]
    public void ThreeDsImagesAreRefusedWithTheExportSentence()
    {
        using var temp = new TempRoot();
        var ctr = new byte[0x4000];
        "NCSD"u8.CopyTo(ctr.AsSpan(0x100));
        var result = IntakeTestData.Counting(new HackKitStore(temp.Path), []).Derive(ctr, "x.3ds");
        Assert.Equal(IntakeText.CannotRead, result.Refusal);
    }
}
