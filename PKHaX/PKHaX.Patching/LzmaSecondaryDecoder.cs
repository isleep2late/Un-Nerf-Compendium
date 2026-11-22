namespace Isleep2late.Files.Services.Patch;

internal sealed class LzmaSecondaryDecoder : ISecondaryDecoder
{
	private static ReadOnlySpan<byte> XzMagic => [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00];

	private const long MaxDictionary = 256L << 20;

	private const byte Lzma2FilterId = 0x21;

	private enum Phase
	{
		StreamHeader,
		InBlock,
		Ended,
	}

	internal static bool ProofSkipDirectNormalise { get; set; }

	private readonly LzmaModel model = new();
	private readonly LzmaDictionary dictionary = new();
	private Phase phase = Phase.StreamHeader;
	private bool needDictionaryReset = true;
	private bool needProperties = true;

	public long HeldBytes => dictionary.Capacity + model.HeldBytes;

	public void PassOver(ReadOnlySpan<byte> raw)
	{
	}

	public void Decode(ReadOnlySpan<byte> input, SectionBuffer output, int outputBytes, string where)
	{
		var at = 0;
		if (phase == Phase.StreamHeader)
		{
			ReadStreamHeader(input, ref at, where);
			ReadBlockHeader(input, ref at, where);
			phase = Phase.InBlock;
		}
		if (phase == Phase.Ended)
			throw PatchReader.Corrupt($"{where}: the LZMA stream for this section kind already ended in an earlier window, which xdelta3 never does; the patch is corrupt");

		var produced = 0;
		while (produced < outputBytes)
		{
			if (at >= input.Length)
				throw PatchReader.Corrupt($"{where}: the LZMA data ends after {produced:N0} of the section's {outputBytes:N0} bytes; the patch is truncated or corrupt");
			int control = input[at++];
			if (control == 0x00)
			{
				phase = Phase.Ended;
				throw PatchReader.Corrupt($"{where}: the LZMA stream ends after {produced:N0} of the section's {outputBytes:N0} bytes; the patch is corrupt");
			}
			if (control >= 0xE0 || control == 0x01)
			{
				needProperties = true;
				needDictionaryReset = true;
			}
			else if (needDictionaryReset)
				throw PatchReader.Corrupt($"{where}: the first LZMA2 chunk (control 0x{control:X2}) does not reset the dictionary; the patch is corrupt");

			if (control >= 0x80)
			{
				var head = Take(input, ref at, 4, where);
				var unpacked = ((control & 0x1F) << 16) + (head[0] << 8) + head[1] + 1;
				var packed = (head[2] << 8) + head[3] + 1;
				if (control >= 0xC0)
				{
					model.SetProperties(Take(input, ref at, 1, where)[0], where);
					needProperties = false;
					model.Reset();
				}
				else if (needProperties)
					throw PatchReader.Corrupt($"{where}: an LZMA2 chunk (control 0x{control:X2}) reuses properties that were never set; the patch is corrupt");
				else if (control >= 0xA0)
					model.Reset();
				if (needDictionaryReset)
				{
					dictionary.Reset();
					needDictionaryReset = false;
				}
				if (unpacked > outputBytes - produced)
					throw PatchReader.Corrupt($"{where}: an LZMA2 chunk of {unpacked:N0} bytes runs past the section's {outputBytes:N0}; the patch is corrupt");
				var chunk = Take(input, ref at, packed, where);
				model.DecodeChunk(chunk, unpacked, dictionary, output, where);
				produced += unpacked;
			}
			else if (control <= 0x02)
			{
				var head = Take(input, ref at, 2, where);
				var size = (head[0] << 8) + head[1] + 1;
				if (needDictionaryReset)
				{
					dictionary.Reset();
					needDictionaryReset = false;
				}
				if (size > outputBytes - produced)
					throw PatchReader.Corrupt($"{where}: a stored LZMA2 chunk of {size:N0} bytes runs past the section's {outputBytes:N0}; the patch is corrupt");
				var stored = Take(input, ref at, size, where);
				foreach (var b in stored)
					dictionary.Put(b);
				output.Add(stored);
				produced += size;
			}
			else
				throw PatchReader.Corrupt($"{where}: 0x{control:X2} is not an LZMA2 chunk type; the patch is corrupt");
		}
		if (at != input.Length)
			throw PatchReader.Corrupt($"{where}: the LZMA decoder finished {input.Length - at:N0} byte(s) before the section did; the patch is corrupt");
	}

