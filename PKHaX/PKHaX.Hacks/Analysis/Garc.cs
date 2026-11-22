using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace PKHaX.Hacks.Analysis;

public static class Garc
{
    public static IReadOnlyList<ReadOnlyMemory<byte>>? Read(ReadOnlyMemory<byte> bytes)
    {
        var s = bytes.Span;
        if (s.Length < 0x1C || !Magic(s, 0, "CRAG"))
            return null;
        var headerSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(4));
        var dataOffsetRaw = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(0x10));
        if (dataOffsetRaw > (uint)s.Length)
            return null;
        var dataOffset = (int)dataOffsetRaw;
        var fato = headerSize;
        if (!Magic(s, fato, "OTAF"))
            return null;
        var fatoSize = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(fato + 4));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(fato + 8));
        var fatbLong = (long)fato + fatoSize;
        if (fatoSize < 12 + count * 4 || fatbLong > s.Length || !Magic(s, (int)fatbLong, "BTAF"))
            return null;
        var fatb = (int)fatbLong;
        var entries = fatb + 12;
        var files = new List<ReadOnlyMemory<byte>>(count);
        for (var i = 0; i < count; i++)
        {
            var atLong = (long)entries + BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(fato + 12 + i * 4));
            if (atLong + 16 > s.Length)
                return null;
            var at = (int)atLong;
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(at));
            if (bits == 0)
            {
                files.Add(ReadOnlyMemory<byte>.Empty);
                continue;
            }
            var start = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(at + 4));
            var end = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(at + 8));
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(at + 12));
            if (start < 0 || end < start || length < 0 || length > end - start || (long)dataOffset + start + length > s.Length)
                return null;
            files.Add(bytes.Slice(dataOffset + start, length));
        }
        return files;
    }

    private static bool Magic(ReadOnlySpan<byte> s, int at, string magic) =>
        at >= 0 && (long)at + 12 <= s.Length && s[at] == magic[0] && s[at + 1] == magic[1] && s[at + 2] == magic[2] && s[at + 3] == magic[3];
}

public static class Lz11
{
    public const int MaxSize = 0x1000000;

    public static bool IsCompressed(ReadOnlySpan<byte> s) => s.Length >= 4 && s[0] == 0x11 && ((s[1] | (s[2] << 8) | (s[3] << 16)) > 0 || s.Length >= 8);

    public static byte[]? Decompress(ReadOnlySpan<byte> s)
    {
        if (!IsCompressed(s))
            return null;
        var size = s[1] | (s[2] << 8) | (s[3] << 16);
        var p = 4;
        if (size == 0 && s.Length >= 8)
        {
            var big = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(4));
            if (big == 0 || big > MaxSize)
                return null;
            size = (int)big;
            p = 8;
        }
        var o = new byte[size];
        var w = 0;
        while (w < size)
        {
            if (p >= s.Length)
                return null;
            var flags = s[p++];
            for (var bit = 7; bit >= 0 && w < size; bit--)
            {
                if ((flags & (1 << bit)) == 0)
                {
                    if (p >= s.Length)
                        return null;
                    o[w++] = s[p++];
                    continue;
                }
                if (p + 1 >= s.Length)
                    return null;
                var b0 = s[p];
                int length, disp;
                switch (b0 >> 4)
                {
                    case 0:
                        if (p + 2 >= s.Length)
                            return null;
                        length = (((b0 & 0x0F) << 4) | (s[p + 1] >> 4)) + 0x11;
                        disp = (((s[p + 1] & 0x0F) << 8) | s[p + 2]) + 1;
                        p += 3;
                        break;
                    case 1:
                        if (p + 3 >= s.Length)
                            return null;
                        length = (((b0 & 0x0F) << 12) | (s[p + 1] << 4) | (s[p + 2] >> 4)) + 0x111;
                        disp = (((s[p + 2] & 0x0F) << 8) | s[p + 3]) + 1;
                        p += 4;
                        break;
                    default:
                        length = (b0 >> 4) + 1;
                        disp = (((b0 & 0x0F) << 8) | s[p + 1]) + 1;
                        p += 2;
                        break;
                }
                if (disp > w)
                    return null;
                for (var i = 0; i < length && w < size; i++, w++)
                    o[w] = o[w - disp];
            }
        }
        if (p < s.Length && (p & ~3) + 4 < s.Length)
            return null;
        return o;
    }
}
