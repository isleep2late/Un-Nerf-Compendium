using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PKHaX.Hacks.Analysis;

public static class IvfcRomFs
{
    public const int Level3HeaderLength = 0x28;

    public static long Level3Offset(ReadOnlySpan<byte> header)
    {
        if (header.Length < 0x60 || header[0] != (byte)'I' || header[1] != (byte)'V' || header[2] != (byte)'F' || header[3] != (byte)'C')
            throw new ArgumentException("the file does not start with an IVFC header, so it is not a decrypted romfs.bin.");
        var masterHashSize = BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(0x08));
        var blockLog = (int)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(0x4C));
        if (blockLog is < 1 or > 24)
            throw new ArgumentException($"the IVFC level 3 block size 2^{blockLog} is not sane.");
        var block = 1L << blockLog;
        var start = 0x60L + masterHashSize;
        return (start + block - 1) / block * block;
    }

    public static int Extract(string romfsBin, string outFolder)
    {
        using var f = File.OpenRead(romfsBin);
        var head = new byte[0x60];
        f.ReadExactly(head);
        var l3 = Level3Offset(head);
        var h = new byte[Level3HeaderLength];
        f.Position = l3;
        f.ReadExactly(h);
        if (BinaryPrimitives.ReadUInt32LittleEndian(h) != Level3HeaderLength)
            throw new ArgumentException("the IVFC level 3 header length is not 0x28.");
        var dirMeta = Read(f, l3 + BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(0x0C)), BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(0x10)));
        var fileMeta = Read(f, l3 + BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(0x1C)), BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(0x20)));
        var dataStart = l3 + BinaryPrimitives.ReadUInt32LittleEndian(h.AsSpan(0x24));
        var count = 0;
        var steps = 0;
        var seenDirs = new HashSet<uint>();
        var seenFiles = new HashSet<uint>();
        var outRoot = Path.GetFullPath(outFolder);
        Directory.CreateDirectory(outRoot);
        void Dir(uint offset, string path, int depth)
        {
            if (depth > 64 || offset + 0x18 > dirMeta.Length || !seenDirs.Add(offset))
                return;
            var d = dirMeta.AsSpan((int)offset);
            var child = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(0x08));
            var file = BinaryPrimitives.ReadUInt32LittleEndian(d.Slice(0x0C));
            for (var fo = file; fo != 0xFFFFFFFF && fo + 0x20 <= fileMeta.Length && ++steps < 400000 && seenFiles.Add(fo);)
            {
                var e = fileMeta.AsSpan((int)fo);
                var nameLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(0x1C));
                var name = Name(fileMeta, (int)fo + 0x20, nameLen);
                var target = Path.GetFullPath(Path.Combine(outRoot, path, name));
                if (name.Length > 0 && name != "." && name != ".." && target.StartsWith(outRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    var at = dataStart + (long)BinaryPrimitives.ReadUInt64LittleEndian(e.Slice(0x08));
                    var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(e.Slice(0x10));
                    if (size is >= 0 and < int.MaxValue && at + size <= f.Length)
                    {
                        File.WriteAllBytes(target, Read(f, at, size));
                        count++;
                    }
                }
                fo = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(0x04));
            }
            for (var co = child; co != 0xFFFFFFFF && co + 0x18 <= dirMeta.Length && ++steps < 400000;)
            {
                var c = dirMeta.AsSpan((int)co);
                var nameLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(c.Slice(0x14));
                var name = Name(dirMeta, (int)co + 0x18, nameLen);
                if (name.Length > 0 && name != "." && name != "..")
                    Dir(co, Path.Combine(path, name), depth + 1);
                co = BinaryPrimitives.ReadUInt32LittleEndian(c.Slice(0x04));
            }
        }
        Dir(0, "", 0);
        return count;
    }

    private static byte[] Read(Stream f, long at, long size)
    {
        if (at < 0 || size < 0 || at + size > f.Length || size > int.MaxValue)
            throw new ArgumentException("an IVFC level 3 table lies outside the file.");
        var b = new byte[size];
        f.Position = at;
        f.ReadExactly(b);
        return b;
    }

    private static string Name(byte[] meta, int at, int length) =>
        length <= 0 || length % 2 != 0 || at + length > meta.Length ? "" : Encoding.Unicode.GetString(meta, at, length).Replace('\\', '_').Replace('/', '_');
}
