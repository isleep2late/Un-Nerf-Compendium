using System;
using System.Buffers.Binary;
using System.Text.Json;
using PKHaX.Hacks.Analysis;
using PKHeX.Core;

namespace PKHaX.Hacks.Intake;

public enum DropKind
{
    Patch,
    GbaRom,
    GbRom,
    NdsRom,
    CtrImage,
    Profile,
    Bundle,
    Save,
    Unknown,
}

public sealed record DropVerdict(DropKind Kind, string? Platform, string Detail, string? Sentence)
{
    public bool IsRom => Kind is DropKind.GbaRom or DropKind.GbRom or DropKind.NdsRom or DropKind.CtrImage;
}

public static class DropClassifier
{
    public const int HeadBytes = 0x4000;
    public const long WholeFileBelow = 4L * 1024 * 1024;
    public const string GbaHeaderSource = "GBATEK cartridge header: fixed 0x96 at 0xB2, complement check at 0xBD over 0xA0-0xBC";
    public const string GbHeaderSource = "Pan Docs cartridge header: header checksum at 0x14D over 0x134-0x14C, ROM size code at 0x148";
    public const string NdsHeaderSource = "GBATEK DS cartridge header: CRC-16 (0xFFFF, poly 0xA001) of 0x000-0x15D at 0x15E";

    private static ReadOnlySpan<byte> VcdiffMagic => [0xD6, 0xC3, 0xC4];
    private static ReadOnlySpan<byte> ZipMagic => [0x50, 0x4B, 0x03, 0x04];
    private static ReadOnlySpan<byte> Utf8Bom => [0xEF, 0xBB, 0xBF];

    public static DropVerdict Classify(ReadOnlySpan<byte> bytes, long length, string? fileName)
    {
        if (PatchMagic(bytes) is { } patch)
            return new DropVerdict(DropKind.Patch, null, $"{PatchProbe.NameOf(patch)} patch header at offset 0", null);
        if (bytes.Length >= 4 && bytes[..4].SequenceEqual(ZipMagic))
            return new DropVerdict(DropKind.Bundle, null, "a zip archive (read as an .is2lprofile bundle if it holds pkhax-profile.json)", null);
        if (LooksLikeProfile(bytes) || (bytes.Length == length && ParsesAsProfile(bytes)))
            return new DropVerdict(DropKind.Profile, null, $"JSON naming {Profiles.PkhaxProfile.Schema}", null);
        var sniffed = bytes.Length == length ? HackTables.Sniff(bytes) : null;
        if (sniffed == HackTables.Ctr || (bytes.Length >= 0x104 && (bytes.Slice(0x100, 4).SequenceEqual("NCSD"u8) || bytes.Slice(0x100, 4).SequenceEqual("NCCH"u8))))
            return new DropVerdict(DropKind.CtrImage, HackTables.Ctr, "NCSD or NCCH at 0x100", null);
        if (NdsHeader(bytes) && (bytes.Length != length || sniffed == HackTables.Nds))
            return new DropVerdict(DropKind.NdsRom, HackTables.Nds, NdsHeaderSource, null);
        if (GbaHeader(bytes) && (bytes.Length != length || sniffed == HackTables.Gba))
            return new DropVerdict(DropKind.GbaRom, HackTables.Gba, GbaHeaderSource, null);
        if (GbHeader(bytes, length) && (bytes.Length != length || sniffed == HackTables.Gb))
            return new DropVerdict(DropKind.GbRom, HackTables.Gb, GbHeaderSource, null);
        if (bytes.Length == length && length <= WholeFileBelow && SaveContainer(bytes) is { } container)
            return new DropVerdict(DropKind.Save, null, container, null);
        return new DropVerdict(DropKind.Unknown, null, "nothing matched", IntakeText.Unknown(fileName));
    }

    public static PatchKind? PatchMagic(ReadOnlySpan<byte> head)
    {
        if (head.StartsWith("IPS32"u8))
            return PatchKind.Ips32;
        if (head.StartsWith("PATCH"u8))
            return PatchKind.Ips;
        if (head.StartsWith("UPS1"u8))
            return PatchKind.Ups;
        if (head.StartsWith("BPS1"u8))
            return PatchKind.Bps;
        if (head.StartsWith(VcdiffMagic))
            return PatchKind.Vcdiff;
        return null;
    }

    private static int JsonStart(ReadOnlySpan<byte> head)
    {
        var at = 0;
        if (head.StartsWith(Utf8Bom))
            at = 3;
        while (at < head.Length && head[at] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            at++;
        return at < head.Length && head[at] == (byte)'{' ? at : -1;
    }

    private static bool LooksLikeProfile(ReadOnlySpan<byte> head) =>
        JsonStart(head) >= 0 && head[..Math.Min(head.Length, HeadBytes)].IndexOf("\"pkhax-profile/1\""u8) >= 0;

    private static bool ParsesAsProfile(ReadOnlySpan<byte> file)
    {
        if (file.Length > Profiles.ProfileStore.MaxProfileBytes)
            return false;
        var at = JsonStart(file);
        if (at < 0)
            return false;
        try
        {
            var reader = new Utf8JsonReader(file[at..], new JsonReaderOptions { MaxDepth = 256 });
            using var doc = JsonDocument.ParseValue(ref reader);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("schema", out var schema)
                && schema.ValueKind == JsonValueKind.String
                && schema.ValueEquals(Profiles.PkhaxProfile.Schema);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool GbaHeader(ReadOnlySpan<byte> head)
    {
        if (head.Length < 0xC0 || head[0xB2] != 0x96)
            return false;
        var sum = 0;
        for (var i = 0xA0; i <= 0xBC; i++)
            sum -= head[i];
        return (byte)(sum - 0x19) == head[0xBD];
    }

    public static bool GbHeader(ReadOnlySpan<byte> head, long length)
    {
        if (head.Length < 0x150 || head[0x148] > 8)
            return false;
        byte sum = 0;
        for (var i = 0x134; i <= 0x14C; i++)
            sum = (byte)(sum - head[i] - 1);
        return sum == head[0x14D] && length == (0x8000L << head[0x148]);
    }

    public static bool NdsHeader(ReadOnlySpan<byte> head)
    {
        if (head.Length < 0x160)
            return false;
        foreach (var c in head.Slice(0x0C, 4))
        {
            if (c is not (>= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9'))
                return false;
        }
        return Crc16(head[..0x15E]) == BinaryPrimitives.ReadUInt16LittleEndian(head[0x15E..]);
    }

    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }
        return crc;
    }

    public static string? SaveContainer(ReadOnlySpan<byte> file)
    {
        if (HackOpen.CfruContainer(file.ToArray()) is { } cfru)
            return $"a CFRU hack save ({cfru})";
        if (SaveUtil.TryGetSaveFile(file.ToArray(), out var sav))
            return $"a save PKHeX reads as {sav.GetType().Name}";
        return null;
    }
}
