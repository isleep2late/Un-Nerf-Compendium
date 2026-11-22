using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Intake;
using PKHaX.Hacks.Profiles;

namespace PKHaX.Hacks.Tests.Intake;

internal sealed class FakePatcher(PatchProbe probe, Func<byte[], byte[]> apply) : IRomPatcher
{
    public PatchProbe Inspect(ReadOnlySpan<byte> patch) => probe;

    public byte[] Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch) => apply(source.ToArray());
}

internal sealed class TempRoot : IDisposable
{
    public TempRoot()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pkhax-intake-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal static class IntakeTestData
{
    public const string Source = "GBATEK cartridge header (0x96 at 0xB2, complement at 0xBD), Pan Docs Game Boy header (checksum 0x14D, size code 0x148), GBATEK DS header CRC-16 at 0x15E, 3dbrew NCSD at 0x100";

    public static byte[] Gba(int length = 0x40000, int seed = 3)
    {
        var rom = new byte[length];
        new Random(seed).NextBytes(rom);
        "BPEE"u8.CopyTo(rom.AsSpan(0xAC));
        rom[0xB2] = 0x96;
        rom.AsSpan(0x200, 0x10).Clear();
        Complement(rom);
        return rom;
    }

    public static void Complement(byte[] rom)
    {
        var sum = 0;
        for (var i = 0xA0; i <= 0xBC; i++)
            sum -= rom[i];
        rom[0xBD] = (byte)(sum - 0x19);
    }

    public static string Sha1(byte[] data) => IntakeHash.Sha1(data);

    public static HackData Data(string key, bool unmapped = false, string name = "ZORBASAUR") => new()
    {
        RomSha1 = key,
        Reader = "synthetic",
        Platform = HackTables.Gba,
        GameCode = "BPEE",
        Engine = Gen3Engine.Vanilla,
        Species = unmapped
            ? [new HackSpecies(1, name, 1, 0, [12, 3], [45, 49, 49, 45, 65, 65], [65, 0], 3, 31, 45, 64), new HackSpecies(412, "NEWMON", null, 0, [0, 0], [50, 50, 50, 50, 50, 50], [1, 0], 0, 127, 45, 60)]
            : [new HackSpecies(1, name, 1, 0, [12, 3], [45, 49, 49, 45, 65, 65], [65, 0], 3, 31, 45, 64)],
        Moves = [new HackMove(1, "POUND", 1, 40, 0, 100, 35)],
        Items = [new HackItem(1, "MASTER BALL", 1, 3, 0)],
        Abilities = [new HackAbility(1, "STENCH", 1)],
    };

    public static HackKit Kit(string key, string family = SaveShape.Retail, int deriveVersion = HackKit.CurrentDeriveVersion) => new()
    {
        Key = key,
        Name = "Synthetic hack",
        Platform = HackTables.Gba,
        BaseGame = "E",
        GameCode = "BPEE",
        SaveFamily = family,
        DeriveVersion = deriveVersion,
        Source = new KitSource("synthetic.gba", 0x40000),
        At = DateTimeOffset.UnixEpoch,
    };

    public static string Key(char c) => new(c, 40);

    public static HackIntake Counting(HackKitStore store, List<string> calls) => new(store,
        (image, sym) =>
        {
            calls.Add(Sha1(image.ToArray()));
            return new HackDataResult(Data(Sha1(image.ToArray())), null);
        },
        image => SaveShape.Unknown(Sha1(image.ToArray()), "synthetic"));

    public static string Profile(HackData data) => new JsonObject
    {
        ["schema"] = PkhaxProfile.Schema,
        ["id"] = "synthetic-profile",
        ["displayName"] = "Synthetic profile",
        ["kind"] = "imported",
        ["platform"] = "gba",
        ["base"] = "synthetic-profile",
        ["match"] = new JsonArray(new JsonObject { ["sha1"] = data.RomSha1, ["label"] = "rom" }),
        ["data"] = new JsonObject { ["source"] = "embedded", ["romSha1"] = data.RomSha1, ["embedded"] = JsonNode.Parse(HackDataCache.ToJson(data)) },
    }.ToJsonString();
}
