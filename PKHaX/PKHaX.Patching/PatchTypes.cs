namespace Isleep2late.Files.Services.Patch;

public enum PatchFormat
{
	Unknown,
	Ips,
	Ips32,
	Ups,
	Bps,
	Vcdiff,
}

public enum PatchFailure
{
	WrongSource,
	Corrupt,
	Unsupported,
	AlreadyPatched,
}

public sealed class PatchException : Exception
{
	public PatchFailure Kind { get; }

	public bool CauseUnknown { get; init; }

	public PatchException(PatchFailure kind, string message) : base(message)
	{
		Kind = kind;
	}
}

public sealed record PatchInfo(
	PatchFormat Format,
	long? SourceSize,
	long? TargetSize,
	uint? SourceCrc32,
	uint? TargetCrc32,
	uint? PatchCrc32,
	string? AppHeader,
	int? Windows,
	int? SecondaryCompressorId,
	long? MaxWriteEnd,
	IReadOnlyList<string> Warnings)
{
	public bool CustomCodeTable { get; init; }

	public string? SourceBlake3 { get; init; }

	public string? TargetBlake3 { get; init; }

	public bool CanApply => Format != PatchFormat.Unknown && !CustomCodeTable
		&& (SecondaryCompressorId is null || SecondaryCompression.IsKnown(SecondaryCompressorId.Value));
}

public sealed record PatchResult(
	long BytesWritten,
	bool? SourceChecksumOk,
	bool? TargetChecksumOk,
	bool? PatchChecksumOk,
	int WindowsVerified)
{
	public long PeakBufferBytes { get; init; }

	public string? Unchecked { get; init; }
}
