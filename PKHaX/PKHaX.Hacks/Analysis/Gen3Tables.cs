using System;
using System.Collections.Generic;
using System.Text;
using PKHeX.Core;

namespace PKHaX.Hacks.Analysis;

public static class Gen3Tables
{
    public const int SpeciesNameStride = 11;
    public const int MoveNameStride = 13;
    public const int AbilityNameStride = 13;
    public const int ItemNameLength = 14;
    public const int MaxEntries = 2048;

    public static readonly int[] SpeciesInfoStrides = [28, 26];
    public static readonly int[] MoveStrides = [12, 9];
    public static readonly int[] ItemStrides = [44];

    public static bool IsNameByte(byte c) => c is 0x00 or >= 0xA1 and <= 0xEE or 0x1B or 0xF0 or >= GlyphFirst and <= GlyphLast;

    private const byte GlyphFirst = 0x53;
    private const byte GlyphLast = 0x59;
    private static readonly string[] Glyphs = ["PK", "MN", "PO", "Ké", "BL", "OC", "K"];

    public static string? Name(GbaImage rom, int offset, int stride)
    {
        if (!rom.Has(offset, stride))
            return null;
        var slot = rom.Slice(offset, stride);
        var end = slot.IndexOf((byte)0xFF);
        if (end <= 0)
            return null;
        foreach (var c in slot[..end])
        {
            if (!IsNameByte(c))
                return null;
        }
        var text = Decode(slot[..end]);
        return text.Trim().Length == 0 ? null : text;
    }

    public static string? FullName(GbaImage rom, int offset, int length)
    {
        if (!rom.Has(offset, length))
            return null;
        var slot = rom.Slice(offset, length);
        if (slot[0] is 0x00 or 0xFF || slot.IndexOf((byte)0xFF) >= 0)
            return null;
        foreach (var c in slot)
        {
            if (!IsNameByte(c))
                return null;
        }
        var text = Decode(slot);
        return text.Trim().Length == 0 ? null : text;
    }

    private static string Decode(ReadOnlySpan<byte> body)
    {
        var sb = new StringBuilder(body.Length + 4);
        var run = new List<byte>(body.Length + 1);
        foreach (var c in body)
        {
            if (c is < GlyphFirst or > GlyphLast)
            {
                run.Add(c);
                continue;
            }
            Flush();
            sb.Append(Glyphs[c - GlyphFirst]);
        }
        Flush();
        return sb.ToString();

        void Flush()
        {
            if (run.Count == 0)
                return;
            run.Add(0xFF);
            sb.Append(StringConverter3.GetString(run.ToArray(), (int)LanguageID.English));
            run.Clear();
        }
    }

    public static int Bound(int start, int stride, IEnumerable<int?> otherTables)
    {
        var next = int.MaxValue;
        foreach (var t in otherTables)
        {
            if (t is { } o && o > start && o < next)
                next = o;
        }
        return next == int.MaxValue ? MaxEntries : Math.Min(MaxEntries, (next - start) / stride);
    }

    public static List<string> Names(GbaImage rom, int start, int stride, int? count = null, int max = MaxEntries)
    {
        var names = new List<string>();
        var limit = Math.Min(count ?? MaxEntries, max);
        for (var i = 0; i < limit; i++)
        {
            var name = Name(rom, start + i * stride, stride);
            if (name is null)
            {
                if (count is not null)
                {
                    names.Add("");
                    continue;
                }
                if (i == 0)
                {
                    names.Add("");
                    continue;
                }
                break;
            }
            names.Add(name);
        }
        return names;
    }

    public static int? SpeciesInfoStride(GbaImage rom, int table)
    {
        foreach (var stride in SpeciesInfoStrides)
        {
            var b = table + stride;
            if (rom.Has(b, 6) && rom.U8(b) == 45 && rom.U8(b + 1) == 49 && rom.U8(b + 2) == 49 && rom.U8(b + 3) == 45 && rom.U8(b + 4) == 65 && rom.U8(b + 5) == 65)
                return stride;
        }
        return null;
    }

    public static int? MoveStride(GbaImage rom, int table)
    {
        foreach (var stride in MoveStrides)
        {
            var b = table + stride;
            if (rom.Has(b, 5) && rom.U8(b + 1) == 40 && rom.U8(b + 2) == 0 && rom.U8(b + 3) == 100 && rom.U8(b + 4) == 35)
                return stride;
        }
        return null;
    }

    public static int? ItemStride(GbaImage rom, int table)
    {
        foreach (var stride in ItemStrides)
        {
            if (Name(rom, table + stride, ItemNameLength) is { } name && name.Equals("MASTER BALL", StringComparison.OrdinalIgnoreCase))
                return stride;
        }
        return null;
    }
}
