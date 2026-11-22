using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Profiles;
using Xunit;

namespace PKHaX.Hacks.Tests;

public sealed class SaveProfileResolverTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "pkhax-resolver-" + Guid.NewGuid().ToString("N"));

    public SaveProfileResolverTests() => Directory.CreateDirectory(dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(dir, true);
        }
        catch (IOException)
        {
        }
    }

    private static readonly byte[] RomA = Enumerable.Range(0, 4096).Select(i => (byte)i).ToArray();
    private static readonly byte[] RomB = Enumerable.Range(0, 4096).Select(i => (byte)(i * 7)).ToArray();
    private static string Sha1(byte[] b) => Convert.ToHexStringLower(SHA1.HashData(b));

    private static JsonObject Profile(string id, string sha1, params string[] signatures)
    {
        var o = PkhaxProfileTests.HeartSoul();
        o["id"] = id;
        o["match"]![0]!["sha1"] = sha1;
        o["data"]!["romSha1"] = sha1;
        if (signatures.Length > 0)
            o["save"]!["container"]!["signatures"] = new JsonArray(signatures.Select(s => (JsonNode)JsonValue.Create(s)!).ToArray());
        return o;
    }

    private static ProfileStore Store(params JsonObject[] profiles) =>
        ProfileStore.Create(profiles.Select((p, i) => new ProfileDocument($"test:{i}", p.ToJsonString())));

    private static byte[] Gen3Save(uint signature)
    {
        var b = new byte[0x20000];
        for (var at = 0; at < b.Length; at += 0x1000)
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at + 0xFF8), signature);
        return b;
    }

    private string File(string name, byte[] bytes)
    {
        var p = Path.Combine(dir, name);
        System.IO.File.WriteAllBytes(p, bytes);
        return p;
    }

    private static readonly SaveTrainer Ash = new(12345, "ASH");

    [Fact]
    public void TheHintWinsOverEveryOtherStep()
    {
        var a = Profile("hack-a", Sha1(RomA), "0x01121999");
        var b = Profile("hack-b", Sha1(RomB));
        var store = Store(a, b);
        var save = File("game.sav", Gen3Save(0x01121999));
        var r = new SaveProfileResolver(store).Resolve(System.IO.File.ReadAllBytes(save), save, new ProfileHint(ImageKey: Sha1(RomB)));
        Assert.Equal(ResolveHow.Hint, r.How);
        Assert.Equal("hack-b", r.Profile!.Id);
        Assert.Equal("hack-b", new SaveProfileResolver(store).Resolve(Gen3Save(0x01121999), save, new ProfileHint(ProfileId: "hack-b")).Profile!.Id);
        var unknown = new SaveProfileResolver(store).Resolve(Gen3Save(0x01121999), save, new ProfileHint(ImageKey: new string('0', 40)));
        Assert.Equal(ResolveHow.Signature, unknown.How);
        Assert.Contains(unknown.Trail, t => t.Contains("no stored profile"));
    }

    [Fact]
    public void TheUnboundSignatureFindsItsProfileAndTheRomBreaksATie()
    {
        var a = Profile("unbound-a", Sha1(RomA), "0x01121999");
        var b = Profile("unbound-b", Sha1(RomB), "0x01121999");
        var save = File("unbound.sav", Gen3Save(0x01121999));
        var single = new SaveProfileResolver(Store(a)).Resolve(Gen3Save(0x01121999), save);
        Assert.Equal((ResolveHow.Signature, "unbound-a"), (single.How, single.Profile!.Id));
        var tie = new SaveProfileResolver(Store(a, b)).Resolve(Gen3Save(0x01121999), save);
        Assert.Equal(ResolveHow.Ask, tie.How);
        Assert.Equal(["unbound-a", "unbound-b"], tie.Alternatives.Select(p => p.Id).Order());
        File("unbound.gba", RomB);
        var broken = new SaveProfileResolver(Store(a, b)).Resolve(Gen3Save(0x01121999), save);
        Assert.Equal((ResolveHow.Signature, "unbound-b"), (broken.How, broken.Profile!.Id));
        Assert.Equal("unbound-a", Assert.Single(broken.Alternatives).Id);
    }

    [Fact]
    public void ASingularSignatureCountsLikeTheList()
    {
        var single = Profile("unbound-single", Sha1(RomA));
        single["save"]!["container"]!["signature"] = "0x01121999";
        var both = Profile("unbound-both", Sha1(RomB), "0x01121999");
        both["save"]!["container"]!["signature"] = "0x01121999";
        var store = Store(single);
        Assert.Equal([0x01121999u], ProfileStore.SaveSignatures(store.ById("unbound-single")!));
        Assert.Equal((ResolveHow.Signature, "unbound-single"), (new SaveProfileResolver(store).Resolve(Gen3Save(0x01121999), null) is var r ? (r.How, r.Profile!.Id) : default));
        Assert.Equal([0x01121999u], ProfileStore.SaveSignatures(Store(both).ById("unbound-both")!));
        var bad = Profile("unbound-bad", Sha1(RomA));
        bad["save"]!["container"]!["signature"] = "01121999";
        var badProfile = Store(bad).ById("unbound-bad")!;
        Assert.Empty(ProfileStore.SaveSignatures(badProfile));
        Assert.Equal(["01121999"], ProfileStore.DeclaredSaveSignatures(badProfile));
    }

    [Fact]
    public void TheVanillaSignatureNeverPicksAProfile()
    {
        var a = Profile("hack-a", Sha1(RomA), "0x08012025");
        var r = new SaveProfileResolver(Store(a)).Resolve(Gen3Save(0x08012025), File("x.sav", Gen3Save(0x08012025)));
        Assert.Equal(ResolveHow.Ask, r.How);
        Assert.Null(r.Profile);
        Assert.Equal(SaveResolution.Question, r.Trail[^1]);
    }

    [Fact]
    public void TheSiblingRomIsHashedOnceAndLookedUp()
    {
        var store = Store(Profile("hack-a", Sha1(RomA)), Profile("hack-b", Sha1(RomB)));
        var save = File("Heart Soul.sav", new byte[0x20000]);
        File("Heart Soul.gba", RomA);
        File("Heart Soul.nds", RomB);
        var resolver = new SaveProfileResolver(store);
        var r = resolver.Resolve(new byte[0x20000], save);
        Assert.Equal((ResolveHow.SiblingRom, "hack-a"), (r.How, r.Profile!.Id));
        resolver.Resolve(new byte[0x20000], save);
        Assert.Equal(1, resolver.HashesComputed);
        var other = File("Heart Soul.gba", RomB);
        System.IO.File.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddMinutes(5));
        Assert.Equal("hack-b", resolver.Resolve(new byte[0x20000], save).Profile!.Id);
        Assert.Equal(2, resolver.HashesComputed);
        var viaHint = new SaveProfileResolver(store).Resolve(new byte[16], File("elsewhere.sav", new byte[16]), new ProfileHint(RomPath: Path.Combine(dir, "Heart Soul.nds")));
        Assert.Equal("hack-b", viaHint.Profile!.Id);
    }

    [Fact]
    public void AnUnknownSiblingRomFallsThroughToTheProfileFile()
    {
        var save = File("game.sav", new byte[0x20000]);
        File("game.gba", new byte[] { 1, 2, 3 });
        var r0 = new SaveProfileResolver(Store()).Resolve(new byte[0x20000], save);
        Assert.Contains(r0.Trail, t => t.Contains("no profile (Analyse ROM"));
        File("game" + PkhaxProfile.FileSuffix, System.Text.Encoding.UTF8.GetBytes(Profile("from-file", Sha1(RomA)).ToJsonString()));
        var r = new SaveProfileResolver(Store()).Resolve(new byte[0x20000], save);
        Assert.Equal((ResolveHow.ProfileFile, "from-file"), (r.How, r.Profile!.Id));
        File("bad" + PkhaxProfile.FileSuffix, "{\"schema\":\"pkhax-profile/1\"}"u8.ToArray());
        var picked = new SaveProfileResolver(Store()).Resolve(new byte[16], File("other.sav", new byte[16]), pickedProfileFile: Path.Combine(dir, "bad" + PkhaxProfile.FileSuffix));
        Assert.Equal(ResolveHow.Ask, picked.How);
        Assert.Contains(picked.Trail, t => t.Contains("refused"));
    }

    [Fact]
    public void TheAnswerIsRememberedByPathAndByTrainerAcrossSessions()
    {
        var memoryFile = Path.Combine(dir, "pkhax", PairingMemory.FileName);
        var store = Store(Profile("hack-a", Sha1(RomA)), Profile("hack-b", Sha1(RomB)));
        var save = File("x.sav", new byte[0x20000]);
        var first = new SaveProfileResolver(store, new PairingMemory(memoryFile));
        Assert.Equal(ResolveHow.Ask, first.Resolve(new byte[0x20000], save, trainer: Ash).How);
        first.Remember(save, Ash, store.ById("hack-a")!);
        Assert.True(System.IO.File.Exists(memoryFile));
        var second = new SaveProfileResolver(store, new PairingMemory(memoryFile));
        Assert.Equal((ResolveHow.Pairing, "hack-a"), (second.Resolve(new byte[0x20000], save).How, second.Resolve(new byte[0x20000], save).Profile!.Id));
        var moved = File("moved.sav", new byte[0x20000]);
        Assert.Equal("hack-a", second.Resolve(new byte[0x20000], moved, trainer: Ash).Profile!.Id);
        Assert.Equal(ResolveHow.Ask, second.Resolve(new byte[0x20000], moved, trainer: new SaveTrainer(12345, "GARY")).How);
        var memory = new PairingMemory(memoryFile);
        memory.Remember(Path.Combine(dir, "y.sav"), Ash, "hack-b");
        var tie = new SaveProfileResolver(store, memory).Resolve(new byte[0x20000], moved, trainer: Ash);
        Assert.Equal(ResolveHow.Ask, tie.How);
        Assert.Equal(["hack-a", "hack-b"], tie.Alternatives.Select(p => p.Id).Order());
    }

    [Fact]
    public void APairingToAProfileThatIsGoneIsNotUsed()
    {
        var memoryFile = Path.Combine(dir, PairingMemory.FileName);
        var save = File("x.sav", new byte[16]);
        new PairingMemory(memoryFile).Remember(save, null, "deleted-profile");
        var r = new SaveProfileResolver(Store(Profile("hack-a", Sha1(RomA))), new PairingMemory(memoryFile)).Resolve(new byte[16], save);
        Assert.Equal(ResolveHow.Ask, r.How);
        Assert.Contains(r.Trail, t => t.Contains("no longer stored"));
    }

    [Fact]
    public void AnUnreadableOrNewerPairingFileIsLeftAsItIs()
    {
        foreach (var text in new[] { "{ not json", "{\"schema\":\"pkhax-pairings/2\",\"pairings\":[{\"savePath\":\"/x\",\"profileId\":\"a\"}]}", "{\"schema\":\"pkhax-pairings/1\",\"schema\":\"x\",\"pairings\":[]}" })
        {
            var memoryFile = File(PairingMemory.FileName, System.Text.Encoding.UTF8.GetBytes(text));
            var memory = new PairingMemory(memoryFile);
            Assert.NotNull(memory.LoadProblem);
            Assert.False(memory.Remember(Path.Combine(dir, "a.sav"), Ash, "hack-a"));
            Assert.Equal("hack-a", memory.ByPath(Path.Combine(dir, "a.sav"))!.ProfileId);
            Assert.Equal(text, System.IO.File.ReadAllText(memoryFile));
        }
    }

    [Fact]
    public void AWriteThatFailsKeepsTheAnswerForTheSessionAndDoesNotThrow()
    {
        var blocker = File("blocker", [1]);
        var memory = new PairingMemory(Path.Combine(blocker, PairingMemory.FileName));
        Assert.False(memory.Remember(Path.Combine(dir, "a.sav"), Ash, "hack-a"));
        Assert.NotNull(memory.SaveProblem);
        Assert.NotNull(memory.ByPath(Path.Combine(dir, "a.sav")));
    }

    [Fact]
    public void APathPairingForAnotherTrainerIsNotUsed()
    {
        var memoryFile = Path.Combine(dir, PairingMemory.FileName);
        var save = File("x.sav", new byte[16]);
        new PairingMemory(memoryFile).Remember(save, Ash, "hack-a");
        var store = Store(Profile("hack-a", Sha1(RomA)));
        var other = new SaveProfileResolver(store, new PairingMemory(memoryFile)).Resolve(new byte[16], save, trainer: new SaveTrainer(999, "RED"));
        Assert.Equal(ResolveHow.Ask, other.How);
        Assert.Contains(other.Trail, t => t.Contains("not used"));
        Assert.Equal(ResolveHow.Pairing, new SaveProfileResolver(store, new PairingMemory(memoryFile)).Resolve(new byte[16], save, trainer: Ash).How);
        Assert.Equal(ResolveHow.Pairing, new SaveProfileResolver(store, new PairingMemory(memoryFile)).Resolve(new byte[16], save).How);
    }

    [Fact]
    public void AKnownImageWithoutAProfileTurnsOffTrainerPairings()
    {
        var memory = new PairingMemory(Path.Combine(dir, PairingMemory.FileName));
        memory.Remember(Path.Combine(dir, "elsewhere.sav"), Ash, "hack-a");
        var resolver = new SaveProfileResolver(Store(Profile("hack-a", Sha1(RomA))), memory);
        Assert.Equal(ResolveHow.Pairing, resolver.Resolve(new byte[16], null, trainer: Ash).How);
        var known = resolver.Resolve(new byte[16], null, new ProfileHint(ImageKey: Sha1(RomB)), Ash);
        Assert.Equal(ResolveHow.Ask, known.How);
        Assert.Contains(known.Trail, t => t.Contains("without trainer pairings"));
    }

    [Fact]
    public void AProfileThatDoesNotFitTheSaveIsNeverPicked()
    {
        var store = Store(Profile("hack-a", Sha1(RomA), "0x01121999"));
        var r = new SaveProfileResolver(store).Resolve(Gen3Save(0x01121999), null, new ProfileHint(ProfileId: "hack-a"), fits: p => p.Platform == "nds");
        Assert.Equal(ResolveHow.Ask, r.How);
        Assert.Equal(2, r.Trail.Count(t => t.Contains("does not fit")));
    }

    [Fact]
    public void OnlyAGen3SizedSaveWithAWholeSlotOfOneSignatureCounts()
    {
        var ds = new byte[0x80000];
        for (var at = 0; at < ds.Length; at += 0x1000)
            BinaryPrimitives.WriteUInt32LittleEndian(ds.AsSpan(at + 0xFF8), 0x01121999);
        Assert.Empty(SaveProfileResolver.Gen3Signatures(ds));
        var one = new byte[0x20000];
        BinaryPrimitives.WriteUInt32LittleEndian(one.AsSpan(0xFF8), 0x01121999);
        Assert.Empty(SaveProfileResolver.Gen3Signatures(one));
        var rtc = new byte[0x20010];
        Gen3Save(0x01121999).CopyTo(rtc, 0);
        Assert.Equal([0x01121999u], SaveProfileResolver.Gen3Signatures(rtc));
        Assert.Equal([0x01121999u], SaveProfileResolver.Gen3Signatures(Gen3Save(0x01121999).AsSpan(0, 0x10000)));
    }

    [Fact]
    public void HostilePathsDoNotThrow()
    {
        var resolver = new SaveProfileResolver(Store(), new PairingMemory(Path.Combine(dir, PairingMemory.FileName)));
        foreach (var path in new[] { "", "   ", "a\0b", "content://com.android.providers/document/1", dir })
            Assert.Equal(ResolveHow.Ask, resolver.Resolve(new byte[16], path, new ProfileHint(RomPath: path)).How);
        Assert.False(resolver.Remember("a\0b", null, PkhaxProfileReader.Load(Profile("x", Sha1(RomA))).Profile!));
    }

    [Fact]
    public void AnAndroidContentTokenIsAPairingKeyButNeverAFolder()
    {
        const string token = "content://com.android.externalstorage.documents/document/primary%3ASaves%2Fheart.sav";
        var memory = new PairingMemory(Path.Combine(dir, PairingMemory.FileName));
        Assert.True(memory.Remember(token, Ash, "hack-a"));
        Assert.Equal(token, new PairingMemory(Path.Combine(dir, PairingMemory.FileName)).ByPath(token)!.SavePath);
        Assert.Null(SaveProfileResolver.SiblingRom(token));
        Assert.Null(SaveProfileResolver.ProfileFileFor(token));
        var r = new SaveProfileResolver(Store(Profile("hack-a", Sha1(RomA))), memory).Resolve(new byte[16], token, trainer: Ash);
        Assert.Equal((ResolveHow.Pairing, "hack-a"), (r.How, r.Profile!.Id));
        Assert.Null(PairingMemory.Normalise("emulator://live"));
        Assert.False(memory.Remember("emulator://live", Ash, "hack-a"));
    }

    [Fact]
    public void TheAskNamesTheDetectedClass()
    {
        Assert.Equal("Vanilla SAV3E", SaveResolution.ChoicesFor("SAV3E")[2]);
        Assert.Equal("Vanilla", SaveResolution.ChoicesFor(null)[2]);
    }

    [Fact]
    public void ARepeatedKeyIsARefusalNotACrash()
    {
        var text = "{\"schema\":\"pkhax-profile/1\",\"schema\":\"pkhax-profile/1\"}";
        Assert.Contains("repeats a key", PkhaxProfileReader.Load(text).Refusal);
        var store = ProfileStore.Create([new ProfileDocument("user:dup", text)]);
        Assert.Contains(store.Problems, p => p.Contains("repeats a key"));
    }

    [Fact]
    public void ThePairingFileKeepsTheNewestEntries()
    {
        var memory = new PairingMemory(Path.Combine(dir, PairingMemory.FileName));
        for (var i = 0; i < PairingMemory.MaxEntries + 5; i++)
            memory.Remember(Path.Combine(dir, $"s{i}.sav"), null, "hack-a");
        var again = new PairingMemory(Path.Combine(dir, PairingMemory.FileName));
        Assert.Equal(PairingMemory.MaxEntries, again.Entries.Count);
        Assert.Null(again.ByPath(Path.Combine(dir, "s0.sav")));
        Assert.NotNull(again.ByPath(Path.Combine(dir, $"s{PairingMemory.MaxEntries + 4}.sav")));
    }

    [Fact]
    public void TheStoreIndexesByImageReportsRefusalsAndLetsTheUserFolderReplaceABuiltIn()
    {
        var a = Profile("hack-a", Sha1(RomA));
        var store = ProfileStore.Create([
            new ProfileDocument("built-in:a", a.ToJsonString()),
            new ProfileDocument("user:bad", "{}"),
            new ProfileDocument("user:a", Profile("hack-a", Sha1(RomB)).ToJsonString()),
            new ProfileDocument("user:unreadable", null, "permission denied"),
        ]);
        Assert.Equal("user:a", store.SourceOf("hack-a"));
        Assert.Empty(store.ForImage(Sha1(RomA)));
        Assert.Equal("hack-a", Assert.Single(store.ForImage(Sha1(RomB).ToUpperInvariant())).Id);
        Assert.Equal(2, store.Problems.Count);
        Assert.Contains("replaces", Assert.Single(store.Notes));
        Assert.Contains(store.Problems, p => p.Contains("permission denied"));
    }

    [Fact]
    public void TheStoreReadsAFolderOfProfileFiles()
    {
        File("one" + PkhaxProfile.FileSuffix, System.Text.Encoding.UTF8.GetBytes(Profile("one", Sha1(RomA)).ToJsonString()));
        File("ignored.json", System.Text.Encoding.UTF8.GetBytes(Profile("ignored", Sha1(RomB)).ToJsonString()));
        var store = ProfileStore.Create(ProfileStore.Folder(dir));
        Assert.Equal(["one"], store.All.Select(p => p.Id));
        Assert.Empty(ProfileStore.Create(ProfileStore.Folder(Path.Combine(dir, "missing"))).All);
    }

    [Fact]
    public void Gen3SignaturesNeedWholeSectors()
    {
        Assert.Empty(SaveProfileResolver.Gen3Signatures(new byte[0x1001]));
        Assert.Empty(SaveProfileResolver.Gen3Signatures(new byte[0x2000]));
        Assert.Equal([0x01121999u], SaveProfileResolver.Gen3Signatures(Gen3Save(0x01121999)));
    }

    [Fact]
    public void EveryBuiltInProfileLoadsAndNoneIsAHack()
    {
        var store = ProfileStore.Create(ProfileStore.BuiltIns());
        Assert.Empty(store.Problems);
        Assert.NotEmpty(store.All);
        Assert.All(store.All, p => Assert.Equal("unnerf", p.Kind));
        Assert.All(store.All, p => Assert.StartsWith(ProfileStore.BuiltInSource + ":", store.SourceOf(p.Id)));
    }
}
