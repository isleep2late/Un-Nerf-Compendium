using System;

namespace PKHaX.Hacks.Analysis;

public sealed record RhhRomHeader(int Offset, int Major, int Minor, int Patch, int Flags, int Moves, int Species, int Abilities, int? AbilitiesTable, int Items, int ItemNameLength)
{
    public const int SearchLimit = 0x10000;
    public const int Size = 0x18;

    public string Version => $"{Major}.{Minor}.{Patch}";

    public static RhhRomHeader? Read(GbaImage rom)
    {
        var span = rom.Span[..Math.Min(rom.Length, SearchLimit)];
        var at = span.IndexOf("RHHEXP"u8);
        if (at < 0 || !rom.Has(at, Size))
            return null;
        return new RhhRomHeader(at, rom.U8(at + 0x06), rom.U8(at + 0x07), rom.U8(at + 0x08), rom.U8(at + 0x09),
            rom.U16(at + 0x0A), rom.U16(at + 0x0C), rom.U16(at + 0x0E), rom.PointerAt(at + 0x10), rom.U16(at + 0x14), rom.U8(at + 0x16));
    }
}
