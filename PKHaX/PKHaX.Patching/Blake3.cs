using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;

namespace Isleep2late.Files.Services.Patch;

internal sealed class Blake3
{
	public const int OutLength = 32;

	private const int BlockLength = 64;
	private const int ChunkLength = 1024;
	private const uint ChunkStart = 1;
	private const uint ChunkEnd = 2;
	private const uint Parent = 4;
	private const uint Root = 8;

	private static readonly uint[] Iv = [0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A, 0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19];

	private readonly uint[] cv = new uint[8];
	private readonly byte[] block = new byte[BlockLength];
	private readonly uint[] stack = new uint[54 * 8];
	private int blockLength;
	private int blocksCompressed;
	private ulong chunkCounter;
	private int stackLength;
	private bool finished;

	public Blake3()
	{
		Iv.CopyTo(cv, 0);
	}

	public long Length { get; private set; }

	public static byte[] Hash(ReadOnlySpan<byte> input)
	{
		var h = new Blake3();
		h.Update(input);
		return h.Finish();
	}

	public static byte[] Hash(Stream stream, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(stream);
		var h = new Blake3();
		var buffer = ArrayPool<byte>.Shared.Rent(1 << 20);
		try
		{
			int read;
			while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
			{
				ct.ThrowIfCancellationRequested();
				h.Update(buffer.AsSpan(0, read));
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(buffer);
		}
		return h.Finish();
	}

	public static string Hex(ReadOnlySpan<byte> digest) => Convert.ToHexStringLower(digest);

	private int ChunkFill => blocksCompressed * BlockLength + blockLength;

	private uint StartFlag => blocksCompressed == 0 ? ChunkStart : 0;

	public void Update(ReadOnlySpan<byte> input)
	{
		if (finished)
			throw new InvalidOperationException("this BLAKE3 hasher has already been finished");
		Length += input.Length;
		Span<uint> words = stackalloc uint[16];
		Span<uint> chunkCv = stackalloc uint[8];
		while (input.Length > 0)
		{
			if (ChunkFill == ChunkLength)
			{
				LoadWords(block, words);
				Compress(cv, words, chunkCounter, (uint)blockLength, StartFlag | ChunkEnd, chunkCv);
				AddChunk(chunkCv, chunkCounter + 1);
				ResetChunk(chunkCounter + 1);
			}
			if (ChunkFill == 0 && input.Length > ChunkLength)
			{
				Iv.CopyTo(chunkCv);
				for (var b = 0; b < ChunkLength / BlockLength; b++)
				{
					LoadWords(input.Slice(b * BlockLength, BlockLength), words);
					var flags = (b == 0 ? ChunkStart : 0) | (b == ChunkLength / BlockLength - 1 ? ChunkEnd : 0);
					Compress(chunkCv, words, chunkCounter, BlockLength, flags, chunkCv);
				}
				AddChunk(chunkCv, chunkCounter + 1);
				ResetChunk(chunkCounter + 1);
				input = input[ChunkLength..];
				continue;
			}
			var take = Math.Min(ChunkLength - ChunkFill, input.Length);
			var part = input[..take];
			input = input[take..];
			while (part.Length > 0)
			{
				if (blockLength == BlockLength)
				{
					LoadWords(block, words);
					Compress(cv, words, chunkCounter, BlockLength, StartFlag, cv);
					blocksCompressed++;
					blockLength = 0;
					Array.Clear(block);
				}
				var n = Math.Min(BlockLength - blockLength, part.Length);
				part[..n].CopyTo(block.AsSpan(blockLength));
				blockLength += n;
				part = part[n..];
			}
		}
	}

	public byte[] Finish()
	{
		if (finished)
			throw new InvalidOperationException("this BLAKE3 hasher has already been finished");
		finished = true;
		Span<uint> inputCv = stackalloc uint[8];
		Span<uint> words = stackalloc uint[16];
		Span<uint> outCv = stackalloc uint[8];
		cv.CopyTo(inputCv);
		LoadWords(block, words);
		var counter = chunkCounter;
		var length = (uint)blockLength;
		var flags = StartFlag | ChunkEnd;
		for (var i = stackLength - 1; i >= 0; i--)
		{
			Compress(inputCv, words, counter, length, flags, outCv);
			stack.AsSpan(i * 8, 8).CopyTo(words);
			outCv.CopyTo(words[8..]);
			Iv.CopyTo(inputCv);
			counter = 0;
			length = BlockLength;
			flags = Parent;
		}
		Compress(inputCv, words, counter, length, flags | Root, outCv);
		var digest = new byte[OutLength];
		for (var i = 0; i < 8; i++)
			BinaryPrimitives.WriteUInt32LittleEndian(digest.AsSpan(i * 4), outCv[i]);
		return digest;
	}

	private void ResetChunk(ulong counter)
	{
		Iv.CopyTo(cv, 0);
		chunkCounter = counter;
		blocksCompressed = 0;
		blockLength = 0;
		Array.Clear(block);
	}

	private void AddChunk(ReadOnlySpan<uint> chunkCv, ulong totalChunks)
	{
		Span<uint> merged = stackalloc uint[8];
		Span<uint> words = stackalloc uint[16];
		chunkCv.CopyTo(merged);
		while ((totalChunks & 1) == 0)
		{
			stackLength--;
			stack.AsSpan(stackLength * 8, 8).CopyTo(words);
			merged.CopyTo(words[8..]);
			Compress(Iv, words, 0, BlockLength, Parent, merged);
			totalChunks >>= 1;
		}
		merged.CopyTo(stack.AsSpan(stackLength * 8, 8));
		stackLength++;
	}

	private static void LoadWords(ReadOnlySpan<byte> bytes, Span<uint> words)
	{
		for (var i = 0; i < 16; i++)
			words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(i * 4, 4));
	}

