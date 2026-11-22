using System;
using System.Diagnostics.CodeAnalysis;
using PKHeX.Core;

namespace PKHaX.Hacks.Readers;

public interface IHackSaveVariantReader
{
    string VariantId { get; }

    bool IsRecognized(long dataLength);

    bool IsSelfIdentifying(ReadOnlySpan<byte> data);

    bool TryRead(Memory<byte> data, HackSaveContext? context, [NotNullWhen(true)] out SaveFile? result);
}
