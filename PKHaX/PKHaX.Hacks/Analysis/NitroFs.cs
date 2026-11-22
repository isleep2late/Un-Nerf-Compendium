using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace PKHaX.Hacks.Analysis;

public sealed class NitroFs
{
    public const int HeaderSize = 0x200;

    private readonly ReadOnlyMemory<byte> rom;
    private readonly Dictionary<string, int> paths = new(StringComparer.Ordinal);
    private readonly (int Start, int End)[] fat;

    public string GameCode { get; }
    public string Title { get; }
    public IReadOnlyCollection<string> Paths => paths.Keys;
    public int FileCount => fat.Length;

    public NitroFs(ReadOnlyMemory<byte> bytes)
    {
        rom = bytes;
        var s = bytes.Span;
        if (s.Length < HeaderSize)
            throw new ArgumentException($"the image is {s.Length} bytes, shorter than an NDS header (0x{HeaderSize:X}).");
        Title = Ascii(s.Slice(0, 12));
        GameCode = Ascii(s.Slice(0x0C, 4));
        var fntOffset = U32(s, 0x40);
        var fntSize = U32(s, 0x44);
        var fatOffset = U32(s, 0x48);
        var fatSize = U32(s, 0x4C);
        if (!Inside(s, fntOffset, fntSize) || !Inside(s, fatOffset, fatSize) || fatSize % 8 != 0 || fntSize < 8)
            throw new ArgumentException("the NDS header's FNT or FAT does not lie inside the image, so this is not a NitroFS image.");
        fat = new (int, int)[fatSize / 8];
        for (var i = 0; i < fat.Length; i++)
        {
            var start = U32(s, fatOffset + i * 8);
            var end = U32(s, fatOffset + i * 8 + 4);
            fat[i] = end >= start && end <= s.Length ? (start, end) : (0, 0);
        }
        var fnt = s.Slice(fntOffset, fntSize);
        var dirCount = BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(6));
        if (dirCount == 0 || dirCount > 4096 || dirCount * 8 > fnt.Length)
            throw new ArgumentException("the NDS file name table's directory count is not sane, so this is not a NitroFS image.");
        Walk(fnt, 0, "", dirCount, 0, new HashSet<int>());
    }

    private void Walk(ReadOnlySpan<byte> fnt, int dir, string prefix, int dirCount, int depth, HashSet<int> visited)
    {
        if (depth > 32 || !visited.Add(dir))
            return;
        var entry = dir * 8;
        var sub = (int)BinaryPrimitives.ReadUInt32LittleEndian(fnt.Slice(entry));
        var fileId = BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(entry + 4));
        var p = sub;
        while (p >= 0 && p < fnt.Length)
        {
            var len = fnt[p++];
            if (len == 0)
                break;
            var isDir = (len & 0x80) != 0;
            var nameLen = len & 0x7F;
            if (p + nameLen > fnt.Length)
                break;
            var name = Encoding.ASCII.GetString(fnt.Slice(p, nameLen));
            p += nameLen;
            if (isDir)
            {
                if (p + 2 > fnt.Length)
                    break;
                var child = BinaryPrimitives.ReadUInt16LittleEndian(fnt.Slice(p)) & 0x0FFF;
                p += 2;
                if (child > 0 && child < dirCount)
                    Walk(fnt, child, prefix + name + "/", dirCount, depth + 1, visited);
            }
            else
            {
                if (fileId < fat.Length)
                    paths.TryAdd(prefix + name, fileId);
                fileId++;
            }
        }
    }

    public bool Has(string path) => paths.ContainsKey(path);

    public ReadOnlyMemory<byte>? File(string path) => paths.TryGetValue(path, out var id) ? File(id) : null;

    public ReadOnlyMemory<byte>? File(int id)
    {
        if ((uint)id >= (uint)fat.Length)
            return null;
        var (start, end) = fat[id];
        return end > start ? rom.Slice(start, end - start) : null;
    }

    private static string Ascii(ReadOnlySpan<byte> s)
    {
        var end = s.IndexOf((byte)0);
        return Encoding.ASCII.GetString(end < 0 ? s : s.Slice(0, end));
    }

    private static int U32(ReadOnlySpan<byte> s, int offset) => (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(offset)), int.MaxValue);

    private static bool Inside(ReadOnlySpan<byte> s, int offset, int size) => offset >= 0 && size >= 0 && (long)offset + size <= s.Length;
}
