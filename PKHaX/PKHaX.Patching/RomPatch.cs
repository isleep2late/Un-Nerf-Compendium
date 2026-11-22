namespace Isleep2late.Files.Services.Patch;

public static class RomPatch
{
	private static ReadOnlySpan<byte> VcdiffMagic => [0xD6, 0xC3, 0xC4, 0x00];

	public const long TargetFloor = 64L << 20;

	public const long TargetCap = 8L << 30;

	public static long MaxTargetFor(long sourceLength)
	{
		if (sourceLength <= 0)
			return TargetFloor;
		if (sourceLength >= (TargetCap - TargetFloor) / 2)
			return TargetCap;
		return sourceLength * 2 + TargetFloor;
	}

	internal static PatchException OverCeiling(string claim, long size, long ceiling, long sourceLength) =>
		new(PatchFailure.Corrupt,
			$"{claim} {size:N0} bytes, more than the {ceiling:N0} bytes a patch for this {sourceLength:N0}-byte ROM could produce; the patch is corrupt or was made for a much larger file");

	public static PatchFormat Detect(ReadOnlySpan<byte> head)
	{
		if (Starts(head, "PATCH"u8)) return PatchFormat.Ips;
		if (Starts(head, "IPS32"u8)) return PatchFormat.Ips32;
		if (Starts(head, "UPS1"u8)) return PatchFormat.Ups;
		if (Starts(head, "BPS1"u8)) return PatchFormat.Bps;
		if (Starts(head, VcdiffMagic)) return PatchFormat.Vcdiff;
		return PatchFormat.Unknown;
	}

	private static bool Starts(ReadOnlySpan<byte> head, ReadOnlySpan<byte> magic) =>
		head.Length >= magic.Length && head[..magic.Length].SequenceEqual(magic);

	public static PatchInfo Inspect(ReadOnlySpan<byte> patch) => Detect(patch) switch
	{
		PatchFormat.Ips or PatchFormat.Ips32 => IpsPatch.Inspect(patch),
		PatchFormat.Ups => UpsPatch.Inspect(patch),
		PatchFormat.Bps => BpsPatch.Inspect(patch),
		PatchFormat.Vcdiff => VcdiffPatch.Inspect(patch),
		_ => new PatchInfo(PatchFormat.Unknown, null, null, null, null, null, null, null, null, null,
			["not a patch this decoder recognises: the file does not start with PATCH, IPS32, UPS1, BPS1 or the VCDIFF magic."]),
	};

	public static PatchResult Apply(ReadOnlySpan<byte> patch, Stream source, Stream target, IProgress<double>? progress = null, CancellationToken ct = default, long? maxTarget = null)
	{
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(target);
		if (maxTarget is <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxTarget), maxTarget, "the output ceiling must be positive");

		var ceiling = MaxTargetFor(source.CanSeek ? source.Length : 0);
		if (maxTarget is { } cap && cap < ceiling)
			ceiling = cap;

		return Detect(patch) switch
		{
			PatchFormat.Ips or PatchFormat.Ips32 => IpsPatch.Apply(patch, source, target, ceiling, progress, ct),
			PatchFormat.Ups => UpsPatch.Apply(patch, source, target, ceiling, progress, ct),
			PatchFormat.Bps => BpsPatch.Apply(patch, source, target, ceiling, progress, ct),
			PatchFormat.Vcdiff => VcdiffPatch.Apply(patch, source, target, ceiling, progress, ct),
			_ => throw new PatchException(PatchFailure.Unsupported, "not a patch this decoder recognises: the file does not start with PATCH, IPS32, UPS1, BPS1 or the VCDIFF magic"),
		};
	}

	public static byte[] Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch)
	{
		using var sourceStream = new MemoryStream(source.ToArray(), writable: false);
		using var targetStream = new MemoryStream();
		Apply(patch, sourceStream, targetStream);
		return targetStream.ToArray();
	}

	public static byte[] MergeIps(IEnumerable<byte[]> ipsPatches)
	{
		ArgumentNullException.ThrowIfNull(ipsPatches);
		return IpsPatch.Merge(ipsPatches);
	}

	public static IReadOnlyList<(long Offset, byte[] Bytes)> IpsRecords(ReadOnlySpan<byte> ips) => IpsPatch.Records(ips);
}
