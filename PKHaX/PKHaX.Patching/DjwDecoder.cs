namespace Isleep2late.Files.Services.Patch;

internal sealed class DjwDecoder : ISecondaryDecoder
{
	private const int Alphabet = 256;
	private const int MaxCodeLength = 20;
	private const int TotalCodes = MaxCodeLength + 2;
	private const int Run0 = 0;
	private const int Run1 = 1;
	private const int BasicCodes = 5;
	private const int RunCodes = 2;
	private const int Extra12Offset = BasicCodes + RunCodes;
	private const int ExtraCodeBits = 4;
	private const int GroupBits = 3;
	private const int SectorSizeMultiple = 5;
	private const int SectorSizeBits = 5;
	private const int MaxClcLength = 15;
	private const int ClcLengthBits = 4;
	private const int MaxGbcLength = 7;
	private const int GbcLengthBits = 3;

	private static ReadOnlySpan<byte> ClenMtfStart => [0, 4, 5, 6, 7, 8, 9, 10, 3, 11, 2, 12, 13, 1, 14, 15, 16, 17, 18, 19, 20];

	internal static bool ProofSelectorOffByOne { get; set; }

	public void PassOver(ReadOnlySpan<byte> raw)
	{
	}

	public void Decode(ReadOnlySpan<byte> input, SectionBuffer output, int outputBytes, string where)
	{
		if (outputBytes > (long)input.Length * 8)
			throw PatchReader.Corrupt($"{where}: {input.Length:N0} byte(s) of DJW cannot hold the {outputBytes:N0} bytes the section declares; the patch is corrupt");
		var bits = new BitReader(input, where);
		var groups = bits.Read(GroupBits) + 1;
		var sectorSize = groups > 1 ? (bits.Read(SectorSizeBits) + 1) * SectorSizeMultiple : outputBytes;
		var sectors = 1 + (outputBytes - 1) / sectorSize;

		var numCodes = bits.Read(ExtraCodeBits) + Extra12Offset;
		var clClen = new byte[TotalCodes];
		for (var i = 0; i < numCodes; i++)
			clClen[i] = (byte)bits.Read(ClcLengthBits);
		var clTable = new Canonical(clClen, MaxClcLength);
		var clMtf = ClenMtfStart.ToArray();
		var clen = new byte[groups * Alphabet];
		DecodeOneTwo(ref bits, clTable, clMtf, clen, Alphabet, where);
		var tables = new Canonical[groups];
		for (var g = 0; g < groups; g++)
			tables[g] = new Canonical(clen.AsSpan(g * Alphabet, Alphabet), MaxCodeLength);

		byte[]? selectors = null;
		if (groups > 1)
		{
			var selClen = new byte[groups + 1];
			var selMtf = new byte[groups + 1];
			for (var g = 0; g <= groups; g++)
			{
				selClen[g] = (byte)bits.Read(GbcLengthBits);
				selMtf[g] = (byte)g;
			}
			var selTable = new Canonical(selClen, MaxGbcLength);
			selectors = new byte[sectors];
			DecodeOneTwo(ref bits, selTable, selMtf, selectors, 0, where);
		}

		var remaining = outputBytes;
		for (var c = 0; c < sectors; c++)
		{
			var table = tables[0];
			if (selectors is not null)
			{
				var pick = selectors[ProofSelectorOffByOne ? Math.Min(c + 1, sectors - 1) : c];
				if (pick >= groups)
					throw PatchReader.Corrupt($"{where}: DJW sector {c} selects table {pick} of {groups}; the patch is corrupt");
				table = tables[pick];
			}
			var n = Math.Min(sectorSize, remaining);
			for (var i = 0; i < n; i++)
				output.Add((byte)table.Decode(ref bits, where));
			remaining -= n;
		}

		if (bits.Position != input.Length)
			throw PatchReader.Corrupt($"{where}: the DJW decoder finished {input.Length - bits.Position:N0} byte(s) before the section did; the patch is corrupt");
	}

