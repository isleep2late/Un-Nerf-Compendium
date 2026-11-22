using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Profiles;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class PkhaxProfileTests
{
    internal const string HeartSoulSha1 = "79ee6df0869c1773c8c6a5f764afc1f8d833d8bb";
    internal const string CodeSha1 = "0123456789abcdef0123456789abcdef01234567";
    internal const string ModSet = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static JsonObject HeartSoul() => JsonNode.Parse($$"""
        {
          "schema": "pkhax-profile/1",
          "id": "heartsoul-2.0.6",
          "displayName": "Pokemon Heart & Soul 2.0.6",
          "kind": "hack",
          "base": "emerald-usa-europe",
          "parent": null,
          "platform": "gba",
          "match": [ { "sha1": "{{HeartSoulSha1}}", "crc32": "01713508", "size": 33554432, "label": "plain" } ],
          "engine": { "family": "gba-expansion", "version": "1.15.2" },
          "save": {
            "variant": "gen3-expansion/1",
            "writeEvidence": "Unproven",
            "container": { "kind": "gba-sectors", "fileSize": 131072 },
            "party": { "block": "saveBlock1", "countOffset": 568, "offset": 572, "stride": 100 },
            "money": { "block": "saveBlock1", "offset": null },
            "preserve": ["tails", "unchecked"]
          },
          "data": { "source": "rom", "reader": "gba-rhh", "readerVersion": 1, "romSha1": "{{HeartSoulSha1}}", "embedded": null },
          "ram": { "party": { "address": "0x02034764", "stride": 100 } },
          "unNerf": { "anyAbility": { "kind": "none" }, "deoxysForm": { "kind": "none" }, "frontierShift": 0 },
          "editor": { "legality": "off" },
          "codes": [],
          "evidence": { "save": "Unproven" },
          "sources": [ { "what": "sb sizes", "from": "GF ROM header" } ],
          "magic": { "shiny": { "hook": "@ram:gRngValue" } },
          "headerFamily": "gba:BPEE:0"
        }
        """)!.AsObject();

    internal static JsonObject Ctr(string kind = "hack") => JsonNode.Parse($$"""
        {
          "schema": "pkhax-profile/1", "id": "rising-ruby-1.0", "displayName": "Rising Ruby", "kind": "{{kind}}",
          "base": "omega-ruby", "platform": "3ds",
          "match": [ { "titleId": "000400000011C400", "codeSha1": "{{CodeSha1}}", "modSet": "{{ModSet}}", "updateVersion": 0, "label": "mods" } ]
        }
        """)!.AsObject();

    internal static JsonObject Nx() => JsonNode.Parse("""
        {
          "schema": "pkhax-profile/1", "id": "lgpe-hack", "displayName": "LGPE hack", "kind": "hack",
          "base": "lets-go-pikachu", "platform": "switch",
          "match": [ { "titleId": "010003F003A34000", "mainBuildId": "0123456789ABCDEF0123456789ABCDEF00000000", "updateVersion": 131072, "modSet": "none", "label": "v1-0-2" } ]
        }
        """)!.AsObject();

    private static string Refusal(JsonObject o)
    {
        var r = PkhaxProfileReader.Load(o);
        Assert.False(r.Ok, "expected a refusal");
        return r.Refusal!;
    }

    private static JsonObject With(JsonObject o, Action<JsonObject> edit)
    {
        var c = (JsonObject)o.DeepClone();
        edit(c);
        return c;
    }

    private static JsonObject Row(JsonObject o) => o["match"]![0]!.AsObject();

    private static PkhaxProfile Load(JsonObject o) => PkhaxProfileReader.Load(o).Profile!;

    [Fact]
    public void TheDesignExampleLoadsWithItsBlocks()
    {
        var r = PkhaxProfileReader.Load(HeartSoul());
        Assert.True(r.Ok, r.Refusal);
        var p = r.Profile!;
        Assert.Equal("heartsoul-2.0.6", p.Id);
        Assert.Equal("hack", p.Kind);
        Assert.Equal("gba", p.Platform);
        Assert.Null(p.Parent);
        Assert.True(p.IsHack);
        Assert.Equal("gen3-expansion/1", p.SaveVariant);
        Assert.Equal("Unproven", p.WriteEvidence);
        Assert.Equal(HeartSoulSha1, p.DataRomSha1);
        Assert.False(p.HasEmbeddedData);
        var m = Assert.Single(p.Match);
        Assert.Equal(MatchShape.RomFile, m.Shape);
        Assert.Equal(HeartSoulSha1, m.Key);
        Assert.Equal(33554432, m.Size);
        Assert.True(p.Matches(HeartSoulSha1.ToUpperInvariant()));
        Assert.Equal(572, (int)p.Save!["party"]!["offset"]!);
    }

    [Fact]
    public void UnknownKeysSurviveAReExport()
    {
        var source = With(HeartSoul(), o =>
        {
            o["futureBlock"] = new JsonObject { ["nested"] = new JsonArray(1, 2, 3), ["n"] = null };
            o["save"]!["futureSaveKey"] = "kept";
        });
        var p = PkhaxProfileReader.Load(source).Profile!;
        Assert.Equal(["futureBlock", "headerFamily", "magic"], p.UnknownKeys().OrderBy(k => k, StringComparer.Ordinal));
        var again = PkhaxProfileReader.Load(p.ToJson());
        Assert.True(again.Ok, again.Refusal);
        Assert.True(JsonNode.DeepEquals(source, again.Profile!.ToJsonObject()));
        Assert.Equal("kept", (string)again.Profile.Save!["futureSaveKey"]!);
    }

    [Fact]
    public void TheProfileDoesNotShareStateWithItsSourceOrItsReaders()
    {
        var source = HeartSoul();
        var p = PkhaxProfileReader.Load(source).Profile!;
        source["id"] = "changed";
        p.Save!["variant"] = "changed";
        p.ToJsonObject()["kind"] = "vanilla";
        Assert.Equal("heartsoul-2.0.6", p.Id);
        Assert.Equal("gen3-expansion/1", p.SaveVariant);
        Assert.Equal("hack", p.Kind);
    }

    [Fact]
    public void AGameProfileProjectsAndComesBack()
    {
        var game = With(HeartSoul(), o => o["schema"] = 1);
        Assert.Contains("GameProfile", Refusal(game));
        var projected = PkhaxProfileReader.FromGameProfile(game);
        var p = PkhaxProfileReader.Load(projected).Profile!;
        Assert.True(JsonNode.DeepEquals(game, PkhaxProfileReader.ToGameProfile(p)));
        Assert.Equal(1, (int)game["schema"]!);
    }

    [Theory]
    [InlineData("Hack")]
    [InlineData("HackUnNerf")]
    [InlineData("hack-unnerf")]
    [InlineData("")]
    public void KindsOutsideTheDesign1VocabularyAreRefused(string kind)
    {
        Assert.Contains("hack+unnerf", Refusal(With(HeartSoul(), o => o["kind"] = kind)));
    }

    [Fact]
    public void EveryDesign1KindAndPlatformLoads()
    {
        foreach (var kind in PkhaxProfile.Kinds)
            Assert.True(PkhaxProfileReader.Load(With(HeartSoul(), o => o["kind"] = kind)).Ok, kind);
        foreach (var platform in new[] { "gb", "gbc", "gba", "nds" })
            Assert.True(PkhaxProfileReader.Load(With(HeartSoul(), o => o["platform"] = platform)).Ok, platform);
        Assert.Contains("platform", Refusal(With(HeartSoul(), o => o["platform"] = "n64")));
    }

    [Fact]
    public void AProfileWithoutAnExactIdentityIsRefused()
    {
        Assert.Contains("exact image identity", Refusal(With(HeartSoul(), o => o.Remove("match"))));
        Assert.Contains("exact image identity", Refusal(With(HeartSoul(), o => o["match"] = new JsonArray())));
        Assert.Contains("sha1", Refusal(With(HeartSoul(), o => Row(o).Remove("sha1"))));
        Assert.Contains("sha1", Refusal(With(HeartSoul(), o => Row(o)["sha1"] = HeartSoulSha1.ToUpperInvariant())));
        Assert.Contains("sha1", Refusal(With(HeartSoul(), o => Row(o)["sha1"] = HeartSoulSha1[..39])));
        Assert.Contains("label", Refusal(With(HeartSoul(), o => Row(o).Remove("label"))));
        Assert.Contains("crc32", Refusal(With(HeartSoul(), o => Row(o)["crc32"] = "0171350")));
        Assert.Contains("size", Refusal(With(HeartSoul(), o => Row(o)["size"] = 0)));
        Assert.Contains("mixes", Refusal(With(HeartSoul(), o => Row(o)["titleId"] = "000400000011C400")));
        Assert.Contains("repeats", Refusal(With(HeartSoul(), o => o["match"]!.AsArray().Add(With(Row(o), r => r["label"] = "again")))));
    }

    [Fact]
    public void A3dsRowNeedsTheCodeSha1Identity()
    {
        var p = PkhaxProfileReader.Load(Ctr()).Profile!;
        var m = Assert.Single(p.Match);
        Assert.Equal(MatchShape.CtrMods, m.Shape);
        var expected = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes($"3ds\n000400000011C400\n{CodeSha1}\n{ModSet}\n0")));
        Assert.Equal(expected, m.Key);
        Assert.Contains("codeCrc32", Refusal(With(Ctr(), o => { Row(o).Remove("codeSha1"); Row(o)["codeCrc32"] = "01713508"; })));
        Assert.Contains("codeSha1", Refusal(With(Ctr(), o => Row(o).Remove("updateVersion"))));
        Assert.Contains("codeSha1", Refusal(With(Ctr(), o => Row(o)["titleId"] = "000400000011c400")));
        Assert.Contains("mod set", Refusal(With(Ctr(), o => Row(o)["modSet"] = "abc")));
        var upperMods = PkhaxProfileReader.Load(With(Ctr(), o => Row(o)["modSet"] = ModSet.ToUpperInvariant())).Profile!;
        Assert.Equal(expected, upperMods.Match[0].Key);
    }

    [Fact]
    public void NumbersBuiltInCodeReadLikeParsedOnes()
    {
        var built = With(Ctr(), o => o["match"] = new JsonArray(new JsonObject
        {
            ["titleId"] = "000400000011C400", ["codeSha1"] = CodeSha1, ["modSet"] = ModSet, ["updateVersion"] = 1040, ["label"] = "mods",
        }));
        var parsed = With(Ctr(), o => Row(o)["updateVersion"] = JsonNode.Parse("1040"));
        Assert.Equal(PkhaxProfileReader.Load(parsed).Profile!.Match[0].Key, PkhaxProfileReader.Load(built).Profile!.Match[0].Key);
        Assert.Equal(33554432L, PkhaxProfileReader.Load(With(HeartSoul(), o => Row(o)["size"] = 33554432)).Profile!.Match[0].Size);
        Assert.True(PkhaxProfileReader.Load(With(HeartSoul(), o => o["unNerf"]!["frontierShift"] = (byte)4)).Ok);
        Assert.Contains("updateVersion", Refusal(With(built, o => Row(o)["updateVersion"] = 1.5)));
    }

    [Fact]
    public void TheReaderAgreesWithTheSchemaOnTheReviewsEdgeCases()
    {
        Assert.Contains("id", Refusal(With(HeartSoul(), o => o["id"] = "heartsoul\n")));
        Assert.Contains("label", Refusal(With(HeartSoul(), o => Row(o)["label"] = new string('a', 97))));
        Assert.Contains("base", Refusal(With(HeartSoul(), o => o["base"] = new string('a', 97))));
        Assert.Contains("save.variant", Refusal(With(HeartSoul(), o => o["save"]!["variant"] = "gen3-expansion/1\n")));
        Assert.Contains("data.reader", Refusal(With(HeartSoul(), o => o["data"]!["reader"] = 5)));
        Assert.Contains("data.readerVersion", Refusal(With(HeartSoul(), o => o["data"]!["readerVersion"] = "x")));
        Assert.Equal(33554432L, Load(With(HeartSoul(), o => Row(o)["size"] = JsonNode.Parse("33554432.0"))).Match[0].Size);
        Assert.True(PkhaxProfileReader.Load(With(HeartSoul(), o => o["unNerf"]!["frontierShift"] = JsonNode.Parse("4.0"))).Ok);
        Assert.True(PkhaxProfileReader.Load(With(HeartSoul(), o => o["displayName"] = string.Concat(Enumerable.Repeat("\U0001F600", 61)))).Ok);
        Assert.Contains("displayName", Refusal(With(HeartSoul(), o => o["displayName"] = string.Concat(Enumerable.Repeat("\U0001F600", 121)))));
    }

    [Fact]
    public void A3dsBakedImageKeysOnItsImageRoot()
    {
        var root = new string('b', 64);
        var p = PkhaxProfileReader.Load(With(Ctr(), o => o["match"] = new JsonArray(new JsonObject { ["imageRoot"] = root, ["label"] = "baked" }))).Profile!;
        Assert.Equal(MatchShape.CtrBaked, p.Match[0].Shape);
        Assert.Equal(root, p.Match[0].Key);
        Assert.Contains("imageRoot", Refusal(With(Ctr(), o => o["match"] = new JsonArray(new JsonObject { ["imageRoot"] = root.ToUpperInvariant(), ["label"] = "baked" }))));
    }

    [Fact]
    public void ASwitchRowKeysOnTheCanonicalBuildId()
    {
        var p = PkhaxProfileReader.Load(Nx()).Profile!;
        var m = Assert.Single(p.Match);
        Assert.Equal(MatchShape.Switch, m.Shape);
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF", m.MainBuildId);
        var expected = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes("nx\n010003F003A34000\n0123456789ABCDEF0123456789ABCDEF\n131072\nnone")));
        Assert.Equal(expected, m.Key);
        Assert.Contains("mainBuildId", Refusal(With(Nx(), o => Row(o).Remove("mainBuildId"))));
        Assert.Contains("32 or 40 hex digits", Refusal(With(Nx(), o => Row(o)["mainBuildId"] = "0123")));
        Assert.Contains("32 or 40 hex digits", Refusal(With(Nx(), o => Row(o)["mainBuildId"] = new string('A', 32) + "00")));
        Assert.Contains("mod set", Refusal(With(Nx(), o => Row(o)["modSet"] = " none")));
        Assert.Equal(Load(Nx()).Match[0].Key, Load(With(Nx(), o => Row(o)["mainBuildId"] = "0123456789ABCDEF0123456789ABCDEF" + new string('0', 32))).Match[0].Key);
        Assert.Contains("updateVersion", Refusal(With(Nx(), o => Row(o)["updateVersion"] = -1)));
    }

    [Fact]
    public void BlocksWithTheWrongShapeAreRefused()
    {
        Assert.Contains("unNerf", Refusal(With(HeartSoul(), o => o["unnerf"] = new JsonObject())));
        Assert.Contains("frontierShift", Refusal(With(HeartSoul(), o => o["unNerf"]!["frontierShift"] = 2)));
        Assert.Contains("anyAbility", Refusal(With(HeartSoul(), o => o["unNerf"]!["anyAbility"] = new JsonObject { ["kind"] = "guess" })));
        Assert.Contains("fields", Refusal(With(HeartSoul(), o => o["unNerf"]!["anyAbility"] = new JsonObject { ["kind"] = "bits" })));
        Assert.Contains("save.variant", Refusal(With(HeartSoul(), o => o["save"]!.AsObject().Remove("variant"))));
        Assert.Contains("writeEvidence", Refusal(With(HeartSoul(), o => o["save"]!["writeEvidence"] = "Proven")));
        Assert.Contains("save.party", Refusal(With(HeartSoul(), o => o["save"]!["party"] = 5)));
        Assert.Contains("data.romSha1", Refusal(With(HeartSoul(), o => o["data"]!["romSha1"] = "x")));
        Assert.Contains("hackdata/1", Refusal(With(HeartSoul(), o => o["data"]!["embedded"] = new JsonObject { ["schema"] = "other" })));
        Assert.Contains("data.source", Refusal(With(HeartSoul(), o => o["data"]!["source"] = "web")));
        Assert.Contains("codes", Refusal(With(HeartSoul(), o => o["codes"] = new JsonObject())));
        Assert.Contains("parent", Refusal(With(HeartSoul(), o => o["parent"] = "Not An Id")));
        Assert.Contains("schema", Refusal(With(HeartSoul(), o => o["schema"] = "pkhax-profile/2")));
        Assert.True(PkhaxProfileReader.Load(With(HeartSoul(), o => o["data"]!["embedded"] = new JsonObject { ["schema"] = "hackdata/1" })).Profile!.HasEmbeddedData);
    }

    [Fact]
    public void TextThatIsNotAProfileObjectIsRefused()
    {
        Assert.Contains("not JSON", PkhaxProfileReader.Load("{ \"schema\": ").Refusal);
        Assert.Contains("not a JSON object", PkhaxProfileReader.Load("[1]").Refusal);
        Assert.Contains("not JSON", PkhaxProfileReader.Load("{ // c\n }").Refusal);
    }

    [Fact]
    public void TheSchemaFileSitsBesideTheReaderAndNamesTheSameVocabulary()
    {
        var path = Path.Combine(SourcePaths.PKHaXRoot, "PKHaX.Hacks", "Profiles", "pkhax-profile-1.schema.json");
        var schema = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(PkhaxProfile.Schema, (string)schema["properties"]!["schema"]!["const"]!);
        Assert.Equal(PkhaxProfile.Kinds, schema["properties"]!["kind"]!["enum"]!.AsArray().Select(k => (string)k!));
        Assert.Equal(PkhaxProfile.Platforms, schema["properties"]!["platform"]!["enum"]!.AsArray().Select(k => (string)k!));
        Assert.Equal(PkhaxProfile.WriteEvidenceLevels, schema["definitions"]!["evidence"]!["enum"]!.AsArray().Select(k => (string)k!));
    }

    [Fact]
    public void AnEmbeddedVersion4TableSetStillLoads()
    {
        var sha = new string('c', 40);
        var embedded = new JsonObject
        {
            ["schema"] = "hackdata/1", ["readerVersion"] = 4, ["romSha1"] = sha, ["reader"] = "gba-gf",
            ["species"] = new JsonArray(new JsonObject
            {
                ["id"] = 1, ["name"] = "Bulbasaur", ["national"] = 1, ["form"] = 0, ["types"] = new JsonArray(12, 3), ["base"] = new JsonArray(45, 49, 49, 45, 65, 65),
                ["abilities"] = new JsonArray(65, 0), ["growth"] = 3, ["gender"] = 31, ["catch"] = 45, ["exp"] = 64,
            }),
        };
        var profile = PkhaxProfileReader.Load(With(HeartSoul(), o => o["data"]!["embedded"] = embedded.DeepClone())).Profile!;
        Assert.True(profile.HasEmbeddedData);
        var data = PKHaX.Hacks.Analysis.HackDataCache.FromJson(embedded.ToJsonString(), sha)!;
        Assert.Equal(4, data.ReaderVersion);
        Assert.Equal("Bulbasaur", data.Species[0].Name);
        Assert.True(data.Species[0].GenderRead);
        Assert.Empty(data.Tables);
    }
}
