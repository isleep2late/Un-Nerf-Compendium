using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Saves.Gen3;

public sealed class ChecksumPolicy
{
    private ChecksumPolicy(string name, int[] windows)
    {
        Name = name;
        Windows = windows;
    }

    public string Name { get; }
    public IReadOnlyList<int> Windows { get; }

    public int Window(int id) => Windows[id];

    public static ChecksumPolicy FromLayout(SectionLayout layout) =>
        new($"{layout.Name} section sizes", layout.Slots.Select(s => s.Size).ToArray());

    public static ChecksumPolicy Create(string name, IReadOnlyList<int> windows)
    {
        if (windows.Count != SectionLayout.SectionCount)
            throw new ArgumentException($"{name}: a policy names {SectionLayout.SectionCount} windows, got {windows.Count}.");
        for (var id = 0; id < windows.Count; id++)
        {
            if (windows[id] is <= 0 or > SectionLayout.PayloadCapacity || windows[id] % 4 != 0)
                throw new ArgumentException($"{name}: section {id} window 0x{windows[id]:X} must be 4..0x{SectionLayout.PayloadCapacity:X} and a multiple of 4.");
        }
        return new ChecksumPolicy(name, windows.ToArray());
    }

    public static ChecksumPolicy Uniform(string name, int window, IReadOnlyDictionary<int, int>? presets = null) =>
        Create(name, Enumerable.Range(0, SectionLayout.SectionCount).Select(id => presets is not null && presets.TryGetValue(id, out var w) ? w : window).ToArray());

    public ushort Compute(int id, ReadOnlySpan<byte> sector) => Fold(sector[..Window(id)]);

    public static ushort Fold(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (var i = 0; i + 4 <= data.Length; i += 4)
            sum += BinaryPrimitives.ReadUInt32LittleEndian(data[i..]);
        return (ushort)((sum >> 16) + (sum & 0xFFFF));
    }
}
