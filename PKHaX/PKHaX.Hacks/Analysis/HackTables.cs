using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace PKHaX.Hacks.Analysis;

public static class HackTables
{
    public const string Gba = "gba";
    public const string Gb = "gb";
    public const string Nds = "nds";
    public const string Ctr = "3ds";
    public const string Nx = "nx";

    public const string Gb1Engine = "gb1-pret";
    public const string Gb2Engine = "gb2-pret";
    public const string NdsGen4Engine = "nds-gen4";
    public const string NdsGen5Engine = "nds-gen5";
    public const string CtrGen6Engine = "ctr-gen6";
    public const string CtrGen7Engine = "ctr-gen7";
    public const string LgpeEngine = "nx-lgpe";

    public const int GbMinLength = 0x8000;
    public const int GbMaxLength = 0x800000;

    public static IReadOnlyList<string> Platforms { get; } = [Gba, Gb, Nds, Ctr, Nx];

    public static string? Sniff(ReadOnlySpan<byte> image)
    {
        if (HackDataCache.IsGba(image))
            return Gba;
        if (LooksNitro(image))
            return Nds;
        if (image.Length >= 0x104 && (image.Slice(0x100, 4).SequenceEqual("NCSD"u8) || image.Slice(0x100, 4).SequenceEqual("NCCH"u8)))
            return Ctr;
        return image.Length is >= GbMinLength and <= GbMaxLength ? Gb : null;
    }

    private static bool LooksNitro(ReadOnlySpan<byte> s)
    {
        if (s.Length < NitroFs.HeaderSize)
            return false;
        foreach (var c in s.Slice(0x0C, 4))
        {
            if (c is not (>= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9'))
                return false;
        }
        var fntOffset = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(0x40));
        var fntSize = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(0x44));
        var fatOffset = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(0x48));
        var fatSize = BinaryPrimitives.ReadUInt32LittleEndian(s.Slice(0x4C));
        return fntOffset >= NitroFs.HeaderSize && fatOffset >= NitroFs.HeaderSize && fntSize >= 8 && fatSize >= 8 && fatSize % 8 == 0
            && (ulong)fntOffset + fntSize <= (ulong)s.Length && (ulong)fatOffset + fatSize <= (ulong)s.Length;
    }

    public static HackDataResult Read(ReadOnlyMemory<byte> image, string? sym = null, IdMapper? mapper = null)
    {
        switch (Sniff(image.Span))
        {
            case Gba:
                return GbaHackData.Read(image, mapper);
            case Nds:
                return NdsHackData.Read(image, Convert.ToHexStringLower(SHA1.HashData(image.Span)), mapper);
            case Ctr:
                return HackDataResult.Refused("a 3DS image is read from its RomFS files, not from the image bytes; open it through the 3DS reader.");
            default:
                return GbHackData.Read(image, sym, mapper);
        }
    }

    public static HackData Finish(HackData data, string platform, string? gameCode, string? engine, IReadOnlyList<HackTableStatus>? tables = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        var stated = tables ?? data.Tables;
        var rows = new List<HackTableStatus>(HackTableStatus.Kinds.Count);
        foreach (var kind in HackTableStatus.Kinds)
            rows.Add(stated.FirstOrDefault(t => t.Table == kind) ?? Default(data, kind));
        var finished = data with { Platform = platform, GameCode = gameCode, Engine = engine, Tables = rows };
        return finished with { TableKey = HackTableKey.Of(finished) };
    }

    private static HackTableStatus Default(HackData data, string kind) => kind switch
    {
        HackTableStatus.Species => data.Species.Any(s => s.Id != 0 && s.BaseStats.Count == 6)
            ? new HackTableStatus(kind, HackTableStatus.Read, null, true)
            : new HackTableStatus(kind, data.Species.Any(s => s.Name.Length > 0) ? HackTableStatus.Partial : HackTableStatus.Missing, "no base stats were read for any species"),
        HackTableStatus.Moves => data.Moves.Any(m => m.Name.Length > 0)
            ? data.Moves.Any(m => m.Power != 0 || m.Pp != 0) ? new HackTableStatus(kind, HackTableStatus.Read) : new HackTableStatus(kind, HackTableStatus.Partial, "move names only; power, type, accuracy and PP are not read")
            : new HackTableStatus(kind, HackTableStatus.Missing, "no move names were read"),
        HackTableStatus.Items => HackTableStatus.Of(kind, data.Items.Any(i => i.Name.Length > 0), "no item names were read"),
        HackTableStatus.Abilities => HackTableStatus.Of(kind, data.Abilities.Any(a => a.Name.Length > 0), "no ability names were read"),
        _ => HackTableStatus.Of(kind, data.LevelUp.Count > 0, "no level-up learnsets were read"),
    };

    public static string StatusLine(HackData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return string.Join(",", HackTableStatus.Kinds.Select(k => $"{k}:{data.StatusOf(k)}"));
    }
}
