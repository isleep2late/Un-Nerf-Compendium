using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace PKHaX.Hacks.Analysis;

public static class Narc
{
    public static IReadOnlyList<ReadOnlyMemory<byte>>? Read(ReadOnlyMemory<byte> bytes)
    {
        var s = bytes.Span;
        if (s.Length < 0x1C || s[0] != (byte)'N' || s[1] != (byte)'A' || s[2] != (byte)'R' || s[3] != (byte)'C')
            return null;
        var headerSize = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(0x0C));
        var fatb = headerSize;
        if (!Magic(s, fatb, "BTAF"))
            return null;
        var fatbSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(fatb + 4));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(s.Slice(fatb + 8));
        if (fatbSize < 12 + count * 8 || fatb + fatbSize > s.Length)
            return null;
        var fntb = fatb + fatbSize;
        if (!Magic(s, fntb, "BTNF"))
            return null;
        var fntbSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(fntb + 4));
        var fimg = fntb + fntbSize;
        if (!Magic(s, fimg, "GMIF"))
            return null;
        var data = fimg + 8;
        var files = new List<ReadOnlyMemory<byte>>(count);
        for (var i = 0; i < count; i++)
        {
            var start = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(fatb + 12 + i * 8));
            var end = (int)BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(fatb + 16 + i * 8));
            if (start < 0 || end < start || (long)data + end > s.Length)
                return null;
            files.Add(bytes.Slice(data + start, end - start));
        }
        return files;
    }

    public static IReadOnlyList<ReadOnlyMemory<byte>>? Read(NitroFs fs, string path) => fs.File(path) is { } f ? Read(f) : null;

    private static bool Magic(ReadOnlySpan<byte> s, int at, string magic) =>
        at >= 0 && at + 12 <= s.Length && s[at] == magic[0] && s[at + 1] == magic[1] && s[at + 2] == magic[2] && s[at + 3] == magic[3];
}