	private static ReadOnlySpan<byte> Take(ReadOnlySpan<byte> input, ref int at, int count, string where)
	{
		if (count > input.Length - at)
			throw PatchReader.Corrupt($"{where}: the LZMA data needs {count:N0} more byte(s) at offset {at:N0} and the section has {input.Length - at:N0}; the patch is truncated or corrupt");
		var slice = input.Slice(at, count);
		at += count;
		return slice;
	}

	private static void ReadStreamHeader(ReadOnlySpan<byte> input, ref int at, string where)
	{
		var header = Take(input, ref at, 12, where);
		if (!header[..6].SequenceEqual(XzMagic))
			throw PatchReader.Corrupt($"{where}: the first LZMA section does not start with the .xz stream header (FD 37 7A 58 5A 00) xdelta3 writes; the patch is corrupt");
		if (header[6] != 0 || (header[7] & 0xF0) != 0)
			throw PatchReader.Corrupt($"{where}: the .xz stream flags 0x{header[6]:X2}{header[7]:X2} have reserved bits set; the patch is corrupt");
		var crc = (uint)(header[8] | (header[9] << 8) | (header[10] << 16) | (header[11] << 24));
		if (Checksums.Crc32(header.Slice(6, 2)) != crc)
			throw PatchReader.Corrupt($"{where}: the .xz stream header's CRC-32 does not match its flags; the patch is corrupt");
	}

	private void ReadBlockHeader(ReadOnlySpan<byte> input, ref int at, string where)
	{
		if (at >= input.Length)
			throw PatchReader.Corrupt($"{where}: the .xz stream has no block header; the patch is truncated or corrupt");
		if (input[at] == 0x00)
			throw PatchReader.Corrupt($"{where}: the .xz stream has no data block (its index starts at once); the patch is corrupt");
		var size = (input[at] + 1) * 4;
		var header = Take(input, ref at, size, where);
		var crc = (uint)(header[size - 4] | (header[size - 3] << 8) | (header[size - 2] << 16) | (header[size - 1] << 24));
		if (Checksums.Crc32(header[..(size - 4)]) != crc)
			throw PatchReader.Corrupt($"{where}: the .xz block header's CRC-32 does not match; the patch is corrupt");
		var flags = header[1];
		if ((flags & 0x3C) != 0)
			throw PatchReader.Corrupt($"{where}: the .xz block flags 0x{flags:X2} have reserved bits set; the patch is corrupt");
		var p = 2;
		if ((flags & 0x40) != 0)
			ReadVli(header, ref p, size - 4, where);
		if ((flags & 0x80) != 0)
			ReadVli(header, ref p, size - 4, where);
		var filters = (flags & 0x03) + 1;
		var id = ReadVli(header, ref p, size - 4, where);
		var propsSize = ReadVli(header, ref p, size - 4, where);
		if (filters != 1 || id != Lzma2FilterId || propsSize != 1)
			throw new PatchException(PatchFailure.Unsupported,
				$"{where}: the LZMA stream is filtered through {filters} filter(s) starting with id 0x{id:X}, and xdelta3 writes exactly one, LZMA2 (0x21); this decoder applies only what xdelta3 writes");
		if (p >= size - 4)
			throw PatchReader.Corrupt($"{where}: the .xz block header ends inside its filter properties; the patch is corrupt");
		var dictBits = header[p++];
		if (dictBits > 40)
			throw PatchReader.Corrupt($"{where}: the LZMA2 dictionary size byte {dictBits} is over 40; the patch is corrupt");
		for (; p < size - 4; p++)
			if (header[p] != 0)
				throw PatchReader.Corrupt($"{where}: the .xz block header's padding is not zero; the patch is corrupt");
		var dictSize = dictBits == 40 ? uint.MaxValue : (2L | (dictBits & 1L)) << (dictBits / 2 + 11);
		dictionary.Configure(Math.Min(dictSize, MaxDictionary));
	}

