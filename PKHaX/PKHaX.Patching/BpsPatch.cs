using System.Buffers;
using System.Text;

namespace Isleep2late.Files.Services.Patch;

internal static class BpsPatch
{
	private static ReadOnlySpan<byte> Magic => "BPS1"u8;

	private const int FooterLength = 12;

	private readonly record struct Header(long SourceSize, long TargetSize, string Metadata, int BodyStart, uint SourceCrc, uint TargetCrc, uint PatchCrc);

	private static Header ReadHeader(ReadOnlySpan<byte> patch, long ceiling = long.MaxValue, long sourceLength = 0)
	{
		var reader = new PatchReader(patch);
		if (!reader.StartsWith(Magic))
			throw PatchReader.Corrupt("not a BPS patch: the file does not start with BPS1");
		if (patch.Length < Magic.Length + 3 + FooterLength)
			throw PatchReader.Corrupt($"a BPS patch is at least {Magic.Length + 3 + FooterLength} bytes; this file is {patch.Length}");
		reader.Position = Magic.Length;
		var sourceSize = reader.ReadByuuVarint();
		var targetSize = reader.ReadByuuVarint();
		if (targetSize > ceiling)
			throw RomPatch.OverCeiling("the patch says its result is", targetSize, ceiling, sourceLength);
		var metadataSize = reader.ReadByuuVarint();
		if (metadataSize > reader.Remaining - FooterLength)
			throw PatchReader.Corrupt($"the BPS metadata claims {metadataSize:N0} bytes but only {Math.Max(0, reader.Remaining - FooterLength):N0} are there; the file is truncated");
		var metadata = Encoding.UTF8.GetString(reader.ReadBytes(metadataSize));
		var bodyStart = reader.Position;
		var footer = new PatchReader(patch, patch.Length - FooterLength);
		var sourceCrc = footer.ReadU32LE();
		var targetCrc = footer.ReadU32LE();
		var patchCrc = footer.ReadU32LE();
		return new Header(sourceSize, targetSize, metadata, bodyStart, sourceCrc, targetCrc, patchCrc);
	}

	public static PatchInfo Inspect(ReadOnlySpan<byte> patch)
	{
		var h = ReadHeader(patch);
		var warnings = new List<string>();
		if (Checksums.Crc32(patch[..^4]) != h.PatchCrc)
			warnings.Add("the patch's own CRC-32 does not match its footer: the file is corrupt and will be refused.");
		return new PatchInfo(PatchFormat.Bps, h.SourceSize, h.TargetSize, h.SourceCrc, h.TargetCrc, h.PatchCrc,
			AppHeader: h.Metadata.Length > 0 ? h.Metadata : null,
			Windows: null, SecondaryCompressorId: null, MaxWriteEnd: null, Warnings: warnings);
	}

	public static PatchResult Apply(ReadOnlySpan<byte> patch, Stream sourceStream, Stream targetStream, long ceiling, IProgress<double>? progress, CancellationToken ct)
	{
		var h = ReadHeader(patch, ceiling, sourceStream.CanSeek ? sourceStream.Length : 0);

		var actualPatchCrc = Checksums.Crc32(patch[..^4]);
		if (actualPatchCrc != h.PatchCrc)
			throw new PatchException(PatchFailure.Corrupt, $"the patch's own CRC-32 is {actualPatchCrc:X8} but its footer says {h.PatchCrc:X8}: the patch file is corrupt");

		using var source = new SourceReader(sourceStream);
		var sourceCrc = source.Crc32();
		if (sourceCrc != h.SourceCrc || source.Length != h.SourceSize)
		{
			if (sourceCrc == h.TargetCrc && source.Length == h.TargetSize)
				throw new PatchException(PatchFailure.AlreadyPatched, $"this ROM already has the patch applied: its CRC-32 {sourceCrc:X8} is the patch's target CRC-32");
			throw new PatchException(PatchFailure.WrongSource,
				$"wrong source ROM: the patch was made for a {h.SourceSize:N0}-byte file with CRC-32 {h.SourceCrc:X8}; this file is {source.Length:N0} bytes with CRC-32 {sourceCrc:X8}");
		}

		using var target = new TargetWriter(targetStream, ceiling);
		Decode(patch, h, source, target, progress, ct);
		if (target.Length != h.TargetSize)
			throw new PatchException(PatchFailure.Corrupt, $"the decoder produced {target.Length:N0} bytes where the patch promised {h.TargetSize:N0}; the patch is corrupt");
		if (target.Crc32 != h.TargetCrc)
			throw new PatchException(PatchFailure.Corrupt, $"the output's CRC-32 is {target.Crc32:X8} but the patch says the result should be {h.TargetCrc:X8}: the patch is corrupt in a way its own checksum did not catch");
		progress?.Report(1);
		return new PatchResult(target.Length, true, true, true, 0);
	}

