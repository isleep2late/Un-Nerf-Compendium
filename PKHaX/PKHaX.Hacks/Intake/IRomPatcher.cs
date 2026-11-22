using System;
using System.Collections.Generic;

namespace PKHaX.Hacks.Intake;

public enum PatchKind
{
    Unknown,
    Ips,
    Ips32,
    Ups,
    Bps,
    Vcdiff,
}

public sealed record PatchProbe(PatchKind Format, long? SourceSize, uint? SourceCrc32, long? TargetSize, uint? TargetCrc32, IReadOnlyList<string> Warnings)
{
    public bool Known => Format != PatchKind.Unknown;

    public bool StatesSource => Format is PatchKind.Ups or PatchKind.Bps or PatchKind.Vcdiff;

    public bool StatesSourceCrc => SourceCrc32 is not null;

    public string Name => NameOf(Format);

    public static string NameOf(PatchKind format) => format switch
    {
        PatchKind.Ips => "IPS",
        PatchKind.Ips32 => "IPS32",
        PatchKind.Ups => "UPS",
        PatchKind.Bps => "BPS",
        PatchKind.Vcdiff => "xdelta",
        _ => "unknown",
    };
}

public sealed class RomPatchRefusedException : Exception
{
    public RomPatchRefusedException(string message, bool wrongSource) : base(message)
    {
        WrongSource = wrongSource;
    }

    public bool WrongSource { get; }
}

public interface IRomPatcher
{
    PatchProbe Inspect(ReadOnlySpan<byte> patch);

    byte[] Apply(ReadOnlySpan<byte> source, ReadOnlySpan<byte> patch);
}