	private static long ReadVli(ReadOnlySpan<byte> header, ref int p, int end, string where)
	{
		long value = 0;
		for (var i = 0; i < 9; i++)
		{
			if (p >= end)
				break;
			var b = header[p++];
			value |= (long)(b & 0x7F) << (7 * i);
			if ((b & 0x80) == 0)
			{
				if (b == 0 && i != 0)
					break;
				return value;
			}
		}
		throw PatchReader.Corrupt($"{where}: a number in the .xz block header is not a valid variable-length integer; the patch is corrupt");
	}

	private sealed class LzmaDictionary
	{
		private byte[] buffer = [];
		private int position;
		private bool full;
		private long size = 1 << 16;

		public long Capacity => buffer.Length;

		public long TotalSinceReset { get; private set; }

		public long Available => full ? buffer.Length : position;

		public void Configure(long dictionarySize) => size = Math.Max(dictionarySize, 4096);

		public void Reset()
		{
			position = 0;
			full = false;
			TotalSinceReset = 0;
		}

		public void Put(byte value)
		{
			if (position == buffer.Length)
			{
				if (buffer.Length < size)
				{
					var next = new byte[(int)Math.Min(size, Math.Max(1L << 16, (long)buffer.Length * 2))];
					buffer.AsSpan(0, position).CopyTo(next);
					buffer = next;
				}
				else
				{
					position = 0;
					full = true;
				}
			}
			buffer[position++] = value;
			TotalSinceReset++;
		}

		public byte Get(long distance)
		{
			if (distance >= Available)
				return 0;
			var index = position - distance - 1;
			if (index < 0)
				index += buffer.Length;
			return buffer[index];
		}
	}

	private sealed class LzmaModel
	{
		private const int NumStates = 12;
		private const int PosStatesMax = 16;
		private const int LenToPosStates = 4;
		private const int AlignBits = 4;
		private const int EndPosModelIndex = 14;
		private const int FullDistances = 128;
		private const int MatchMinLength = 2;
		private const ushort ProbInit = 1024;

		private int lc;
		private int lp;
		private int pb;
		private ushort[] literal = [];
		private readonly ushort[] isMatch = new ushort[NumStates * PosStatesMax];
		private readonly ushort[] isRep = new ushort[NumStates];
		private readonly ushort[] isRepG0 = new ushort[NumStates];
		private readonly ushort[] isRepG1 = new ushort[NumStates];
		private readonly ushort[] isRepG2 = new ushort[NumStates];
		private readonly ushort[] isRep0Long = new ushort[NumStates * PosStatesMax];
		private readonly ushort[] posSlot = new ushort[LenToPosStates * 64];
		private readonly ushort[] posSpecial = new ushort[FullDistances - EndPosModelIndex];
		private readonly ushort[] align = new ushort[1 << AlignBits];
		private readonly LengthCoder matchLength = new();
		private readonly LengthCoder repLength = new();
		private int state;
		private long rep0;
		private long rep1;
		private long rep2;
		private long rep3;

		public long HeldBytes => literal.Length * 2L;

		public void SetProperties(byte properties, string where)
		{
			int d = properties;
			if (d >= 9 * 5 * 5)
				throw PatchReader.Corrupt($"{where}: LZMA properties byte 0x{properties:X2} is out of range; the patch is corrupt");
			lc = d % 9;
			d /= 9;
			lp = d % 5;
			pb = d / 5;
			if (lc + lp > 4 || pb > 4)
				throw PatchReader.Corrupt($"{where}: LZMA properties lc={lc} lp={lp} pb={pb} are outside what LZMA2 allows (lc + lp <= 4, pb <= 4); the patch is corrupt");
			var size = 0x300 << (lc + lp);
			if (literal.Length != size)
				literal = new ushort[size];
		}

		public void Reset()
		{
			Array.Fill(literal, ProbInit);
			Array.Fill(isMatch, ProbInit);
			Array.Fill(isRep, ProbInit);
			Array.Fill(isRepG0, ProbInit);
			Array.Fill(isRepG1, ProbInit);
			Array.Fill(isRepG2, ProbInit);
			Array.Fill(isRep0Long, ProbInit);
			Array.Fill(posSlot, ProbInit);
			Array.Fill(posSpecial, ProbInit);
			Array.Fill(align, ProbInit);
			matchLength.Reset();
			repLength.Reset();
			state = 0;
			rep0 = rep1 = rep2 = rep3 = 0;
		}