	internal static bool ProofDecodeChecksTarget { get; set; }

	internal static (byte[] Bytes, uint Crc32) DecodeUnchecked(ReadOnlySpan<byte> patch, Stream sourceStream, CancellationToken ct = default)
	{
		var ceiling = RomPatch.MaxTargetFor(sourceStream.CanSeek ? sourceStream.Length : 0);
		var h = ReadHeader(patch, ceiling, sourceStream.CanSeek ? sourceStream.Length : 0);
		var actualPatchCrc = Checksums.Crc32(patch[..^4]);
		if (actualPatchCrc != h.PatchCrc)
			throw new PatchException(PatchFailure.Corrupt, $"the patch's own CRC-32 is {actualPatchCrc:X8} but its footer says {h.PatchCrc:X8}: the patch file is corrupt");
		using var source = new SourceReader(sourceStream);
		using var output = new MemoryStream((int)Math.Min(h.TargetSize, 1 << 28));
		uint crc;
		using (var target = new TargetWriter(output, ceiling))
		{
			Decode(patch, h, source, target, null, ct);
			if (ProofDecodeChecksTarget && (target.Length != h.TargetSize || target.Crc32 != h.TargetCrc))
				throw new PatchException(PatchFailure.Corrupt, $"the output's CRC-32 is {target.Crc32:X8} but the patch says the result should be {h.TargetCrc:X8}");
			crc = target.Crc32;
		}
		return (output.ToArray(), crc);
	}

	private static void Decode(ReadOnlySpan<byte> patch, Header h, SourceReader source, TargetWriter target, IProgress<double>? progress, CancellationToken ct)
	{
		var reader = new PatchReader(patch, h.BodyStart);
		var bodyEnd = patch.Length - FooterLength;
		long output = 0;
		long sourceRelative = 0;
		long targetRelative = 0;
		var chunk = ArrayPool<byte>.Shared.Rent(1 << 16);
		try
		{
			var actions = 0;
			while (reader.Position < bodyEnd)
			{
				if ((++actions & 0x3FF) == 0)
				{
					ct.ThrowIfCancellationRequested();
					progress?.Report((double)reader.Position / patch.Length);
				}
				var at = reader.Position;
				var data = reader.ReadByuuVarint();
				var command = (int)(data & 3);
				var length = (data >> 2) + 1;
				if (output + length > h.TargetSize)
					throw PatchReader.Corrupt($"the action at patch offset 0x{at:X} writes past the {h.TargetSize:N0}-byte target; the patch is corrupt");

				switch (command)
				{
					case 0:
						if (output + length > source.Length)
							throw PatchReader.Corrupt($"SourceRead at patch offset 0x{at:X} reads past the end of the source; the patch is corrupt");
						CopyFromSource(source, target, output, length, chunk);
						break;
					case 1:
						target.Write(reader.ReadBytes(length));
						break;
					case 2:
					{
						var d = reader.ReadByuuVarint();
						sourceRelative += (d & 1) != 0 ? -(d >> 1) : d >> 1;
						if (sourceRelative < 0 || sourceRelative + length > source.Length)
							throw PatchReader.Corrupt($"SourceCopy at patch offset 0x{at:X} points at 0x{sourceRelative:X}, outside the source; the patch is corrupt");
						CopyFromSource(source, target, sourceRelative, length, chunk);
						sourceRelative += length;
						break;
					}
					default:
					{
						var d = reader.ReadByuuVarint();
						targetRelative += (d & 1) != 0 ? -(d >> 1) : d >> 1;
						if (targetRelative < 0 || targetRelative >= output)
							throw PatchReader.Corrupt($"TargetCopy at patch offset 0x{at:X} points at 0x{targetRelative:X}, which has not been written yet; the patch is corrupt");
						target.CopyFromOutput(targetRelative, length);
						targetRelative += length;
						break;
					}
				}
				output += length;
			}
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(chunk);
		}

		target.Complete();
	}

	private static void CopyFromSource(SourceReader source, TargetWriter target, long offset, long length, byte[] chunk)
	{
		while (length > 0)
		{
			var n = (int)Math.Min(chunk.Length, length);
			source.ReadAt(offset, chunk.AsSpan(0, n));
			target.Write(chunk.AsSpan(0, n));
			offset += n;
			length -= n;
		}
	}
}
