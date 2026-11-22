using Isleep2late.Files.Services.Patch;
using PKHaX.Hacks.Intake;

namespace Isleep2late.Files.Services.Pkhax;

public sealed class PkhaxRomPatcher : IRomPatcher
{
	public static PkhaxRomPatcher Shared { get; } = new();

	public static PatchKind KindOf(PatchFormat format) => format switch
	{
		PatchFormat.Ips => PatchKind.Ips,
		PatchFormat.Ips32 => PatchKind.Ips32,
		PatchFormat.Ups => PatchKind.Ups,
		PatchFormat.Bps => PatchKind.Bps,
		PatchFormat.Vcdiff => PatchKind.Vcdiff,
		_ => PatchKind.Unknown,
	};

	public PatchProbe Inspect(ReadOnlySpan<byte> patch)
	{
		PatchInfo info;
		try
		{
			info = RomPatch.Inspect(patch);
		}
		catch (PatchException e)
		{
			throw new RomPatchRefusedException(e.Message, e.Kind == PatchFailure.WrongSource);
		}
		catch (Exception e) when (Unreadable(e))
		{
			throw new RomPatchRefusedException(e.Message, false);
		}
		return new PatchProbe(KindOf(info.Format), info.SourceSize, info.SourceCrc32, info.TargetSize, info.TargetCrc32, info.Warnings);
	}

	public byte[] Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch)
	{
		try
		{
			return RomPatch.Apply(source, patch);
		}
		catch (PatchException e)
		{
			throw new RomPatchRefusedException(e.Message, e.Kind is PatchFailure.WrongSource or PatchFailure.AlreadyPatched);
		}
		catch (Exception e) when (Unreadable(e))
		{
			throw new RomPatchRefusedException(e.Message, false);
		}
	}

	private static bool Unreadable(Exception e) => e is System.IO.IOException or System.IO.InvalidDataException or ArgumentException or OverflowException or IndexOutOfRangeException;
}
