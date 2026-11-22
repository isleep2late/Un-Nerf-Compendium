using System.Buffers;

namespace Isleep2late.Files.Services.Patch;

internal sealed class SourceReader : IDisposable
{
	private const int WindowSize = 1 << 16;

	private readonly Stream stream;
	private readonly byte[] window;
	private long windowStart;
	private int windowLength;

	public long Length { get; }

	public SourceReader(Stream stream)
	{
		if (!stream.CanSeek || !stream.CanRead)
			throw new ArgumentException("the source ROM stream must be seekable and readable", nameof(stream));
		this.stream = stream;
		Length = stream.Length;
		window = ArrayPool<byte>.Shared.Rent(WindowSize);
		windowStart = 0;
		windowLength = 0;
	}

	public byte AtOrZero(long offset)
	{
		if (offset < 0 || offset >= Length)
			return 0;
		if (offset < windowStart || offset >= windowStart + windowLength)
			Slide(offset);
		return window[offset - windowStart];
	}

	private void Slide(long offset)
	{
		stream.Position = offset;
		var want = (int)Math.Min(window.Length, Length - offset);
		stream.ReadExactly(window, 0, want);
		windowStart = offset;
		windowLength = want;
	}

	public void ReadAt(long offset, Span<byte> into)
	{
		if (into.Length == 0)
			return;
		if (offset >= windowStart && offset + into.Length <= windowStart + windowLength)
		{
			window.AsSpan((int)(offset - windowStart), into.Length).CopyTo(into);
			return;
		}
		if (into.Length <= window.Length)
		{
			Slide(offset);
			window.AsSpan(0, into.Length).CopyTo(into);
			return;
		}
		stream.Position = offset;
		stream.ReadExactly(into);
	}

	public uint Crc32()
	{
		stream.Position = 0;
		return Checksums.Crc32(stream);
	}

	public void Dispose() => ArrayPool<byte>.Shared.Return(window);
}

internal sealed class TargetWriter : IDisposable
{
	private const int BufferSize = 1 << 16;

	private readonly Stream stream;
	private readonly byte[] buffer;
	private readonly long ceiling;
	private int used;
	private long flushed;
	private uint crc;

	public TargetWriter(Stream stream, long ceiling)
	{
		if (!stream.CanWrite)
			throw new ArgumentException("the target stream must be writable", nameof(stream));
		this.stream = stream;
		this.ceiling = ceiling;
		if (stream.CanSeek)
			stream.Position = 0;
		buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
	}

	public long Length => flushed + used;

	public uint Crc32 => crc;

	public void Write(ReadOnlySpan<byte> data)
	{
		if (data.Length > ceiling - Length)
			throw new PatchException(PatchFailure.Corrupt,
				$"the decoder was about to write byte {Length + data.Length:N0} of the output, past the {ceiling:N0} bytes a patch for this ROM could produce; the patch is corrupt or was made for a much larger file");
		crc = Checksums.Crc32Update(crc, data);
		if (data.Length >= buffer.Length)
		{
			Flush();
			stream.Write(data);
			flushed += data.Length;
			return;
		}
		if (used + data.Length > buffer.Length)
			Flush();
		data.CopyTo(buffer.AsSpan(used));
		used += data.Length;
	}

	public void Fill(byte value, long count)
	{
		Span<byte> run = stackalloc byte[256];
		run.Fill(value);
		while (count > 0)
		{
			var n = (int)Math.Min(run.Length, count);
			Write(run[..n]);
			count -= n;
		}
	}

	public void ReadBack(long offset, Span<byte> into)
	{
		if (offset < flushed)
		{
			var fromStream = (int)Math.Min(into.Length, flushed - offset);
			if (!stream.CanSeek || !stream.CanRead)
				throw new ArgumentException("this patch copies from its own output, which needs a seekable, readable target stream");
			stream.Flush();
			var end = stream.Position;
			stream.Position = offset;
			stream.ReadExactly(into[..fromStream]);
			stream.Position = end;
			into = into[fromStream..];
			offset += fromStream;
		}
		if (into.Length > 0)
			buffer.AsSpan((int)(offset - flushed), into.Length).CopyTo(into);
	}

