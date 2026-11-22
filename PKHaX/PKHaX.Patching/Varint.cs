using System.Buffers.Binary;
using System.Text;

namespace Isleep2late.Files.Services.Patch;

internal ref struct PatchReader
{
	private readonly ReadOnlySpan<byte> data;

	public int Position;

	public PatchReader(ReadOnlySpan<byte> data, int position = 0)
	{
		this.data = data;
		Position = position;
	}

	public readonly int Length => data.Length;

	public readonly int Remaining => data.Length - Position;

	public readonly ReadOnlySpan<byte> Peek(int count) =>
		Remaining >= count ? data.Slice(Position, count) : ReadOnlySpan<byte>.Empty;

	public byte ReadByte()
	{
		if (Remaining < 1)
			throw Truncated(1);
		return data[Position++];
	}

	public ReadOnlySpan<byte> ReadBytes(long count)
	{
		if (count < 0 || count > Remaining)
			throw Truncated(count);
		var slice = data.Slice(Position, (int)count);
		Position += (int)count;
		return slice;
	}

	public ushort ReadU16BE() => BinaryPrimitives.ReadUInt16BigEndian(ReadBytes(2));

	public uint ReadU24BE()
	{
		var b = ReadBytes(3);
		return ((uint)b[0] << 16) | ((uint)b[1] << 8) | b[2];
	}

	public uint ReadU32BE() => BinaryPrimitives.ReadUInt32BigEndian(ReadBytes(4));

	public uint ReadU32LE() => BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));

	public long ReadByuuVarint()
	{
		ulong value = 0;
		ulong shift = 1;
		for (var i = 0; i < 10; i++)
		{
			var x = ReadByte();
			value += (x & 0x7Ful) * shift;
			if ((x & 0x80) != 0)
			{
				if (value > long.MaxValue)
					break;
				return (long)value;
			}
			shift <<= 7;
			value += shift;
		}
		throw new PatchException(PatchFailure.Corrupt, $"a length at patch offset 0x{Position:X} does not fit in 63 bits; the patch is corrupt");
	}

	public long ReadVcdiffInt()
	{
		ulong value = 0;
		for (var i = 0; i < 10; i++)
		{
			var x = ReadByte();
			value = (value << 7) | (x & 0x7Ful);
			if ((x & 0x80) == 0)
			{
				if (value > long.MaxValue)
					break;
				return (long)value;
			}
		}
		throw new PatchException(PatchFailure.Corrupt, $"an integer at patch offset 0x{Position:X} does not fit in 63 bits; the patch is corrupt");
	}

	public int ReadVcdiffLength(string what, long limit = int.MaxValue)
	{
		var at = Position;
		var v = ReadVcdiffInt();
		if (v > limit)
			throw new PatchException(PatchFailure.Corrupt, $"the {what} at patch offset 0x{at:X} is {v:N0} bytes, more than the {limit:N0} this decoder will believe; the patch is corrupt");
		return (int)v;
	}

	public readonly bool StartsWith(ReadOnlySpan<byte> magic) => data.Length >= magic.Length && data[..magic.Length].SequenceEqual(magic);

	public static PatchException Corrupt(string message) => new(PatchFailure.Corrupt, message);

	private readonly PatchException Truncated(long wanted) =>
		new(PatchFailure.Corrupt, $"the patch ends at byte {data.Length:N0} but {wanted:N0} more byte(s) were needed at offset 0x{Position:X}; the file is truncated or corrupt");

	public static string Ascii(ReadOnlySpan<byte> bytes)
	{
		var sb = new StringBuilder(bytes.Length);
		foreach (var b in bytes)
			sb.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
		return sb.ToString();
	}
}
