using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace PKHaX.Hacks.Analysis;

public sealed class GbaImage
{
    public const uint RomBase = 0x08000000;
    public const int MaxLength = 0x2000000;

    private readonly ReadOnlyMemory<byte> data;

    public GbaImage(ReadOnlyMemory<byte> data)
    {
        if (data.Length is < 0x200 or > MaxLength)
            throw new ArgumentException($"a GBA image is 512 bytes to 32 MiB; this one is {data.Length} bytes", nameof(data));
        this.data = data;
        Sha1 = Convert.ToHexStringLower(SHA1.HashData(data.Span));
    }

    public string Sha1 { get; }

    public int Length => data.Length;

    public ReadOnlySpan<byte> Span => data.Span;

    public bool Has(int offset, int length) => offset >= 0 && length >= 0 && (long)offset + length <= data.Length;

    public byte U8(int offset) => data.Span[offset];

    public ushort U16(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.Span[offset..]);

    public uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Span[offset..]);

    public ReadOnlySpan<byte> Slice(int offset, int length) => data.Span.Slice(offset, length);

    public int? Offset(uint pointer)
    {
        if (pointer < RomBase || pointer >= RomBase + (uint)data.Length)
            return null;
        return (int)(pointer - RomBase);
    }

    public int? PointerAt(int offset) => Has(offset, 4) ? Offset(U32(offset)) : null;

    public string GameCode => System.Text.Encoding.ASCII.GetString(data.Span.Slice(0xAC, 4));
}
