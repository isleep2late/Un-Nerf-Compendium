using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Intake;
using PKHaX.Hacks.Profiles;
using Xunit;

namespace PKHaX.Hacks.Tests.Intake;

public sealed class HackKitStoreTests
{
    [Fact]
    public void AStoredKitLoadsBackEqualAndASecondDeriveRunsNoReader()
    {
        using var temp = new TempRoot();
        var store = new HackKitStore(temp.Path);
        var calls = new List<string>();
        var rom = IntakeTestData.Gba();
        var first = IntakeTestData.Counting(store, calls).Derive(rom, "synthetic.gba");
        Assert.True(first.Ok, first.Refusal);
        var loaded = store.Load(first.Kit!.Key);
        Assert.NotNull(loaded);
        Assert.Equal(first.Kit.ToJson(), loaded!.Kit.ToJson());
        var again = IntakeTestData.Counting(store, calls).Derive(rom, "synthetic.gba");
        Assert.True(again.FromStore);
        Assert.Single(calls);
    }

    [Fact]
    public void AStoreHitRemembersTheRomPathTheKitWasMissing()
    {
        using var temp = new TempRoot();
        var store = new HackKitStore(temp.Path);
        var calls = new List<string>();
        var rom = IntakeTestData.Gba();
        Assert.True(IntakeTestData.Counting(store, calls).Derive(rom, "synthetic.gba").Ok);
        var path = Path.Combine(temp.Path, "synthetic.gba");
        var again = IntakeTestData.Counting(store, calls).Derive(rom, "synthetic.gba", romPath: path);
        Assert.True(again.FromStore);
        Assert.Single(calls);
        Assert.Equal(path, store.Load(again.Kit!.Key)!.Kit.Source.RomPath);
    }

    [Fact]
    public void NoFileUnderTheStoreHoldsTheRom()
    {
        using var temp = new TempRoot();
        var store = new HackKitStore(temp.Path);
        var rom = IntakeTestData.Gba();
        Assert.True(IntakeTestData.Counting(store, []).Derive(rom, "synthetic.gba").Ok);
        foreach (var file in Directory.EnumerateFiles(temp.Path, "*", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(file);
            for (var at = 0; at + 256 <= rom.Length; at += 128)
                Assert.True(bytes.AsSpan().IndexOf(rom.AsSpan(at, 256)) < 0, $"{file} holds ROM bytes from 0x{at:X}");
        }
    }

    [Fact]
    public void AnOlderDeriveVersionIsListedStale()
    {
        using var temp = new TempRoot();
        var store = new HackKitStore(temp.Path);
        var key = IntakeTestData.Key('c');
        store.Save(IntakeTestData.Kit(key, deriveVersion: HackKit.CurrentDeriveVersion - 1), IntakeTestData.Data(key), null);
        var kit = Assert.Single(store.List());
        Assert.False(kit.Usable);
        Assert.Equal(IntakeText.StaleKit, kit.Problem);
        Assert.Null(store.Usable(key));
    }

    [Fact]
    public void ForgetRemovesTheFolderAndItsPairingsOnly()
    {
        using var temp = new TempRoot();
        var store = new HackKitStore(temp.Path);
        var key = IntakeTestData.Key('d');
        var kit = store.Save(IntakeTestData.Kit(key), IntakeTestData.Data(key), null);
        var pairings = new PairingMemory(store.PairingsPath);
        pairings.Remember(Path.Combine(temp.Path, "a.sav"), null, kit.Kit.ProfileId);
        pairings.Remember(Path.Combine(temp.Path, "b.sav"), null, "other-profile");
        Assert.True(store.Forget(key, pairings));
        Assert.False(Directory.Exists(store.DirFor(key)));
        var reread = new PairingMemory(store.PairingsPath);
        Assert.DoesNotContain(reread.Entries, e => e.ProfileId == kit.Kit.ProfileId);
        Assert.Contains(reread.Entries, e => e.ProfileId == "other-profile");
    }

    [Fact]
    public void BasesKeepPathsAndHashesOnly()
    {
        using var temp = new TempRoot();
        var store = new HackKitStore(temp.Path);
        var path = Path.Combine(temp.Path, "base.gba");
        var rom = IntakeTestData.Gba();
        File.WriteAllBytes(path, rom);
        store.RememberBase(new KitBase(path, rom.Length, IntakeHash.Crc32(rom), IntakeHash.Sha1(rom)));
        var found = store.FindBase(IntakeHash.Crc32(rom), rom.Length);
        Assert.NotNull(found);
        Assert.Equal(IntakeHash.Sha1(rom), found!.Sha1);
        Assert.True(new FileInfo(store.BasesPath).Length < 1024);
    }

    [Fact]
    public void TheKitProfileLoadsAsAnImportedProfile()
    {
        var profile = HackKitStore.ProfileFor(IntakeTestData.Kit(IntakeTestData.Key('e')));
        Assert.NotNull(profile);
        Assert.Equal("imported", profile!.Kind);
        Assert.Equal("Unproven", profile.WriteEvidence);
        Assert.True(profile.Matches(IntakeTestData.Key('e')));
        Assert.Equal(IntakeTestData.Key('e'), HackOpen.KeyOfProfile(profile));
    }

    [Fact]
    public void AnExportedProfileCarriesItsTablesIntoASecondStore()
    {
        using var first = new TempRoot();
        using var second = new TempRoot();
        var store = new HackKitStore(first.Path);
        var key = IntakeTestData.Key('a');
        var data = IntakeTestData.Data(key, unmapped: true);
        store.Save(IntakeTestData.Kit(key), data, null);
        var json = store.ExportProfile(key);
        Assert.NotNull(json);
        var profile = PkhaxProfileReader.Load(json!).Profile;
        Assert.NotNull(profile);
        Assert.Equal("embedded", (string?)profile!.Data?["source"]);
        Assert.Equal(key, profile.DataRomSha1);
        Assert.True(profile.HasEmbeddedData);
        var other = new HackKitStore(second.Path);
        var read = new HackIntake(other).FromProfile(System.Text.Encoding.UTF8.GetBytes(json!), "exported" + PkhaxProfile.FileSuffix);
        Assert.True(read.Ok, read.Refusal);
        Assert.False(read.FromStore);
        var stored = other.Usable(key);
        Assert.NotNull(stored);
        Assert.True(stored!.Kit.Source.FromProfile);
        Assert.Equal(data.Counts, stored.Data!.Counts);
        Assert.Equal(data.UnmappedSpecies, stored.Data.UnmappedSpecies);
        Assert.Equal(data.Species.Select(s => s.Name), stored.Data.Species.Select(s => s.Name));
        Assert.Equal(store.Load(key)!.Kit.Name, stored.Kit.Name);
    }

    [Fact]
    public void NoProfileIsExportedForAMissingStaleOrBadKey()
    {
        using var temp = new TempRoot();
        var store = new HackKitStore(temp.Path);
        var stale = IntakeTestData.Key('b');
        store.Save(IntakeTestData.Kit(stale, deriveVersion: HackKit.CurrentDeriveVersion - 1), IntakeTestData.Data(stale), null);
        Assert.Null(store.ExportProfile(stale));
        Assert.Null(store.ExportProfile(IntakeTestData.Key('c')));
        Assert.Null(store.ExportProfile("not-a-key"));
    }
}
