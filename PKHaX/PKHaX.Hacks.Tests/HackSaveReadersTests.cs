using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHaX.Hacks.Overlay;
using PKHaX.Hacks.Readers;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class HackSaveReadersTests : IDisposable
{
    private static readonly byte[] Signature = "PKHAXTST"u8.ToArray();

    public HackSaveReadersTests() => HackSaveReaders.Register();

    public void Dispose()
    {
        HackSaveReaders.Remove(FakeVariant.Id);
        HackSaveReaders.Unregister();
    }

    private static byte[] BlankEmerald() => TestSaves.Gen3(GameVersion.E);

    [Fact]
    public void RegisterIsIdempotent()
    {
        HackSaveReaders.Register();
        HackSaveReaders.Register();
        Assert.True(HackSaveReaders.IsRegistered);
        Assert.Single(SaveUtil.CustomSaveReaders, r => r.GetType().DeclaringType == typeof(HackSaveReaders));
    }

    [Fact]
    public void VanillaDetectionIsUnchangedWithoutAContext()
    {
        var raw = BlankEmerald();
        var sav = SaveUtil.GetSaveFile((byte[])raw.Clone());
        Assert.IsType<SAV3E>(sav);
        Assert.Equal(raw, sav.Write().ToArray());
    }

    [Fact]
    public void UnknownVariantUnderAContextKeepsTheSaveClosed()
    {
        var context = new HackSaveContext { ProfileId = "unbound-2.1.1.1", Kind = ImageKind.Hack, SaveVariant = "gen3-cfru/1" };
        var ex = Assert.Throws<HackSaveUnavailableException>(() => HackSaveReaders.Open(BlankEmerald(), context));
        Assert.Equal("gen3-cfru/1", ex.SaveVariant);
        Assert.IsType<SAV3E>(SaveUtil.GetSaveFile(BlankEmerald()));
    }

    [Fact]
    public void OpenFileSurfacesTheReasonAndThePathOverloadNeverSeesAContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pkhax-test-{Guid.NewGuid():N}.sav");
        File.WriteAllBytes(path, BlankEmerald());
        try
        {
            var context = new HackSaveContext { ProfileId = "unbound-2.1.1.1", Kind = ImageKind.Hack, SaveVariant = "gen3-cfru/1" };
            SaveFile? opened = null;
            var ex = Assert.Throws<HackSaveUnavailableException>(() => opened = HackSaveReaders.OpenFile(path, context));
            Assert.Equal("unbound-2.1.1.1", ex.ProfileId);
            Assert.Null(opened);
            Assert.True(SaveUtil.TryGetSaveFile(path, out var vanilla));
            Assert.IsType<SAV3E>(vanilla);
            var viaVanilla = HackSaveReaders.OpenFile(path, new HackSaveContext { ProfileId = "emerald", Kind = ImageKind.Vanilla, SaveVariant = HackSaveContext.VanillaContainer });
            Assert.IsType<SAV3E>(viaVanilla);
            Assert.Equal(path, viaVanilla!.Metadata.FilePath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VanillaContainerContextFallsThroughToVanilla()
    {
        var context = new HackSaveContext { ProfileId = "emerald-unnerf", Kind = ImageKind.UnNerf, SaveVariant = HackSaveContext.VanillaContainer };
        Assert.IsType<SAV3E>(HackSaveReaders.Open(BlankEmerald(), context));
        HackSaveReaders.Add(new FakeVariant());
        var signed = BlankEmerald();
        Signature.CopyTo(signed, 0x1F000);
        Assert.IsType<SAV3E>(HackSaveReaders.Open((byte[])signed.Clone(), context));
        Assert.IsType<SAV3FRLG>(HackSaveReaders.Open(signed, null));
    }

    [Fact]
    public void RegisteredVariantAnswersForItsContext()
    {
        HackSaveReaders.Add(new FakeVariant());
        var context = new HackSaveContext { ProfileId = "fake", Kind = ImageKind.Hack, SaveVariant = FakeVariant.Id };
        Assert.IsType<SAV3FRLG>(HackSaveReaders.Open(BlankEmerald(), context));
        Assert.IsType<SAV3E>(SaveUtil.GetSaveFile(BlankEmerald()));
        Assert.Throws<ArgumentException>(() => HackSaveReaders.Add(new FakeVariant()));
    }

    [Fact]
    public void SelfIdentifyingVariantAnswersWithoutAContext()
    {
        HackSaveReaders.Add(new FakeVariant());
        var raw = BlankEmerald();
        Assert.IsType<SAV3E>(SaveUtil.GetSaveFile((byte[])raw.Clone()));
        Signature.CopyTo(raw, 0x1F000);
        Assert.IsType<SAV3FRLG>(SaveUtil.GetSaveFile(raw));
    }

    [Fact]
    public async Task AHackOpenInFlightNeverReachesAnotherThreadsOpen()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        HackSaveReaders.Add(new FakeVariant(entered, release));
        var context = new HackSaveContext { ProfileId = "fake", Kind = ImageKind.Hack, SaveVariant = FakeVariant.Id };
        var hackOpen = Task.Run(() => HackSaveReaders.Open(BlankEmerald(), context));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(30)));
        var other = await Task.Run(() => SaveUtil.GetSaveFile(BlankEmerald()));
        release.Set();
        Assert.IsType<SAV3E>(other);
        Assert.IsType<SAV3FRLG>(await hackOpen);
    }

    private sealed class FakeVariant(ManualResetEventSlim? entered = null, ManualResetEventSlim? release = null) : IHackSaveVariantReader
    {
        public const string Id = "test-fake/1";
        public string VariantId => Id;
        public bool IsRecognized(long dataLength) => dataLength == SaveUtil.SIZE_G3RAW;
        public bool IsSelfIdentifying(ReadOnlySpan<byte> data) => data.Slice(0x1F000, Signature.Length).SequenceEqual(Signature);

        public bool TryRead(Memory<byte> data, HackSaveContext? context, [NotNullWhen(true)] out SaveFile? result)
        {
            entered?.Set();
            release?.Wait(TimeSpan.FromSeconds(30));
            result = new SAV3FRLG();
            return true;
        }
    }
}
