using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PKHaX.Hacks.Readers;
using PKHeX.Core;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class VanillaRoundTripTests : IDisposable
{
    public VanillaRoundTripTests() => HackSaveReaders.Register();

    public void Dispose() => HackSaveReaders.Unregister();

    public static IEnumerable<object[]> Gen3Versions() =>
    [
        [GameVersion.E, typeof(SAV3E)], [GameVersion.FR, typeof(SAV3FRLG)], [GameVersion.R, typeof(SAV3RS)],
    ];

    [Theory]
    [MemberData(nameof(Gen3Versions))]
    public void Gen3SavesOpenAsTheSameTypeAndWriteTheSameBytes(GameVersion version, Type expected)
    {
        var raw = TestSaves.Gen3(version);
        HackSaveReaders.Unregister();
        var without = SaveUtil.GetSaveFile((byte[])raw.Clone());
        HackSaveReaders.Register();
        var with = SaveUtil.GetSaveFile((byte[])raw.Clone());
        Assert.NotNull(without);
        Assert.NotNull(with);
        Assert.IsType(expected, without);
        Assert.Equal(without.GetType(), with.GetType());
        Assert.Equal(raw, with.Write().ToArray());
        Assert.Equal(without.Write().ToArray(), with.Write().ToArray());
    }

    [Fact]
    public void FixtureSavesOpenAndWriteTheSameWithTheReaderRegistered()
    {
        var dir = Environment.GetEnvironmentVariable("PKHAX_FIXTURES");
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return;
        var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(f => Path.GetExtension(f) is ".sav" or ".srm" or ".dsv" or ".bin")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var raw = File.ReadAllBytes(file);
            HackSaveReaders.Unregister();
            var without = SaveUtil.GetSaveFile((byte[])raw.Clone(), file);
            HackSaveReaders.Register();
            var with = SaveUtil.GetSaveFile((byte[])raw.Clone(), file);
            Assert.Equal(without?.GetType(), with?.GetType());
            if (without is null || with is null)
                continue;
            Assert.Equal(without.Write().ToArray(), with.Write().ToArray());
        }
    }
}
