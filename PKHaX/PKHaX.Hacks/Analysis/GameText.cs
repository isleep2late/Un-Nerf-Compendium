using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public static class GameText
{
    public const ushort Gen6BaseKey = 0x7C89;
    public const ushort Gen6KeyAdvance = 0x2983;

    public static IReadOnlyList<string>? ReadGen4(ReadOnlySpan<byte> s)
    {
        if (s.Length < 4)
            return null;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(s);
        var seed = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(2));
        if (4 + count * 8 > s.Length)
            return null;
        var lines = new List<string>(count);
        Span<byte> buffer = stackalloc byte[512];
        for (var i = 0; i < count; i++)
        {
            var key = (uint)((seed * 0x2FD * (i + 1)) & 0xFFFF);
            var keyFull = key | (key << 16);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(4 + i * 8)) ^ keyFull;
            var length = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(8 + i * 8)) ^ keyFull;
            if (offset >= s.Length || length > 256 || offset + length * 2 > s.Length)
                return null;
            var charKey = (ushort)((0x91BD3 * (i + 1)) & 0xFFFF);
            var bytes = buffer.Slice(0, (int)length * 2);
            for (var c = 0; c < length; c++)
            {
                var v = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(s.Slice((int)offset + c * 2)) ^ charKey);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(c * 2), v);
                charKey = (ushort)(charKey + 0x493D);
            }
            if (length > 0 && BinaryPrimitives.ReadUInt16LittleEndian(bytes) == 0xF100)
            {
                lines.Add("");
                continue;
            }
            lines.Add(StringConverter4.GetString(bytes));
        }
        return lines;
    }

    public static IReadOnlyList<string>? ReadGen5(ReadOnlySpan<byte> s) => ReadSections(s, gen5: true);

    public static IReadOnlyList<string>? ReadGen6(ReadOnlySpan<byte> s) => ReadSections(s, gen5: false);

    private static IReadOnlyList<string>? ReadSections(ReadOnlySpan<byte> s, bool gen5)
    {
        if (s.Length < 0x10)
            return null;
        var sections = BinaryPrimitives.ReadUInt16LittleEndian(s);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(2));
        if (sections == 0 || 0x0C + sections * 4 > s.Length)
            return null;
        var section = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(0x0C));
        if (section < 0 || section + 4 + count * 8 > s.Length)
            return null;
        var lines = new List<string>(count);
        var key = Gen6BaseKey;
        for (var i = 0; i < count; i++)
        {
            var entry = section + 4 + i * 8;
            var offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(entry));
            var length = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(entry + 4));
            var at = section + offset;
            if (offset < 0 || at < 0 || (long)at + length * 2 > s.Length)
                return null;
            var chars = new ushort[length];
            for (var c = 0; c < length; c++)
                chars[c] = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(at + c * 2));
            if (gen5)
                DecryptGen5(chars);
            else
                DecryptGen6(chars, key);
            key = (ushort)(key + Gen6KeyAdvance);
            lines.Add(Decode(chars));
        }
        return lines;
    }

    public static void DecryptGen5(Span<ushort> chars)
    {
        if (chars.Length == 0)
            return;
        var k = (ushort)(chars[^1] ^ 0xFFFF);
        for (var j = chars.Length - 1; j >= 0; j--)
        {
            chars[j] ^= k;
            k = (ushort)((k >> 3) | (k << 13));
        }
    }

    public static void DecryptGen6(Span<ushort> chars, ushort key)
    {
        var k = key;
        for (var j = 0; j < chars.Length; j++)
        {
            chars[j] ^= k;
            k = (ushort)((k << 3) | (k >> 13));
        }
    }

    private static string Decode(ReadOnlySpan<ushort> chars)
    {
        var sb = new StringBuilder(chars.Length);
        foreach (var c in chars)
        {
            if (c == 0xFFFF || c == 0x0000)
                break;
            if (c == 0xFFFE)
            {
                sb.Append('\n');
                continue;
            }
            if (c >= 0xF000 && c < 0xF100)
                return sb.Length == 0 ? "" : sb.ToString();
            sb.Append(c switch
            {
                0xE08F or 0x246E => '♀',
                0xE08E or 0x246D => '♂',
                0x2019 => '\'',
                _ => (char)c,
            });
        }
        return sb.ToString();
    }

    public static int? FindBank(IReadOnlyList<ReadOnlyMemory<byte>> banks, Func<ReadOnlyMemory<byte>, IReadOnlyList<string>?> read,
        string line1, string line2, out IReadOnlyList<string>? found)
    {
        for (var i = 0; i < banks.Count; i++)
        {
            var lines = Read(read, banks[i]);
            if (lines is { Count: > 2 } && Same(lines[1], line1) && Same(lines[2], line2))
            {
                found = lines;
                return i;
            }
        }
        found = null;
        return null;
    }

    private static IReadOnlyList<string>? Read(Func<ReadOnlyMemory<byte>, IReadOnlyList<string>?> read, ReadOnlyMemory<byte> bank)
    {
        try
        {
            return read(bank);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static bool Same(string a, string b) => string.Equals(IdMapper.Normalise(a), IdMapper.Normalise(b), StringComparison.Ordinal);
}