		public void DecodeChunk(ReadOnlySpan<byte> packed, int unpacked, LzmaDictionary dict, SectionBuffer output, string where)
		{
			var rc = new RangeDecoder(packed, where);
			var pbMask = (1 << pb) - 1;
			var lpMask = (1 << lp) - 1;
			var remaining = unpacked;
			while (remaining > 0)
			{
				var pos = dict.TotalSinceReset;
				var posState = (int)(pos & pbMask);
				if (rc.Bit(isMatch, (state << 4) + posState) == 0)
				{
					var prev = dict.Get(0);
					var probs = 0x300 * ((((int)pos & lpMask) << lc) + (prev >> (8 - lc)));
					int symbol = 1;
					if (state < 7)
					{
						while (symbol < 0x100)
							symbol = (symbol << 1) | rc.Bit(literal, probs + symbol);
					}
					else
					{
						if (rep0 >= dict.Available)
							throw PatchReader.Corrupt($"{where}: an LZMA literal refers {rep0 + 1:N0} bytes back into a history of {dict.Available:N0}; the patch is corrupt");
						int matchByte = dict.Get(rep0);
						while (symbol < 0x100)
						{
							var matchBit = (matchByte >> 7) & 1;
							matchByte <<= 1;
							var bit = rc.Bit(literal, probs + 0x100 + (matchBit << 8) + symbol);
							symbol = (symbol << 1) | bit;
							if (matchBit != bit)
								break;
						}
						while (symbol < 0x100)
							symbol = (symbol << 1) | rc.Bit(literal, probs + symbol);
					}
					var value = (byte)symbol;
					dict.Put(value);
					output.Add(value);
					remaining--;
					state = state < 4 ? 0 : state < 10 ? state - 3 : state - 6;
					continue;
				}

				int length;
				if (rc.Bit(isRep, state) == 0)
				{
					length = matchLength.Decode(ref rc, posState);
					state = state < 7 ? 7 : 10;
					var distance = DecodeDistance(ref rc, length);
					if (distance == 0xFFFFFFFFL)
						throw PatchReader.Corrupt($"{where}: an LZMA end-of-stream marker inside an LZMA2 chunk, which LZMA2 never writes; the patch is corrupt");
					rep3 = rep2;
					rep2 = rep1;
					rep1 = rep0;
					rep0 = distance;
				}
				else
				{
					if (rc.Bit(isRepG0, state) == 0)
					{
						if (rc.Bit(isRep0Long, (state << 4) + posState) == 0)
						{
							if (rep0 >= dict.Available)
								throw PatchReader.Corrupt($"{where}: an LZMA short repeat refers {rep0 + 1:N0} bytes back into a history of {dict.Available:N0}; the patch is corrupt");
							state = state < 7 ? 9 : 11;
							var value = dict.Get(rep0);
							dict.Put(value);
							output.Add(value);
							remaining--;
							continue;
						}
					}
					else
					{
						long distance;
						if (rc.Bit(isRepG1, state) == 0)
							distance = rep1;
						else
						{
							if (rc.Bit(isRepG2, state) == 0)
								distance = rep2;
							else
							{
								distance = rep3;
								rep3 = rep2;
							}
							rep2 = rep1;
						}
						rep1 = rep0;
						rep0 = distance;
					}
					length = repLength.Decode(ref rc, posState);
					state = state < 7 ? 8 : 11;
				}

				var count = length + MatchMinLength;
				if (rep0 >= dict.Available)
					throw PatchReader.Corrupt($"{where}: an LZMA match refers {rep0 + 1:N0} bytes back into a history of {dict.Available:N0}; the patch is corrupt");
				if (count > remaining)
					throw PatchReader.Corrupt($"{where}: an LZMA match of {count} bytes runs past its LZMA2 chunk; the patch is corrupt");
				for (var i = 0; i < count; i++)
				{
					var value = dict.Get(rep0);
					dict.Put(value);
					output.Add(value);
				}
				remaining -= count;
			}
			if (rc.Position != packed.Length)
				throw PatchReader.Corrupt($"{where}: an LZMA2 chunk decoded from {rc.Position:N0} of its {packed.Length:N0} bytes; the patch is corrupt");
		}