	public void CopyFromOutput(long offset, long length)
	{
		var distance = Length - offset;
		var scratch = ArrayPool<byte>.Shared.Rent((int)Math.Min(BufferSize, Math.Max(distance, 1)));
		try
		{
			if (distance < scratch.Length)
			{
				var pattern = scratch.AsSpan(0, (int)distance);
				ReadBack(offset, pattern);
				while (length > 0)
				{
					var n = (int)Math.Min(pattern.Length, length);
					Write(pattern[..n]);
					length -= n;
				}
				return;
			}
			while (length > 0)
			{
				var n = (int)Math.Min(scratch.Length, length);
				ReadBack(offset, scratch.AsSpan(0, n));
				Write(scratch.AsSpan(0, n));
				offset += n;
				length -= n;
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(scratch);
		}
	}

	public void Flush()
	{
		if (used > 0)
		{
			stream.Write(buffer, 0, used);
			flushed += used;
			used = 0;
		}
	}

	public void Complete()
	{
		Flush();
		if (stream.CanSeek && stream.Length != Length)
			stream.SetLength(Length);
		stream.Flush();
	}

	public void Dispose()
	{
		Flush();
		stream.Flush();
		ArrayPool<byte>.Shared.Return(buffer);
	}
}

internal sealed class SourcePages : IDisposable
{
	private const int PageBits = 14;
	private const int PageSize = 1 << PageBits;
	private const int MaxSlots = 256;

	private readonly Stream stream;
	private readonly long length;
	private readonly byte[] pages;
	private readonly int slots;
	private readonly long[] pageInSlot;
	private readonly Dictionary<long, int> slotOfPage;
	private int nextVictim;

	public SourcePages(Stream stream, long length)
	{
		this.stream = stream;
		this.length = length;
		slots = (int)Math.Clamp((length + PageSize - 1) >> PageBits, 1, MaxSlots);
		pageInSlot = new long[slots];
		slotOfPage = new Dictionary<long, int>(slots);
		pages = ArrayPool<byte>.Shared.Rent(slots * PageSize);
		Array.Fill(pageInSlot, -1L);
	}

	public long HeldBytes => pages.Length;

	public long PagesRead { get; private set; }

	public void CopyTo(long offset, Span<byte> into)
	{
		while (into.Length > 0)
		{
			if (offset < 0 || offset >= length)
				throw new EndOfStreamException($"a patch asked for source byte {offset:N0} of a {length:N0}-byte source");
			var page = offset >> PageBits;
			var within = (int)(offset & (PageSize - 1));
			var slot = Slot(page);
			var valid = (int)Math.Min(PageSize, length - (page << PageBits));
			var n = Math.Min(into.Length, valid - within);
			pages.AsSpan(slot * PageSize + within, n).CopyTo(into);
			into = into[n..];
			offset += n;
		}
	}

	private int Slot(long page)
	{
		if (slotOfPage.TryGetValue(page, out var slot))
			return slot;
		slot = nextVictim;
		nextVictim = (nextVictim + 1) % slots;
		if (pageInSlot[slot] >= 0)
			slotOfPage.Remove(pageInSlot[slot]);
		var start = page << PageBits;
		var into = pages.AsSpan(slot * PageSize, (int)Math.Min(PageSize, length - start));
		if (stream is FileStream file)
		{
			var done = 0;
			while (done < into.Length)
			{
				var read = RandomAccess.Read(file.SafeFileHandle, into[done..], start + done);
				if (read <= 0)
					throw new EndOfStreamException($"the source ROM ended at byte {start + done:N0} while a patch was reading it");
				done += read;
			}
		}
		else
		{
			stream.Position = start;
			stream.ReadExactly(into);
		}
		pageInSlot[slot] = page;
		slotOfPage[page] = slot;
		PagesRead++;
		return slot;
	}

	public void Dispose() => ArrayPool<byte>.Shared.Return(pages);
}
