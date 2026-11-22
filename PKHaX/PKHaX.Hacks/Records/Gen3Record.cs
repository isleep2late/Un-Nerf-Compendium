using System;
using System.Buffers.Binary;
using PKHeX.Core;

namespace PKHaX.Hacks.Records;

internal static class Gen3Record
{
    public const int SubstructSize = 12;
    public const int SecureOffset = 0x20;
    public const int SecureSize = 4 * SubstructSize;
    public const int StoredSize = SecureOffset + SecureSize;
    public const int PartySize = 100;

    private static readonly string[] Orders =
    [
        "GAEM", "GAME", "GEAM", "GEMA", "GMAE", "GMEA",
        "AGEM", "AGME", "AEGM", "AEMG", "AMGE", "AMEG",
        "EGAM", "EGMA", "EAGM", "EAMG", "EMGA", "EMAG",
        "MGAE", "MGEA", "MAGE", "MAEG", "MEGA", "MEAG",
    ];

    public static ushort Decrypt(ReadOnlySpan<byte> record, Span<byte> gaem)
    {
        var pid = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var key = pid ^ BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        Span<byte> plain = stackalloc byte[SecureSize];
        ushort sum = 0;
        for (var i = 0; i < SecureSize; i += 4)
        {
            var word = BinaryPrimitives.ReadUInt32LittleEndian(record[(SecureOffset + i)..]) ^ key;
            BinaryPrimitives.WriteUInt32LittleEndian(plain[i..], word);
            sum = (ushort)(sum + (word & 0xFFFF) + (word >> 16));
        }
        Unshuffle(plain, pid, gaem);
        return sum;
    }

    public static void Encrypt(ReadOnlySpan<byte> gaem, Span<byte> record)
    {
        var pid = BinaryPrimitives.ReadUInt32LittleEndian(record);
        var key = pid ^ BinaryPrimitives.ReadUInt32LittleEndian(record[4..]);
        Span<byte> shuffled = stackalloc byte[SecureSize];
        var order = Orders[pid % 24];
        for (var slot = 0; slot < 4; slot++)
        {
            var logical = "GAEM".IndexOf(order[slot]);
            gaem.Slice(logical * SubstructSize, SubstructSize).CopyTo(shuffled.Slice(slot * SubstructSize, SubstructSize));
        }
        for (var i = 0; i < SecureSize; i += 4)
            BinaryPrimitives.WriteUInt32LittleEndian(record[(SecureOffset + i)..], BinaryPrimitives.ReadUInt32LittleEndian(shuffled[i..]) ^ key);
    }

    public static ushort Sum(ReadOnlySpan<byte> secure)
    {
        ushort sum = 0;
        for (var i = 0; i < SecureSize; i += 2)
            sum += BinaryPrimitives.ReadUInt16LittleEndian(secure[i..]);
        return sum;
    }

    public static void Unshuffle(ReadOnlySpan<byte> shuffled, uint pid, Span<byte> gaem)
    {
        var order = Orders[pid % 24];
        for (var slot = 0; slot < 4; slot++)
        {
            var logical = "GAEM".IndexOf(order[slot]);
            shuffled.Slice(slot * SubstructSize, SubstructSize).CopyTo(gaem.Slice(logical * SubstructSize, SubstructSize));
        }
    }

    public static string Text(ReadOnlySpan<byte> data, int language)
    {
        return StringConverter3.GetString(data, Lang(language));
    }

    private static int Lang(int? language) => language is >= 1 and <= 7 ? language.Value : (int)LanguageID.English;

    public static string? TextProblem(string? text, int? language, int length)
    {
        if (text is null || text.Length == 0)
            return "a nickname needs at least one character";
        if (text.Length > length)
            return $"\"{text}\" is {text.Length} characters, longer than the {length} this record holds";
        var lang = Lang(language);
        var buffer = new byte[length];
        StringConverter3.SetString(buffer, text, length, lang);
        var back = StringConverter3.GetString(buffer, lang);
        return back == text ? null : $"\"{text}\" has a character the game's text cannot hold (it would read \"{back}\")";
    }

    public static byte[] EncodeText(string? text, int? language, int length)
    {
        if (TextProblem(text, language, length) is { } why)
            throw new RecordEncodeException(why);
        var buffer = new byte[length];
        StringConverter3.SetString(buffer, text!, length, Lang(language));
        return buffer;
    }

    public static string? NicknameChange(HackMon before, HackMon after, int length) =>
        before.Nickname == after.Nickname ? null
        : TextProblem(after.Nickname, after.Language, length) is { } why ? $"Nickname ({why})"
        : null;

    public static uint ShinyXor(uint pid, uint otId)
    {
        var x = pid ^ otId;
        return (x & 0xFFFF) ^ (x >> 16);
    }

    public static StatSet IvsFromWord(uint word) => new(
        (int)(word & 0x1F),
        (int)((word >> 5) & 0x1F),
        (int)((word >> 10) & 0x1F),
        (int)((word >> 20) & 0x1F),
        (int)((word >> 25) & 0x1F),
        (int)((word >> 15) & 0x1F));

    public static StatSet Evs(ReadOnlySpan<byte> e) => new(e[0], e[1], e[2], e[4], e[5], e[3]);

    public static HackPartyStats PartyTail(ReadOnlySpan<byte> record, int offset) => new(
        BinaryPrimitives.ReadUInt32LittleEndian(record[offset..]),
        record[offset + 4],
        BinaryPrimitives.ReadUInt16LittleEndian(record[(offset + 6)..]),
        new StatSet(
            BinaryPrimitives.ReadUInt16LittleEndian(record[(offset + 8)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[(offset + 10)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[(offset + 12)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[(offset + 16)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[(offset + 18)..]),
            BinaryPrimitives.ReadUInt16LittleEndian(record[(offset + 14)..])));

    public static ushort[] Moves16(ReadOnlySpan<byte> a, ushort mask)
    {
        var moves = new ushort[4];
        for (var i = 0; i < 4; i++)
            moves[i] = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(a[(i * 2)..]) & mask);
        return moves;
    }

    public static byte[] Bytes4(ReadOnlySpan<byte> data, byte mask)
    {
        var result = new byte[4];
        for (var i = 0; i < 4; i++)
            result[i] = (byte)(data[i] & mask);
        return result;
    }

    public static byte[] PpUps(byte ppBonuses)
    {
        var result = new byte[4];
        for (var i = 0; i < 4; i++)
            result[i] = (byte)((ppBonuses >> (2 * i)) & 3);
        return result;
    }

    public static ushort? VanillaNational(ushort internalId)
    {
        if (internalId is 0 or > 411)
            return null;
        var national = SpeciesConverter.GetNational3(internalId);
        return national == 0 ? null : national;
    }
}
