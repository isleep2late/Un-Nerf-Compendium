using System.Buffers;

namespace Isleep2late.Files.Services.Patch;

public static class Checksums
{
	private static readonly uint[] Crc32Table = BuildCrc32Table();

	private static uint[] BuildCrc32Table()
	{
		var table = new uint[256];
		for (uint n = 0; n < 256; n++)
		{
			var c = n;
			for (var k = 0; k < 8; k++)
				c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
			table[n] = c;
		}
		return table;
	}

	public static uint Crc32(ReadOnlySpan<byte> data) => Crc32Update(0, data);

	public static uint Crc32Update(uint crc, ReadOnlySpan<byte> data)
	{
		var c = ~crc;
		foreach (var b in data)
			c = Crc32Table[(c ^ b) & 0xFF] ^ (c >> 8);
		return ~c;
	}

	public static uint Crc32(Stream s)
	{
		var buffer = ArrayPool<byte>.Shared.Rent(1 << 16);
		try
		{
			uint crc = 0;
			int read;
			while ((read = s.Read(buffer, 0, buffer.Length)) > 0)
				crc = Crc32Update(crc, buffer.AsSpan(0, read));
			return crc;
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
	}

	public static uint Adler32(ReadOnlySpan<byte> data) => Adler32Update(1, data);

	public static uint Adler32Update(uint adler, ReadOnlySpan<byte> data)
	{
		const uint mod = 65521;
		const int block = 5552;
		var a = adler & 0xFFFF;
		var b = adler >> 16;
		while (data.Length > 0)
		{
			var n = Math.Min(block, data.Length);
			foreach (var x in data[..n])
			{
				a += x;
				b += a;
			}
			a %= mod;
			b %= mod;
			data = data[n..];
		}
		return (b << 16) | a;
	}
}
