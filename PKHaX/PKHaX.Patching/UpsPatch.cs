using System.Buffers;

namespace Isleep2late.Files.Services.Patch;

internal static class UpsPatch
{
	private static ReadOnlySpan<byte> Magic => "UPS1"u8;

	private const int FooterLength = 12;

	private readonly record struct Header(long SourceSize, long TargetSize, int BodyStart, uint SourceCrc, uint TargetCrc, uint PatchCrc);

	private static Header ReadHeader(ReadOnlySpan<byte> patch, long ceiling = long.MaxValue, long sourceLength = 0)
	{
		var reader = new PatchReader(patch);
		if (!reader.StartsWith(Magic))
			throw PatchReader.Corrupt("not a UPS patch: the file does not start with UPS1");
		if (patch.Length < Magic.Length + 2 + FooterLength)
			throw PatchReader.Corrupt($"a UPS patch is at least {Magic.Length + 2 + FooterLength} bytes; this file is {patch.Length}");
		reader.Position = Magic.Length;
		var sourceSize = reader.ReadByuuVarint();
		var targetSize = reader.ReadByuuVarint();
		if (targetSize > ceiling)
			throw RomPatch.OverCeiling("the patch says its result is", targetSize, ceiling, sourceLength);
		var bodyStart = reader.Position;
		var footer = new PatchReader(patch, patch.Length - FooterLength);
		var sourceCrc = footer.ReadU32LE();
		var targetCrc = footer.ReadU32LE();
		var patchCrc = footer.ReadU32LE();
		if (bodyStart > patch.Length - FooterLength)
			throw PatchReader.Corrupt("the UPS header runs into its own footer; the file is truncated");
		return new Header(sourceSize, targetSize, bodyStart, sourceCrc, targetCrc, patchCrc);
	}

	public static PatchInfo Inspect(ReadOnlySpan<byte> patch)
	{
		var h = ReadHeader(patch);
		var warnings = new List<string>();
		if (Checksums.Crc32(patch[..^4]) != h.PatchCrc)
			warnings.Add("the patch's own CRC-32 does not match its footer: the file is corrupt and will be refused.");
		return new PatchInfo(PatchFormat.Ups, h.SourceSize, h.TargetSize, h.SourceCrc, h.TargetCrc, h.PatchCrc,
			AppHeader: null, Windows: null, SecondaryCompressorId: null, MaxWriteEnd: null, Warnings: warnings);
	}

	public static PatchResult Apply(ReadOnlySpan<byte> patch, Stream sourceStream, Stream targetStream, long ceiling, IProgress<double>? progress, CancellationToken ct)
	{
		var h = ReadHeader(patch, ceiling, sourceStream.CanSeek ? sourceStream.Length : 0);

		if (Checksums.Crc32(patch[..^4]) != h.PatchCrc)
			throw new PatchException(PatchFailure.Corrupt, $"the patch's own CRC-32 is {Checksums.Crc32(patch[..^4]):X8} but its footer says {h.PatchCrc:X8}: the patch file is corrupt");

		using var source = new SourceReader(sourceStream);
		var sourceCrc = source.Crc32();
		if (sourceCrc != h.SourceCrc)
		{
			if (sourceCrc == h.TargetCrc)
				throw new PatchException(PatchFailure.AlreadyPatched, $"this ROM already has the patch applied: its CRC-32 {sourceCrc:X8} is the patch's target CRC-32");
			throw new PatchException(PatchFailure.WrongSource,
				$"wrong source ROM: the patch was made for a {h.SourceSize:N0}-byte file with CRC-32 {h.SourceCrc:X8}; this file is {source.Length:N0} bytes with CRC-32 {sourceCrc:X8}");
		}

		using var target = new TargetWriter(targetStream, ceiling);
		var reader = new PatchReader(patch, h.BodyStart);
		var bodyEnd = patch.Length - FooterLength;
		long position = 0;
		var chunk = ArrayPool<byte>.Shared.Rent(1 << 16);
		try
		{
			var blocks = 0;
			while (reader.Position < bodyEnd)
			{
				if ((++blocks & 0x3FF) == 0)
				{
					ct.ThrowIfCancellationRequested();
					progress?.Report((double)reader.Position / patch.Length);
				}
				var skip = reader.ReadByuuVarint();
				CopyThrough(source, target, ref position, skip, h.TargetSize, chunk);

				while (reader.Position < bodyEnd)
				{
					var x = reader.ReadByte();
					if (x == 0)
					{
						CopyThrough(source, target, ref position, 1, h.TargetSize, chunk);
						break;
					}
					if (position < h.TargetSize)
						target.Write([(byte)(source.AtOrZero(position) ^ x)]);
					position++;
				}
			}
			if (position < h.TargetSize)
				CopyThrough(source, target, ref position, h.TargetSize - position, h.TargetSize, chunk);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(chunk);
		}

		target.Complete();
		if (target.Length != h.TargetSize)
			throw new PatchException(PatchFailure.Corrupt, $"the decoder produced {target.Length:N0} bytes where the patch promised {h.TargetSize:N0}");
		if (target.Crc32 != h.TargetCrc)
			throw new PatchException(PatchFailure.Corrupt, $"the output's CRC-32 is {target.Crc32:X8} but the patch says the result should be {h.TargetCrc:X8}: the patch is corrupt in a way its own checksum did not catch");
		progress?.Report(1);
		return new PatchResult(target.Length, true, true, true, 0);
	}

	private static void CopyThrough(SourceReader source, TargetWriter target, ref long position, long count, long targetSize, byte[] chunk)
	{
		var writable = Math.Max(0, Math.Min(count, targetSize - position));
		var fromSource = Math.Max(0, Math.Min(writable, source.Length - position));
		var offset = position;
		var left = fromSource;
		while (left > 0)
		{
			var n = (int)Math.Min(chunk.Length, left);
			source.ReadAt(offset, chunk.AsSpan(0, n));
			target.Write(chunk.AsSpan(0, n));
			offset += n;
			left -= n;
		}
		if (writable > fromSource)
			target.Fill(0, writable - fromSource);
		position += count;
	}
}