	private static void Compress(ReadOnlySpan<uint> chaining, ReadOnlySpan<uint> m, ulong counter, uint length, uint flags, Span<uint> result)
	{
		uint s0 = chaining[0], s1 = chaining[1], s2 = chaining[2], s3 = chaining[3];
		uint s4 = chaining[4], s5 = chaining[5], s6 = chaining[6], s7 = chaining[7];
		uint s8 = 0x6A09E667, s9 = 0xBB67AE85, s10 = 0x3C6EF372, s11 = 0xA54FF53A;
		uint s12 = (uint)counter, s13 = (uint)(counter >> 32), s14 = length, s15 = flags;
		uint m0 = m[0], m1 = m[1], m2 = m[2], m3 = m[3], m4 = m[4], m5 = m[5], m6 = m[6], m7 = m[7];
		uint m8 = m[8], m9 = m[9], m10 = m[10], m11 = m[11], m12 = m[12], m13 = m[13], m14 = m[14], m15 = m[15];
		for (var round = 0; round < 7; round++)
		{
			s0 = s0 + s4 + m0;
			s12 = BitOperations.RotateRight(s12 ^ s0, 16);
			s8 += s12;
			s4 = BitOperations.RotateRight(s4 ^ s8, 12);
			s0 = s0 + s4 + m1;
			s12 = BitOperations.RotateRight(s12 ^ s0, 8);
			s8 += s12;
			s4 = BitOperations.RotateRight(s4 ^ s8, 7);
			s1 = s1 + s5 + m2;
			s13 = BitOperations.RotateRight(s13 ^ s1, 16);
			s9 += s13;
			s5 = BitOperations.RotateRight(s5 ^ s9, 12);
			s1 = s1 + s5 + m3;
			s13 = BitOperations.RotateRight(s13 ^ s1, 8);
			s9 += s13;
			s5 = BitOperations.RotateRight(s5 ^ s9, 7);
			s2 = s2 + s6 + m4;
			s14 = BitOperations.RotateRight(s14 ^ s2, 16);
			s10 += s14;
			s6 = BitOperations.RotateRight(s6 ^ s10, 12);
			s2 = s2 + s6 + m5;
			s14 = BitOperations.RotateRight(s14 ^ s2, 8);
			s10 += s14;
			s6 = BitOperations.RotateRight(s6 ^ s10, 7);
			s3 = s3 + s7 + m6;
			s15 = BitOperations.RotateRight(s15 ^ s3, 16);
			s11 += s15;
			s7 = BitOperations.RotateRight(s7 ^ s11, 12);
			s3 = s3 + s7 + m7;
			s15 = BitOperations.RotateRight(s15 ^ s3, 8);
			s11 += s15;
			s7 = BitOperations.RotateRight(s7 ^ s11, 7);
			s0 = s0 + s5 + m8;
			s15 = BitOperations.RotateRight(s15 ^ s0, 16);
			s10 += s15;
			s5 = BitOperations.RotateRight(s5 ^ s10, 12);
			s0 = s0 + s5 + m9;
			s15 = BitOperations.RotateRight(s15 ^ s0, 8);
			s10 += s15;
			s5 = BitOperations.RotateRight(s5 ^ s10, 7);
			s1 = s1 + s6 + m10;
			s12 = BitOperations.RotateRight(s12 ^ s1, 16);
			s11 += s12;
			s6 = BitOperations.RotateRight(s6 ^ s11, 12);
			s1 = s1 + s6 + m11;
			s12 = BitOperations.RotateRight(s12 ^ s1, 8);
			s11 += s12;
			s6 = BitOperations.RotateRight(s6 ^ s11, 7);
			s2 = s2 + s7 + m12;
			s13 = BitOperations.RotateRight(s13 ^ s2, 16);
			s8 += s13;
			s7 = BitOperations.RotateRight(s7 ^ s8, 12);
			s2 = s2 + s7 + m13;
			s13 = BitOperations.RotateRight(s13 ^ s2, 8);
			s8 += s13;
			s7 = BitOperations.RotateRight(s7 ^ s8, 7);
			s3 = s3 + s4 + m14;
			s14 = BitOperations.RotateRight(s14 ^ s3, 16);
			s9 += s14;
			s4 = BitOperations.RotateRight(s4 ^ s9, 12);
			s3 = s3 + s4 + m15;
			s14 = BitOperations.RotateRight(s14 ^ s3, 8);
			s9 += s14;
			s4 = BitOperations.RotateRight(s4 ^ s9, 7);
			if (round == 6)
				break;
			uint t0 = m0, t1 = m1, t2 = m2, t3 = m3, t4 = m4, t5 = m5, t6 = m6, t7 = m7, t8 = m8, t9 = m9, t10 = m10, t11 = m11, t12 = m12, t13 = m13, t14 = m14, t15 = m15;
			m0 = t2;
			m1 = t6;
			m2 = t3;
			m3 = t10;
			m4 = t7;
			m5 = t0;
			m6 = t4;
			m7 = t13;
			m8 = t1;
			m9 = t11;
			m10 = t12;
			m11 = t5;
			m12 = t9;
			m13 = t14;
			m14 = t15;
			m15 = t8;
		}
		result[0] = s0 ^ s8;
		result[1] = s1 ^ s9;
		result[2] = s2 ^ s10;
		result[3] = s3 ^ s11;
		result[4] = s4 ^ s12;
		result[5] = s5 ^ s13;
		result[6] = s6 ^ s14;
		result[7] = s7 ^ s15;
	}
}
