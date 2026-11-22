using System.Buffers;

namespace Isleep2late.Files.Services.Patch;

internal interface ISecondaryDecoder
{
	void Decode(ReadOnlySpan<byte> input, SectionBuffer output, int outputBytes, string where);

	void PassOver(ReadOnlySpan<byte> raw);
}

internal sealed class SectionBuffer : IDisposable
{
	private const int FirstCapacity = 1 << 16;

	private byte[] buffer = [];
	private int limit;

	public int Length { get; private set; }

	public int Capacity => buffer.Length;

	public ReadOnlySpan<byte> Span => buffer.AsSpan(0, Length);

	public void Start(int declared)
	{
		Length = 0;
		limit = declared;
	}

	public void Add(byte value)
	{
		if (Length == buffer.Length)
			Grow(Length + 1);
		buffer[Length++] = value;
	}

	public void Add(ReadOnlySpan<byte> values)
	{
		if (Length + values.Length > buffer.Length)
			Grow(Length + values.Length);
		values.CopyTo(buffer.AsSpan(Length));
		Length += values.Length;
	}

	private void Grow(int needed)
	{
		if (needed > limit)
			throw PatchReader.Corrupt($"a secondary decoder wrote past the {limit:N0} bytes its section declared; the patch is corrupt");
		var size = (int)Math.Min(limit, Math.Max(needed, Math.Max(FirstCapacity, (long)buffer.Length * 2)));
		var next = ArrayPool<byte>.Shared.Rent(size);
		buffer.AsSpan(0, Length).CopyTo(next);
		if (buffer.Length > 0)
			ArrayPool<byte>.Shared.Return(buffer);
		buffer = next;
	}

	public void Dispose()
	{
		if (buffer.Length > 0)
			ArrayPool<byte>.Shared.Return(buffer);
		buffer = [];
		Length = 0;
	}
}

internal static class SecondaryCompression
{
	public const int DjwId = 1;
	public const int LzmaId = 2;
	public const int FgkId = 16;

	public const int MinInput = 10;

	public static bool IsKnown(int id) => id is DjwId or LzmaId or FgkId;

	public static string Name(int id) => id switch
	{
		DjwId => "DJW (static Huffman)",
		LzmaId => "LZMA",
		FgkId => "FGK (adaptive Huffman)",
		_ => $"id {id}",
	};

	public static ISecondaryDecoder Create(int id) => id switch
	{
		DjwId => new DjwDecoder(),
		LzmaId => new LzmaSecondaryDecoder(),
		FgkId => new FgkDecoder(),
		_ => throw new ArgumentOutOfRangeException(nameof(id), id, "not a secondary compressor xdelta3 writes"),
	};
}
