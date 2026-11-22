using System.Collections.Generic;

namespace PKHaX.Hacks.Analysis;

public sealed record GfRomHeader
{
    public const int At = 0x100;
    public const int Size = 0x104;

    public static IReadOnlyList<int> Shifts { get; } = [0, -4, 4];

    public int Shift { get; init; }
    public int EarlyShift { get; init; }
    public bool EarlyPlaceAmbiguous { get; init; }
    public uint Version { get; init; }
    public uint Language { get; init; }
    public int? SpeciesNames { get; init; }
    public int? MoveNames { get; init; }
    public uint SaveBlock2Size { get; init; }
    public uint SaveBlock1Size { get; init; }
    public uint PartyCountOffset { get; init; }
    public uint PartyOffset { get; init; }
    public uint TrainerIdOffset { get; init; }
    public uint PlayerNameOffset { get; init; }
    public uint FlagsOffset { get; init; }
    public uint VarsOffset { get; init; }
    public uint PokedexOffset { get; init; }
    public uint PokedexCount { get; init; }
    public uint FrontierStatusOffset { get; init; }
    public int? SpeciesInfo { get; init; }
    public int? AbilityNames { get; init; }
    public int? Items { get; init; }
    public int? Moves { get; init; }
    public IReadOnlyList<byte> BagCounts { get; init; } = [];
    public byte PcItemsCount { get; init; }
    public uint PcItemsOffset { get; init; }
    public uint EnigmaBerryOffset { get; init; }
    public uint EnigmaBerrySize { get; init; }

    public bool Shifted => Shift != 0;

    public bool SaneSizes =>
        SaveBlock2Size is > 0x100 and < 0x2000
        && SaveBlock1Size is > 0x1000 and < 0x8000
        && PartyOffset < SaveBlock1Size && PartyCountOffset < SaveBlock1Size;

    public bool SaneTables => SpeciesInfo is not null && Items is not null && Moves is not null;

    public bool NamesInline => SpeciesNames is null || MoveNames is null || AbilityNames is null;

    public string? MissingTable =>
        SpeciesInfo is null ? "species-info pointer (+0xBC)"
        : Items is null ? "item pointer (+0xC8)"
        : Moves is null ? "move pointer (+0xCC)"
        : null;

    public string? Problem =>
        !SaneSizes ? $"the block sizes do not read as a save layout (SaveBlock2 0x{SaveBlock2Size:X}, SaveBlock1 0x{SaveBlock1Size:X}, party count +0x{PartyCountOffset:X}, party +0x{PartyOffset:X})"
        : MissingTable is { } table ? $"the {table} is not a ROM pointer"
        : null;

    public static GfRomHeader? Read(GbaImage rom) => Read(rom, Shifts, out _);

    public static GfRomHeader? Read(GbaImage rom, out string? why) => Read(rom, Shifts, out why);

    public static GfRomHeader? Read(GbaImage rom, IReadOnlyList<int> shifts, out string? why)
    {
        var reasons = new List<string>();
        foreach (var shift in shifts)
        {
            if (ReadAt(rom, shift) is not { } header)
            {
                reasons.Add($"shift {shift}: the image is too short for a GF header");
                continue;
            }
            if (header.Problem is { } problem)
            {
                reasons.Add($"shift {shift}: {problem}");
                continue;
            }
            why = null;
            return header;
        }
        why = $"no GF header at 0x{At:X} reads as sane ({string.Join("; ", reasons)})";
        return null;
    }

    public static IReadOnlyList<GfRomHeader> ReadAll(GbaImage rom) => ReadAll(rom, Shifts);

    public static IReadOnlyList<GfRomHeader> ReadAll(GbaImage rom, IReadOnlyList<int> shifts)
    {
        var found = new List<GfRomHeader>();
        foreach (var shift in shifts)
        {
            if (ReadAt(rom, shift) is { SaneSizes: true } header)
                found.Add(header);
        }
        return found;
    }

    public static GfRomHeader? ReadAt(GbaImage rom, int shift)
    {
        var b = At;
        if (!rom.Has(b, Size + shift))
            return null;
        var (early, ambiguous) = EarlyPlace(rom, shift);
        int Head(int field) => b + early + field;
        int Tail(int field) => b + shift + field;
        return new GfRomHeader
        {
            Shift = shift,
            EarlyShift = early,
            EarlyPlaceAmbiguous = ambiguous,
            Version = rom.U32(b + 0x00),
            Language = rom.U32(b + 0x04),
            SpeciesNames = rom.PointerAt(Head(0x44)),
            MoveNames = rom.PointerAt(Head(0x48)),
            FlagsOffset = rom.U32(Head(0x50)),
            VarsOffset = rom.U32(Head(0x54)),
            PokedexOffset = rom.U32(Head(0x58)),
            PokedexCount = rom.U32(Head(0x70)),
            SaveBlock2Size = rom.U32(Tail(0x88)),
            SaveBlock1Size = rom.U32(Tail(0x8C)),
            PartyCountOffset = rom.U32(Tail(0x90)),
            PartyOffset = rom.U32(Tail(0x94)),
            TrainerIdOffset = rom.U32(Tail(0x9C)),
            PlayerNameOffset = rom.U32(Tail(0xA0)),
            FrontierStatusOffset = rom.U32(Tail(0xA8)),
            SpeciesInfo = rom.PointerAt(Tail(0xBC)),
            AbilityNames = rom.PointerAt(Tail(0xC0)),
            Items = rom.PointerAt(Tail(0xC8)),
            Moves = rom.PointerAt(Tail(0xCC)),
            BagCounts = [rom.U8(Tail(0xE4)), rom.U8(Tail(0xE5)), rom.U8(Tail(0xE6)), rom.U8(Tail(0xE7)), rom.U8(Tail(0xE8))],
            PcItemsCount = rom.U8(Tail(0xE9)),
            PcItemsOffset = rom.U32(Tail(0xEC)),
            EnigmaBerryOffset = rom.U32(Tail(0xF4)),
            EnigmaBerrySize = rom.U32(Tail(0xF8)),
        };
    }

    private static (int Place, bool Ambiguous) EarlyPlace(GbaImage rom, int shift)
    {
        if (shift >= 0)
            return (0, false);
        var plain = NamePair(rom, 0);
        var moved = NamePair(rom, shift);
        if (plain != 0 && moved != 0)
        {
            var plainEdge = NameEdge(rom, 0);
            var movedEdge = NameEdge(rom, shift);
            if (plainEdge != movedEdge)
                return (movedEdge ? shift : 0, false);
        }
        return (moved > plain ? shift : 0, moved == plain);
    }

    private static int NamePair(GbaImage rom, int place)
    {
        var species = rom.PointerAt(At + place + 0x44) is not null;
        var moves = rom.PointerAt(At + place + 0x48) is not null;
        return species && moves ? 2 : species || moves ? 0 : 1;
    }

    private static bool NameEdge(GbaImage rom, int place)
    {
        var after = rom.U32(At + place + 0x4C);
        return (after == 0 || rom.Offset(after) is not null) && rom.Offset(rom.U32(At + place + 0x50)) is null;
    }
}
