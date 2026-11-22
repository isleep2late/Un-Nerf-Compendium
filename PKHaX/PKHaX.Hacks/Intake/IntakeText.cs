using System.Globalization;
using PKHaX.Hacks.Profiles;

namespace PKHaX.Hacks.Intake;

public static class IntakeText
{
    public const string Title = "ROM hacks";
    public const string Target = "Drop a ROM hack or a patch here";
    public const string Stored = "Only the tables read from your file are kept, on this device. The ROM is not copied.";
    public const string IpsPatch = "An IPS patch does not say which game it is for. Choose the clean game it was made for.";
    public const string IpsNote = "IPS does not check its input";
    public const string StaleKit = "made by an older PKHaX: drop the ROM again";
    public const string NativeNeedsKit = "This is a ROM hack's own save format. Drop its ROM in ROM hacks first.";
    public const string NoShapeYet = "PKHaX read this hack's tables but cannot tell its save format from the ROM yet.";
    public const string CannotRead = "PKHaX cannot read this image on this device yet. In isleep2late Files, use Export for PKHaX on this game and drop the exported profile here.";
    public const string Unmapped = "This hack has species, moves or items PKHaX's stock editor cannot hold, so its save opens read-only.";
    public const string NotASave = "PKHaX did not recognise this file as a save.";
    public const string Question = SaveResolution.Question;
    public const string IpsNotARom = "The IPS patch was applied, but the result no longer reads as a ROM of the game you chose, so it is not used.";

    public static string Patch(string format, uint crc32, long size) =>
        string.Create(CultureInfo.InvariantCulture, $"This is a {format} patch. It needs the clean game it was made for (CRC32 {crc32:x8}, {size:N0} bytes). Choose that file.");

    public static string PatchNoCrc(string format) =>
        $"This is a {format} patch. It needs the clean game it was made for. Choose that file.";

    public static string Done(string name, int species, int moves, int items, int abilities, string shape) =>
        string.Create(CultureInfo.InvariantCulture, $"{name}: {species} species, {moves} moves, {items} items, {abilities} abilities read from your file. Save shape: {shape}. Now open this hack's save.");

    public static string WrongBase(string format, uint wantedCrc, long? wantedSize, uint foundCrc, long foundSize) =>
        string.Create(CultureInfo.InvariantCulture, $"The file you chose is not the game this {format} patch was made for: the patch wants CRC32 {wantedCrc:x8}{(wantedSize is { } w ? $" ({w:N0} bytes)" : "")}, and the file has CRC32 {foundCrc:x8} ({foundSize:N0} bytes).");

    public static string WrongBaseUnstated(string format, uint foundCrc, long foundSize, string why) =>
        string.Create(CultureInfo.InvariantCulture, $"The file you chose (CRC32 {foundCrc:x8}, {foundSize:N0} bytes) is not the game this {format} patch was made for: {why}");

    public static string WriteDiffers(int bytes) =>
        string.Create(CultureInfo.InvariantCulture, $"PKHaX cannot write this hack's save back unchanged ({bytes} bytes differ), so it opens read-only.");

    public static string Unknown(string? name) =>
        $"{(string.IsNullOrEmpty(name) ? "This file" : name)} is not a ROM, a patch, a profile or a save PKHaX knows: it looked for the patch headers PATCH, IPS32, UPS1, BPS1 and VCDIFF, a Game Boy Advance, Game Boy, DS or 3DS cartridge header, a pkhax-profile/1 profile or .is2lprofile bundle, and a save PKHeX or a hack container accepts.";

    public static string SignatureWanted(uint signature) =>
        string.Create(CultureInfo.InvariantCulture, $"PKHaX noted 0x{signature:X8} for this hack: add the hack's ROM again in ROM hacks, then open this save again.");

    public static string SignatureAbsent(uint signature) =>
        string.Create(CultureInfo.InvariantCulture, $"PKHaX already looked for 0x{signature:X8} in this hack's ROM and it is not there, so adding that ROM again will not open this save.");

    public static string SignatureFound(uint signature) =>
        string.Create(CultureInfo.InvariantCulture, $"The signature 0x{signature:X8} a save asked for was found in this ROM; open the save again.");

    public static string SignatureNotFound(uint signature, string why) =>
        string.Create(CultureInfo.InvariantCulture, $"The signature 0x{signature:X8} a save asked for was not taken from this ROM ({why}), so that save stays refused.");

    public static string TooLarge(long size, long cap) =>
        string.Create(CultureInfo.InvariantCulture, $"{CannotRead} (the image is {size:N0} bytes, above the {cap:N0} bytes this device reads.)");
}
