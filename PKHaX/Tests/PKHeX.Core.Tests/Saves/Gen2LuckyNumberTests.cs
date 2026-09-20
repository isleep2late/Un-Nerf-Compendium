using FluentAssertions;
using Xunit;

namespace PKHeX.Core.Tests.Saves;

/// <summary>
/// PKHaX: the Lucky Number Show's stored number (Radio Tower, Goldenrod).
///
/// The game rolls it from the same RNG stream as the Trainer ID when a new game starts, and re-rolls it whenever
/// the day counter moves on - pokecrystal engine/menus/intro_menu.asm LoadOrRegenerateLuckyIDNumber compares
/// sLuckyNumberDay against wCurDay + 1 and calls Random twice when they differ. Both decompilations lay the block
/// out identically: sRTCStatusFlags, 7 bytes of padding, sLuckyNumberDay (1 byte), sLuckyIDNumber (2 bytes), so
/// the offsets are derived from RTCFlags rather than tabulated per language - only the anchor moves between
/// localisations, and PKHeX already knows where that is for international, Japanese and Korean saves.
///
/// Byte order is big endian, like the Trainer ID: _PrintNum loads the first byte of a two-byte value into the
/// more significant half of its buffer (pokecrystal engine/math/print_num.asm), so the number the Lucky Number
/// Man reads out is byte0 * 256 + byte1.
/// </summary>
public static class Gen2LuckyNumberTests // PKHaX
{
    private static SAV2 NewSave(GameVersion version) => new(version: version);

    [Theory]
    [InlineData(GameVersion.GD)]
    [InlineData(GameVersion.SI)]
    [InlineData(GameVersion.C)]
    public static void OffsetsSitWhereTheDecompilationPutsThem(GameVersion version)
    {
        var sav = NewSave(version);
        sav.HasLuckyNumber.Should().BeTrue("every Gen 2 layout PKHeX knows carries the RTC status flags this is anchored on");
        sav.LuckyIDOffset.Should().Be(sav.LuckyNumberDayOffset + 1, "the day byte sits immediately before the number");
        sav.LuckyIDOffset.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(GameVersion.GD)]
    [InlineData(GameVersion.C)]
    public static void NumberRoundTripsBigEndian(GameVersion version)
    {
        var sav = NewSave(version);
        sav.LuckyID = 36267; // 0x8DAB, read off a real Crystal save
        sav.LuckyID.Should().Be(36267);

        // written the way the Lucky Number Man reads it: most significant byte first
        var data = sav.Data;
        data[sav.LuckyIDOffset].Should().Be(0x8D);
        data[sav.LuckyIDOffset + 1].Should().Be(0xAB);
    }

    [Theory]
    [InlineData(GameVersion.GD)]
    [InlineData(GameVersion.C)]
    public static void TheWholeRangeIsReachable(GameVersion version)
    {
        var sav = NewSave(version);
        foreach (var value in new ushort[] { 0, 1, 255, 256, 12345, 65535 })
        {
            sav.LuckyID = value;
            sav.LuckyID.Should().Be(value, "a Lucky ID is any 16-bit number");
        }
    }

    [Theory]
    [InlineData(GameVersion.GD)]
    [InlineData(GameVersion.C)]
    public static void DayRoundTripsAndIsItsOwnByte(GameVersion version)
    {
        var sav = NewSave(version);
        sav.LuckyID = 0x1234;
        sav.LuckyNumberDay = 200;
        sav.LuckyNumberDay.Should().Be(200);
        sav.LuckyID.Should().Be(0x1234, "the day byte sits before the number and must not overlap it");
        sav.Data[sav.LuckyNumberDayOffset].Should().Be(200);
    }

    [Theory]
    [InlineData(GameVersion.GD)]
    [InlineData(GameVersion.C)]
    public static void EditingTheNumberTouchesNothingElse(GameVersion version)
    {
        var sav = NewSave(version);
        var before = sav.Data.ToArray();
        sav.LuckyID = 0xBEEF;

        var after = sav.Data;
        var changed = new System.Collections.Generic.List<int>();
        for (int i = 0; i < before.Length; i++)
        {
            if (before[i] != after[i])
                changed.Add(i);
        }
        changed.Should().Equal([sav.LuckyIDOffset, sav.LuckyIDOffset + 1],
            "only the two bytes of the number may move - the Trainer ID and everything else are untouched");
    }
}
