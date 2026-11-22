using System.Buffers;

namespace Isleep2late.Files.Services.Patch;

internal static class IpsPatch
{
	private static ReadOnlySpan<byte> IpsMagic => "PATCH"u8;
	private static ReadOnlySpan<byte> Ips32Magic => "IPS32"u8;
	private static ReadOnlySpan<byte> Eof => "EOF"u8;
	private static ReadOnlySpan<byte> Eeof => "EEOF"u8;

	internal readonly record struct Record(long Offset, int Length, bool Rle, byte Value, int DataStart)
	{
		public long End => Offset + Length;
	}

	internal sealed class Parsed
	{
		public required bool Is32 { get; init; }
		public required List<Record> Records { get; init; }
		public long? Truncate { get; init; }
		public long MaxWriteEnd { get; init; }
	}

	public static bool IsIps32(ReadOnlySpan<byte> patch) => patch.Length >= Ips32Magic.Length && patch[..Ips32Magic.Length].SequenceEqual(Ips32Magic);

	public static Parsed Parse(ReadOnlySpan<byte> patch)
	{
		var is32 = IsIps32(patch);
		var reader = new PatchReader(patch, is32 ? Ips32Magic.Length : IpsMagic.Length);
		if (!is32 && !reader.StartsWith(IpsMagic))
			throw PatchReader.Corrupt("not an IPS patch: the file does not start with PATCH");

		var records = new List<Record>();
		long? truncate = null;
		long maxEnd = 0;
		while (true)
		{
			if (is32)
			{
				if (reader.Remaining < 4)
					throw PatchReader.Corrupt($"the IPS32 patch ran out at byte {reader.Position:N0} without an EEOF terminator; the file is truncated");
				if (reader.Remaining == 4 && reader.Peek(4).SequenceEqual(Eeof))
					break;
			}
			else
			{
				if (reader.Remaining < 3)
					throw PatchReader.Corrupt($"the IPS patch ran out at byte {reader.Position:N0} without an EOF terminator; the file is truncated");
				if ((reader.Remaining == 3 || reader.Remaining == 6) && reader.Peek(3).SequenceEqual(Eof))
				{
					reader.Position += 3;
					if (reader.Remaining == 3)
						truncate = reader.ReadU24BE();
					break;
				}
			}

			long offset = is32 ? reader.ReadU32BE() : reader.ReadU24BE();
			int size = reader.ReadU16BE();
			Record record;
			if (size == 0)
			{
				int run = reader.ReadU16BE();
				var value = reader.ReadByte();
				record = new Record(offset, run, true, value, 0);
			}
			else
			{
				var start = reader.Position;
				reader.ReadBytes(size);
				record = new Record(offset, size, false, 0, start);
			}
			records.Add(record);
			if (record.End > maxEnd)
				maxEnd = record.End;
		}

		return new Parsed { Is32 = is32, Records = records, Truncate = truncate, MaxWriteEnd = maxEnd };
	}

	public static PatchInfo Inspect(ReadOnlySpan<byte> patch)
	{
		var parsed = Parse(patch);
		var warnings = new List<string>();
		if (parsed.Is32)
			warnings.Add("IPS32 files are Switch exefs patches meant for Eden's load/<TID>/<Mod>/exefs/ folder, not patches for a ROM file; the emulator applies them at boot from there.");
		else
			warnings.Add("IPS carries no checksums and no sizes: nothing in the file can tell whether it is being applied to the right ROM.");
		if (parsed.Truncate is { } t)
			warnings.Add($"the patch truncates the output to {t:N0} bytes after writing.");
		return new PatchInfo(
			parsed.Is32 ? PatchFormat.Ips32 : PatchFormat.Ips,
			SourceSize: null,
			TargetSize: parsed.Truncate,
			SourceCrc32: null, TargetCrc32: null, PatchCrc32: null,
			AppHeader: null,
			Windows: null,
			SecondaryCompressorId: null,
			MaxWriteEnd: parsed.MaxWriteEnd,
			Warnings: warnings);
	}