		private long DecodeDistance(ref RangeDecoder rc, int length)
		{
			var lenState = Math.Min(length, LenToPosStates - 1);
			var slot = rc.BitTree(posSlot, lenState * 64, 6);
			if (slot < 4)
				return slot;
			var directBits = (slot >> 1) - 1;
			long distance = (2L | (slot & 1)) << directBits;
			if (slot < EndPosModelIndex)
				distance += rc.ReverseBitTree(posSpecial, (int)(distance - slot - 1), directBits);
			else
			{
				distance += (long)rc.DirectBits(directBits - AlignBits, ProofSkipDirectNormalise) << AlignBits;
				distance += rc.ReverseBitTree(align, 0, AlignBits);
			}
			return distance;
		}

		private sealed class LengthCoder
		{
			private readonly ushort[] choice = new ushort[2];
			private readonly ushort[] low = new ushort[PosStatesMax << 3];
			private readonly ushort[] mid = new ushort[PosStatesMax << 3];
			private readonly ushort[] high = new ushort[256];

			public void Reset()
			{
				Array.Fill(choice, ProbInit);
				Array.Fill(low, ProbInit);
				Array.Fill(mid, ProbInit);
				Array.Fill(high, ProbInit);
			}

			public int Decode(ref RangeDecoder rc, int posState)
			{
				if (rc.Bit(choice, 0) == 0)
					return rc.BitTree(low, posState << 3, 3);
				if (rc.Bit(choice, 1) == 0)
					return 8 + rc.BitTree(mid, posState << 3, 3);
				return 16 + rc.BitTree(high, 0, 8);
			}
		}
	}

	private ref struct RangeDecoder
	{
		private const uint TopValue = 1u << 24;

		private readonly ReadOnlySpan<byte> input;
		private readonly string where;
		private uint range;
		private uint code;

		public int Position { get; private set; }

		public RangeDecoder(ReadOnlySpan<byte> input, string where)
		{
			this.input = input;
			this.where = where;
			if (input.Length < 5)
				throw PatchReader.Corrupt($"{where}: an LZMA2 chunk of {input.Length} byte(s) is too short to start a range decoder; the patch is corrupt");
			if (input[0] != 0)
				throw PatchReader.Corrupt($"{where}: an LZMA2 chunk's range coder does not start with a zero byte; the patch is corrupt");
			code = (uint)((input[1] << 24) | (input[2] << 16) | (input[3] << 8) | input[4]);
			range = uint.MaxValue;
			Position = 5;
			if (code == range)
				throw PatchReader.Corrupt($"{where}: an LZMA2 chunk's range coder starts at its maximum; the patch is corrupt");
		}

		private void Normalise()
		{
			if (range >= TopValue)
				return;
			if (Position >= input.Length)
				throw PatchReader.Corrupt($"{where}: an LZMA2 chunk's range coder wants more bytes than the chunk holds; the patch is truncated or corrupt");
			range <<= 8;
			code = (code << 8) | input[Position++];
		}

		public int Bit(ushort[] probs, int index)
		{
			var p = probs[index];
			var bound = (range >> 11) * p;
			int bit;
			if (code < bound)
			{
				range = bound;
				probs[index] = (ushort)(p + ((2048 - p) >> 5));
				bit = 0;
			}
			else
			{
				range -= bound;
				code -= bound;
				probs[index] = (ushort)(p - (p >> 5));
				bit = 1;
			}
			Normalise();
			return bit;
		}

		public int BitTree(ushort[] probs, int offset, int bits)
		{
			var m = 1;
			for (var i = 0; i < bits; i++)
				m = (m << 1) | Bit(probs, offset + m);
			return m - (1 << bits);
		}

		public int ReverseBitTree(ushort[] probs, int offset, int bits)
		{
			var m = 1;
			var symbol = 0;
			for (var i = 0; i < bits; i++)
			{
				var bit = Bit(probs, offset + m);
				m = (m << 1) | bit;
				symbol |= bit << i;
			}
			return symbol;
		}

		public uint DirectBits(int count, bool skipNormalise)
		{
			uint result = 0;
			for (var i = 0; i < count; i++)
			{
				range >>= 1;
				if (code >= range)
				{
					code -= range;
					result = (result << 1) | 1;
				}
				else
					result <<= 1;
				if (!skipNormalise)
					Normalise();
			}
			return result;
		}
	}
}