	private static void DecodeOneTwo(ref BitReader bits, Canonical table, byte[] mtf, Span<byte> values, int skip, string where)
	{
		var elts = values.Length;
		var n = 0;
		long rep = 0;
		var pending = 0;
		var shift = 0;
		while (n < elts)
		{
			if (skip != 0 && n >= skip && values[n - skip] == 0)
			{
				values[n++] = 0;
				continue;
			}
			if (rep != 0)
			{
				values[n++] = mtf[0];
				rep--;
				continue;
			}
			if (pending != 0)
			{
				values[n++] = MoveToFront(mtf, pending);
				pending = 0;
				continue;
			}
			var symbol = table.Decode(ref bits, where);
			if (symbol <= Run1)
			{
				rep = (long)(symbol + 1) << shift;
				shift++;
				if (shift > 40 || rep > elts - n)
					throw PatchReader.Corrupt($"{where}: a DJW run of {rep:N0} repeats with {elts - n:N0} position(s) left; the patch is corrupt");
			}
			else
			{
				pending = symbol - 1;
				shift = 0;
				if (pending >= mtf.Length)
					throw PatchReader.Corrupt($"{where}: a DJW move-to-front index {pending} past a list of {mtf.Length}; the patch is corrupt");
			}
		}
		if (rep != 0)
			throw PatchReader.Corrupt($"{where}: a DJW run of repeats continues past the end of its table; the patch is corrupt");
	}

	private static byte MoveToFront(byte[] mtf, int index)
	{
		var symbol = mtf[index];
		for (var k = index; k != 0; k--)
			mtf[k] = mtf[k - 1];
		mtf[0] = symbol;
		return symbol;
	}

	private sealed class Canonical
	{
		private readonly int[] symbols;
		private readonly long[] basis;
		private readonly long[] limit;
		private readonly int minLength;
		private readonly int maxLength;

		public int Count { get; }

		public Canonical(ReadOnlySpan<byte> lengths, int maxAllowed)
		{
			var counts = new int[maxAllowed + 2];
			foreach (var l in lengths)
			{
				if (l > maxAllowed)
					throw PatchReader.Corrupt($"a DJW code length {l} is over the {maxAllowed} allowed; the patch is corrupt");
				counts[l]++;
			}
			var min = 1;
			while (min <= maxAllowed && counts[min] == 0)
				min++;
			var max = maxAllowed;
			while (max > 0 && counts[max] == 0)
				max--;
			minLength = min;
			maxLength = max;
			basis = new long[maxAllowed + 2];
			limit = new long[maxAllowed + 2];
			symbols = new int[lengths.Length];
			if (max == 0)
				return;

			var next = new long[maxAllowed + 2];
			next[min] = 0;
			limit[min] = counts[min] - 1;
			for (var i = min + 1; i <= max; i++)
			{
				var lastLimit = (limit[i - 1] + 1) << 1;
				next[i] = next[i - 1] + counts[i - 1];
				limit[i] = lastLimit + counts[i] - 1;
				basis[i] = lastLimit - next[i];
			}
			var count = 0;
			for (var s = 0; s < lengths.Length; s++)
			{
				var l = lengths[s];
				if (l == 0)
					continue;
				symbols[next[l]++] = s;
				count++;
			}
			Count = count;
		}

		public int Decode(ref BitReader bits, string where)
		{
			long code = 0;
			var length = 0;
			while (true)
			{
				if (length == maxLength)
					throw PatchReader.Corrupt($"{where}: a DJW code longer than any in its table; the patch is corrupt");
				length++;
				code = (code << 1) | (uint)bits.ReadBit();
				if (length >= minLength && code <= limit[length])
					break;
			}
			var offset = code - basis[length];
			if (basis[length] < 0 || offset < 0 || offset >= Count)
				throw PatchReader.Corrupt($"{where}: a DJW code that no symbol in its table has; the patch is corrupt");
			return symbols[offset];
		}
	}

	private ref struct BitReader
	{
		private readonly ReadOnlySpan<byte> input;
		private readonly string where;
		private int current;
		private int mask;

		public int Position { get; private set; }

		public BitReader(ReadOnlySpan<byte> input, string where)
		{
			this.input = input;
			this.where = where;
			mask = 0x100;
		}

		public int ReadBit()
		{
			if (mask == 0x100)
			{
				if (Position == input.Length)
					throw PatchReader.Corrupt($"{where}: the DJW bitstream ends before the section's declared size is reached; the patch is truncated or corrupt");
				current = input[Position++];
				mask = 1;
			}
			var bit = (current & mask) != 0 ? 1 : 0;
			mask <<= 1;
			return bit;
		}

		public int Read(int count)
		{
			var value = 0;
			for (var i = 0; i < count; i++)
				value = (value << 1) | ReadBit();
			return value;
		}
	}
}
