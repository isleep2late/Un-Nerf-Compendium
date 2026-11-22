using System;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Intake;
using PKHaX.Hacks.Profiles;
using Xunit;

namespace PKHaX.Hacks.Tests.Intake;

public sealed class WantedSignatureTests
{
    private const uint Second = 0x01121999;

    private static (HackKitStore Store, string Key, byte[] Rom) Kit(string root, bool withWord = true, string code = "BPEE", HackKitStore? into = null)
    {
        var rom = SaveShapeTests.Rom(rhh: true, code: code, wordAt0x300: withWord ? Second : null);
        var shape = Gen3ShapeFinder.Derive(new GbaImage(rom));
        Assert.True(shape.Known, shape.Reason);
        Assert.DoesNotContain(Second, shape.Signatures);
        var key = shape.RomSha1;
        var store = into ?? new HackKitStore(root);
        store.Save(IntakeTestData.Kit(key, SaveShape.Expansion), IntakeTestData.Data(key), shape);
        return (store, key, rom);
    }

    private static byte[] SecondFile() => ShapeSynth.File(0xF80, true, [(1, 0), (5, 3)], signature: Second);

    private static OpenHint Answer(string key) => new() { KitKey = key, Answered = true };

    [Fact]
    public void ARefusalForAnUnlistedSignatureWithNoRomAtHandRecordsTheWantedValue()
    {
        using var temp = new TempRoot();
        var (store, key, _) = Kit(temp.Path);
        var file = SecondFile();
        try
        {
            using (var r = HackOpen.Open(file, null, store, null, Answer(key)))
            {
                Assert.Equal(OpenKind.Refused, r.Kind);
                Assert.Contains("0x01121999", r.Sentence);
                Assert.Contains(IntakeText.SignatureWanted(Second), r.Sentence);
            }
            Assert.Equal(new[] { Second }, store.WantedSignatures(key));
            Assert.DoesNotContain(Second, store.Load(key)!.Shape!.Signatures);
            using (HackOpen.Open(file, null, store, null, Answer(key)))
            {
            }
            Assert.Equal(new[] { Second }, store.WantedSignatures(key));
            Assert.True(new FileInfo(store.WantedPath(key)).Length < 512);
            for (uint i = 1; i <= 10; i++)
                Assert.True(store.WantSignature(key, 0x0F00_0000 + i));
            Assert.Equal(HackKitStore.MaxWanted, store.WantedSignatures(key).Count);
            Assert.DoesNotContain(Second, store.WantedSignatures(key));
            Assert.False(store.WantSignature(IntakeTestData.Key('9'), Second));
            Assert.Empty(store.WantedSignatures(IntakeTestData.Key('9')));
            Assert.False(store.WantSignature(key, 0));
            Assert.False(store.WantSignature(key, uint.MaxValue));
        }
        finally
        {
            HackOpen.Close();
        }
    }

    [Fact]
    public void AFileOfTheWrongSizeAsksForNoSignature()
    {
        using var temp = new TempRoot();
        var (store, key, _) = Kit(temp.Path);
        var shape = store.Load(key)!.Shape!;
        byte[] file = SecondFile();
        Assert.Equal(Second, Gen3ShapeFinder.UnlistedSignature(shape, file));
        byte[] longer = [.. file, .. new byte[0x100]];
        Assert.Null(Gen3ShapeFinder.UnlistedSignature(shape, longer));
        byte[] footed = [.. file, .. new byte[0x10]];
        Assert.Equal(Second, Gen3ShapeFinder.UnlistedSignature(shape, footed));
    }

    [Fact]
    public void ARetailSignedSaveRecordsNothing()
    {
        using var temp = new TempRoot();
        var (store, key, _) = Kit(temp.Path);
        try
        {
            using (var r = HackOpen.Open(ShapeSynth.File(0xF80, true, [(1, 0)]), null, store, null, Answer(key)))
                Assert.Equal(OpenKind.Native, r.Kind);
            Assert.Empty(store.WantedSignatures(key));
            Assert.False(File.Exists(store.WantedPath(key)));
        }
        finally
        {
            HackOpen.Close();
        }
    }