	public static PatchResult Apply(ReadOnlySpan<byte> patch, Stream source, Stream target, long ceiling, IProgress<double>? progress, CancellationToken ct)
	{
		var parsed = Parse(patch);
		if (!target.CanSeek || !target.CanWrite)
			throw new ArgumentException("an IPS patch overwrites bytes in place, which needs a seekable, writable target stream", nameof(target));

		var sourceLength = source.CanSeek ? source.Length : 0;
		var produced = Math.Max(parsed.MaxWriteEnd, sourceLength);
		if (produced > ceiling)
			throw RomPatch.OverCeiling("applying this patch would produce", produced, ceiling, sourceLength);

		source.Position = 0;
		target.Position = 0;
		source.CopyTo(target, 1 << 20);
		var length = target.Position;

		var zeros = ArrayPool<byte>.Shared.Rent(1 << 16);
		var run = ArrayPool<byte>.Shared.Rent(1 << 16);
		try
		{
			Array.Clear(zeros);
			for (var i = 0; i < parsed.Records.Count; i++)
			{
				if ((i & 0xFF) == 0)
				{
					ct.ThrowIfCancellationRequested();
					progress?.Report((double)i / parsed.Records.Count);
				}
				var record = parsed.Records[i];
				if (record.Offset > length)
				{
					target.Position = length;
					var gap = record.Offset - length;
					while (gap > 0)
					{
						var n = (int)Math.Min(zeros.Length, gap);
						target.Write(zeros, 0, n);
						gap -= n;
					}
				}
				target.Position = record.Offset;
				if (record.Rle)
				{
					run.AsSpan(0, Math.Min(run.Length, record.Length)).Fill(record.Value);
					var left = record.Length;
					while (left > 0)
					{
						var n = Math.Min(run.Length, left);
						target.Write(run, 0, n);
						left -= n;
					}
				}
				else
				{
					target.Write(patch.Slice(record.DataStart, record.Length));
				}
				if (record.End > length)
					length = record.End;
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(zeros);
			ArrayPool<byte>.Shared.Return(run);
		}

		if (parsed.Truncate is { } truncate && truncate < length)
			length = truncate;
		if (target.Length != length)
			target.SetLength(length);
		target.Position = length;
		target.Flush();
		progress?.Report(1);
		return new PatchResult(length, null, null, null, 0);
	}

	public static IReadOnlyList<(long Offset, byte[] Bytes)> Records(ReadOnlySpan<byte> patch)
	{
		var parsed = Parse(patch);
		var list = new List<(long, byte[])>(parsed.Records.Count);
		foreach (var record in parsed.Records)
		{
			var bytes = new byte[record.Length];
			if (record.Rle)
				bytes.AsSpan().Fill(record.Value);
			else
				patch.Slice(record.DataStart, record.Length).CopyTo(bytes);
			list.Add((record.Offset, bytes));
		}
		return list;
	}

	public static byte[] Merge(IEnumerable<byte[]> ipsPatches)
	{
		const int pageShift = 12;
		const int pageSize = 1 << pageShift;
		var pages = new SortedDictionary<long, (byte[] Data, bool[] Written)>();

		var index = 0;
		foreach (var patch in ipsPatches)
		{
			index++;
			if (IsIps32(patch))
				throw new PatchException(PatchFailure.Unsupported, $"patch {index} is IPS32 (a Switch exefs patch), which cannot be merged into an IPS");
			var parsed = Parse(patch);
			if (parsed.Truncate is { } t)
				throw new PatchException(PatchFailure.Unsupported, $"patch {index} truncates the output to {t:N0} bytes, which has no meaning in a merged patch");
			foreach (var record in parsed.Records)
			{
				for (var k = 0; k < record.Length; k++)
				{
					var offset = record.Offset + k;
					var value = record.Rle ? record.Value : patch[record.DataStart + k];
					var page = offset >> pageShift;
					if (!pages.TryGetValue(page, out var entry))
					{
						entry = (new byte[pageSize], new bool[pageSize]);
						pages[page] = entry;
					}
					var slot = (int)(offset & (pageSize - 1));
					if (entry.Written[slot] && entry.Data[slot] != value)
						throw new PatchException(PatchFailure.Unsupported,
							$"patch {index} writes 0x{value:X2} at 0x{offset:X} where an earlier patch wrote 0x{entry.Data[slot]:X2}; these patches conflict and cannot be merged");
					entry.Data[slot] = value;
					entry.Written[slot] = true;
				}
			}
		}

		var output = new MemoryStream();
		output.Write(IpsMagic);
		var run = new List<byte>();
		long runStart = -1;

		void FlushRun()
		{
			var at = 0;
			while (at < run.Count)
			{
				var n = Math.Min(0xFFFF, run.Count - at);
				var offset = runStart + at;
				if (offset > 0xFFFFFF)
					throw new PatchException(PatchFailure.Unsupported, $"a merged record at 0x{offset:X} is past the 16 MiB an IPS offset can address");
				output.WriteByte((byte)(offset >> 16));
				output.WriteByte((byte)(offset >> 8));
				output.WriteByte((byte)offset);
				output.WriteByte((byte)(n >> 8));
				output.WriteByte((byte)n);
				output.Write(CollectionsMarshalSpan(run, at, n));
				at += n;
			}
			run.Clear();
			runStart = -1;
		}

		foreach (var (page, entry) in pages)
		{
			for (var slot = 0; slot < pageSize; slot++)
			{
				var offset = (page << pageShift) + slot;
				if (entry.Written[slot])
				{
					if (runStart >= 0 && runStart + run.Count != offset)
						FlushRun();
					if (runStart < 0)
						runStart = offset;
					run.Add(entry.Data[slot]);
				}
				else if (runStart >= 0)
				{
					FlushRun();
				}
			}
		}
		if (runStart >= 0)
			FlushRun();

		output.Write(Eof);
		return output.ToArray();
	}

	private static ReadOnlySpan<byte> CollectionsMarshalSpan(List<byte> list, int start, int count) =>
		System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list).Slice(start, count);
}
