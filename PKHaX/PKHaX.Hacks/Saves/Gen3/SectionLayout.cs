using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;

namespace PKHaX.Hacks.Saves.Gen3;

public enum SaveBlock
{
    Small,
    Large,
    Storage,
}

public readonly record struct SectionSlot(int ToAdd, int Size);

public sealed class SectionLayout
{
    public const int SectionCount = 14;
    public const int SectorSize = 0x1000;
    public const int PayloadCapacity = 0xFF4;
    public const int VanillaStride = 0xF80;
    public const int CfruStride = 0xFF0;
    public const int TableBytes = SectionCount * 4;

    private SectionLayout(string name, SectionSlot[] slots)
    {
        Name = name;
        Slots = slots;
    }

    public string Name { get; }
    public IReadOnlyList<SectionSlot> Slots { get; }

    public static SaveBlock BlockOf(int id) => id switch
    {
        0 => SaveBlock.Small,
        >= 1 and <= 4 => SaveBlock.Large,
        >= 5 and < SectionCount => SaveBlock.Storage,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "A Gen 3 save section id is 0..13."),
    };

    public static IEnumerable<int> IdsOf(SaveBlock block) => block switch
    {
        SaveBlock.Small => [0],
        SaveBlock.Large => [1, 2, 3, 4],
        _ => Enumerable.Range(5, 9),
    };

    public int BlockSize(SaveBlock block) => IdsOf(block).Max(id => Slots[id].ToAdd + Slots[id].Size);

    public static readonly SectionLayout RubySapphire = FromBlockSizes("vanilla:RS", 0x890, 0x3AC0, 0x83D0, VanillaStride);
    public static readonly SectionLayout FireRedLeafGreen = FromBlockSizes("vanilla:FRLG", 0xF24, 0x3D68, 0x83D0, VanillaStride);
    public static readonly SectionLayout Emerald = FromBlockSizes("vanilla:E", 0xF2C, 0x3D88, 0x83D0, VanillaStride);
    public static readonly SectionLayout Cfru = FromBlockSizes("cfru", 0xF24, 0x3D68, 0x83D0, CfruStride);

    public static SectionLayout FromBlockSizes(string name, int smallSize, int largeSize, int storageSize, int stride)
    {
        var slots = new SectionSlot[SectionCount];
        slots[0] = new SectionSlot(0, smallSize);
        Fill(slots, 1, 4, largeSize, stride, name);
        Fill(slots, 5, 9, storageSize, stride, name);
        return Create(name, slots);
    }

    private static void Fill(SectionSlot[] slots, int first, int count, int size, int stride, string name)
    {
        for (var i = 0; i < count; i++)
            slots[first + i] = new SectionSlot(i * stride, i == count - 1 ? size - (i * stride) : stride);
        if (slots[first + count - 1].Size is <= 0 or > PayloadCapacity)
            throw new ArgumentException($"{name}: a block of 0x{size:X} bytes does not fill {count} sections of stride 0x{stride:X}.");
    }

    public static SectionLayout FromTable(string name, ReadOnlySpan<byte> table)
    {
        if (table.Length < TableBytes)
            throw new ArgumentException($"{name}: the section table needs {TableBytes} bytes, got {table.Length}.");
        var slots = new SectionSlot[SectionCount];
        for (var id = 0; id < SectionCount; id++)
            slots[id] = new SectionSlot(BinaryPrimitives.ReadUInt16LittleEndian(table[(id * 4)..]), BinaryPrimitives.ReadUInt16LittleEndian(table[((id * 4) + 2)..]));
        return Create(name, slots);
    }

    public static SectionLayout Create(string name, IReadOnlyList<SectionSlot> slots)
    {
        if (Problem(slots) is { } problem)
            throw new ArgumentException($"{name}: {problem}");
        return new SectionLayout(name, slots.ToArray());
    }

    public static string? Problem(IReadOnlyList<SectionSlot> slots)
    {
        if (slots.Count != SectionCount)
            return $"a Gen 3 layout has {SectionCount} sections, got {slots.Count}";
        for (var id = 0; id < SectionCount; id++)
        {
            var s = slots[id];
            if (s.Size is <= 0 or > PayloadCapacity || s.Size % 4 != 0)
                return $"section {id} has size 0x{s.Size:X}; it must be 4..0x{PayloadCapacity:X} and a multiple of 4";
            if (s.ToAdd < 0)
                return $"section {id} starts at a negative block offset";
        }
        foreach (var block in Enum.GetValues<SaveBlock>())
        {
            var ids = IdsOf(block).ToList();
            if (slots[ids[0]].ToAdd != 0)
                return $"the {block} block does not start at offset 0";
            for (var i = 1; i < ids.Count; i++)
            {
                var prev = slots[ids[i - 1]];
                if (slots[ids[i]].ToAdd != prev.ToAdd + prev.Size)
                    return $"section {ids[i]} starts at block offset 0x{slots[ids[i]].ToAdd:X}, not right after section {ids[i - 1]} (0x{prev.ToAdd + prev.Size:X})";
            }
        }
        return null;
    }
}