    [Fact]
    public void AddingTheSameRomAgainTakesTheWantedSignatureAndTheSaveOpensAsWithTheRomAtHand()
    {
        using var temp = new TempRoot();
        var (store, key, rom) = Kit(Path.Combine(temp.Path, "learn"));
        var (direct, directKey, _) = Kit(Path.Combine(temp.Path, "direct"));
        Assert.Equal(key, directKey);
        var file = SecondFile();
        try
        {
            string? directRefusal;
            bool directWrite;
            string[] directItems;
            using (var r = HackOpen.Open(file, null, direct, null, Answer(key) with { Rom = rom }))
            {
                Assert.Equal(OpenKind.Native, r.Kind);
                directRefusal = r.WriteRefusal;
                directWrite = r.CanWrite;
                directItems = r.Gate!.Items.Select(i => $"{i.Number}:{i.Passed}").ToArray();
            }
            Assert.Empty(direct.WantedSignatures(key));

            using (var r = HackOpen.Open(file, null, store, null, Answer(key)))
                Assert.Equal(OpenKind.Refused, r.Kind);
            Assert.Equal(new[] { Second }, store.WantedSignatures(key));

            var intake = new HackIntake(store);
            var result = intake.Derive(rom, "synthetic.gba");
            Assert.True(result.Ok, result.Sentence);
            Assert.True(result.FromStore);
            Assert.Equal(0, intake.ReaderCalls);
            Assert.Contains(IntakeText.SignatureFound(Second), result.Notes);
            Assert.Contains(Second, result.Stored!.Shape!.Signatures);
            Assert.Empty(store.WantedSignatures(key));
            Assert.False(File.Exists(store.WantedPath(key)));
            Assert.Contains(Second, store.Load(key)!.Shape!.Signatures);

            using (var r = HackOpen.Open(file, null, store, null, Answer(key)))
            {
                Assert.Equal(OpenKind.Native, r.Kind);
                Assert.Equal(ResolveHow.Answer, r.How);
                Assert.Equal(directRefusal, r.WriteRefusal);
                Assert.Equal(directWrite, r.CanWrite);
                Assert.Equal(directItems, r.Gate!.Items.Select(i => $"{i.Number}:{i.Passed}").ToArray());
            }
            Assert.Empty(store.WantedSignatures(key));

            var again = new HackIntake(store).Derive(rom, "synthetic.gba");
            Assert.True(again.Ok, again.Sentence);
            Assert.DoesNotContain(again.Notes, n => n.Contains("0x01121999", StringComparison.Ordinal));
        }
        finally
        {
            HackOpen.Close();
        }
    }

    [Fact]
    public void AValueTheRomDoesNotHoldIsDroppedAndTheSaveStaysRefused()
    {
        using var temp = new TempRoot();
        var (store, key, rom) = Kit(temp.Path, withWord: false);
        var file = SecondFile();
        try
        {
            using (var r = HackOpen.Open(file, null, store, null, Answer(key)))
                Assert.Equal(OpenKind.Refused, r.Kind);
            Assert.Equal(new[] { Second }, store.WantedSignatures(key));

            var result = new HackIntake(store).Derive(rom, "synthetic.gba");
            Assert.True(result.Ok, result.Sentence);
            var dropped = Assert.Single(result.Notes, n => n.StartsWith("The signature 0x01121999 a save asked for was not taken from this ROM", StringComparison.Ordinal));
            Assert.Contains("word-aligned", dropped);
            Assert.DoesNotContain(IntakeText.SignatureFound(Second), result.Notes);
            Assert.Empty(store.WantedSignatures(key));
            Assert.Equal(new[] { Second }, store.RefusedSignatures(key));
            Assert.DoesNotContain(Second, store.Load(key)!.Shape!.Signatures);

            using (var r = HackOpen.Open(file, null, store, null, Answer(key)))
            {
                Assert.Equal(OpenKind.Refused, r.Kind);
                Assert.False(r.CanWrite);
                Assert.DoesNotContain(IntakeText.SignatureWanted(Second), r.Sentence);
                Assert.Contains(IntakeText.SignatureAbsent(Second), r.Sentence);
            }
            Assert.Empty(store.WantedSignatures(key));
            Assert.False(store.WantSignature(key, Second));
            var quiet = new HackIntake(store).Derive(rom, "synthetic.gba");
            Assert.DoesNotContain(quiet.Notes, n => n.Contains("0x01121999", StringComparison.Ordinal));
            using (var r = HackOpen.Open(file, null, store, null, Answer(key) with { Rom = rom }))
                Assert.Equal(OpenKind.Refused, r.Kind);
        }
        finally
        {
            HackOpen.Close();
        }
    }

    [Fact]
    public void AnotherRomDoesNotTakeTheRecord()
    {
        using var temp = new TempRoot();
        var (store, key, rom) = Kit(temp.Path);
        var (_, otherKey, otherRom) = Kit(temp.Path, code: "BPEF", into: store);
        Assert.NotEqual(key, otherKey);
        var file = SecondFile();
        try
        {
            using (var r = HackOpen.Open(file, null, store, null, Answer(key) with { Rom = otherRom }))
                Assert.Equal(OpenKind.Refused, r.Kind);
            Assert.Equal(new[] { Second }, store.WantedSignatures(key));
            Assert.Empty(store.WantedSignatures(otherKey));

            var other = new HackIntake(store).Derive(otherRom, "other.gba");
            Assert.True(other.Ok, other.Sentence);
            Assert.Equal(otherKey, other.Kit!.Key);
            Assert.DoesNotContain(other.Notes, n => n.Contains("0x01121999", StringComparison.Ordinal));
            Assert.Equal(new[] { Second }, store.WantedSignatures(key));
            Assert.DoesNotContain(Second, store.Load(key)!.Shape!.Signatures);
            Assert.DoesNotContain(Second, store.Load(otherKey)!.Shape!.Signatures);

            var same = new HackIntake(store).Derive(rom, "synthetic.gba");
            Assert.Contains(IntakeText.SignatureFound(Second), same.Notes);
            using var opened = HackOpen.Open(file, null, store, null, Answer(key));
            Assert.Equal(OpenKind.Native, opened.Kind);
        }
        finally
        {
            HackOpen.Close();
        }
    }
}
