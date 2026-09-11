using System;
using System.IO;

namespace PKHeX.Core;

/// <summary>
/// PKHaX: "corrupts" a Generation 1 (Red/Blue/Yellow, international or Japanese layout) save for the
/// Any% 2-swap route: the party count byte is set to 0xFF (party underflow) together with the species-list
/// terminator right after it, and the main-data checksum is recomputed so the game still loads the file on CONTINUE.
/// </summary>
/// <remarks>
/// The corruption is applied to the raw file bytes exactly as they are on disk / as they were opened
/// (<see cref="ApplyInPlace"/>, <see cref="CorruptFileOnDisk"/>), never to a re-serialized <see cref="SAV1"/>:
/// <see cref="SaveFile.Write"/> re-packs the party and box lists and rewrites the checksum (measured: it changes
/// bytes of an unedited real save and is not even idempotent), so its output is not byte-identical to the file.
/// Only the three bytes named above change; in particular the Trainer ID is left exactly as it was.
/// Unsaved editor changes are therefore NOT included - the front-ends say so before asking for confirmation.
/// </remarks>
public static class Gen1SaveCorrupter // PKHaX: Gen 1 save corrupter (Any% 2-swap)
{
    /// <summary>Value written to the party count byte (and the species-list terminator right after it).</summary>
    public const byte CorruptedPartyCount = 0xFF;

    /// <summary>Only raw 32 KiB Game Boy SRAM images are accepted.</summary>
    public const int ExpectedSize = SaveUtil.SIZE_G1RAW;

    /// <summary>Offsets matching the save's layout (international or Japanese).</summary>
    public static SAV1Offsets GetOffsets(SAV1 sav) => sav.Japanese ? SAV1Offsets.JPN : SAV1Offsets.INT;

    /// <summary>
    /// Applies the corruption to a raw Gen 1 save image: party count 0xFF, terminator 0xFF, checksum recomputed.
    /// Every other byte is left untouched. Byte-identical to the standalone corrupt-save-gen1.py on the INT layout.
    /// </summary>
    /// <exception cref="ArgumentException">If <paramref name="raw"/> is not exactly 0x8000 bytes.</exception>
    public static void ApplyInPlace(Span<byte> raw, SAV1Offsets offsets)
    {
        if (raw.Length != ExpectedSize)
            throw new ArgumentException($"Expected a raw {ExpectedSize} byte Gen 1 save, got {raw.Length} bytes.", nameof(raw));
        raw[offsets.Party] = CorruptedPartyCount;
        raw[offsets.Party + 1] = CorruptedPartyCount; // species list terminator
        raw[offsets.ChecksumOfs] = SAV1.GetRBYChecksum(raw, offsets.OT, offsets.ChecksumOfs);
    }

    /// <summary>Trainer ID (big-endian, as stored) of a raw Gen 1 save image.</summary>
    public static ushort GetTID16(ReadOnlySpan<byte> raw, SAV1Offsets offsets) => (ushort)((raw[offsets.TID16] << 8) | raw[offsets.TID16 + 1]);

    /// <summary>
    /// Corrupts the file at <paramref name="path"/> exactly as it is on disk (what the desktop Tools menu item does):
    /// reads the file, refuses anything that is not a raw 0x8000 byte image or whose Trainer ID does not match
    /// <paramref name="expectedTID16"/>, writes the unmodified bytes to <c>&lt;path&gt;.bak-yyyyMMdd-HHmmss</c>
    /// (never overwriting an existing file; a failure here means nothing is corrupted), applies
    /// <see cref="ApplyInPlace"/> to the bytes read and writes them back over the file.
    /// </summary>
    /// <param name="path">Raw Gen 1 save file to corrupt.</param>
    /// <param name="offsets">Layout of the save (<see cref="GetOffsets"/> of the loaded <see cref="SAV1"/>).</param>
    /// <param name="expectedTID16">Trainer ID the caller believes the file has (the loaded save's <see cref="ITrainerID16.TID16"/>) -
    /// a sanity check that the file on disk is the file in the editor; null skips the check.</param>
    /// <param name="now">Timestamp for the backup name; defaults to <see cref="DateTime.Now"/>.</param>
    /// <returns>Path of the backup that was written.</returns>
    /// <exception cref="InvalidDataException">If the file is not 0x8000 bytes or its Trainer ID does not match.</exception>
    /// <exception cref="IOException">If the backup cannot be written (nothing has been modified) or the corrupted file cannot be written (the backup exists).</exception>
    public static string CorruptFileOnDisk(string path, SAV1Offsets offsets, ushort? expectedTID16 = null, DateTime? now = null)
    {
        var raw = File.ReadAllBytes(path);
        if (raw.Length != ExpectedSize)
            throw new InvalidDataException($"{path} is {raw.Length} bytes; only a raw {ExpectedSize} byte Gen 1 save can be corrupted.");
        if (expectedTID16 is { } tid && GetTID16(raw, offsets) != tid)
            throw new InvalidDataException($"The file on disk has Trainer ID {GetTID16(raw, offsets)} but the loaded save has {tid}; it is not the same save. Nothing was changed.");

        var backup = GetBackupName(path, now ?? DateTime.Now);
        using (var fs = new FileStream(backup, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            fs.Write(raw, 0, raw.Length);

        var corrupted = (byte[])raw.Clone();
        ApplyInPlace(corrupted, offsets);
        File.WriteAllBytes(path, corrupted);
        return backup;
    }

    /// <summary>True if the stored main-data checksum matches the data.</summary>
    public static bool IsChecksumValid(ReadOnlySpan<byte> raw, SAV1Offsets offsets)
        => raw.Length == ExpectedSize && raw[offsets.ChecksumOfs] == SAV1.GetRBYChecksum(raw, offsets.OT, offsets.ChecksumOfs);

    /// <summary>True if <paramref name="raw"/> already carries the corruption (count 0xFF, terminator 0xFF, checksum valid).</summary>
    public static bool IsApplied(ReadOnlySpan<byte> raw, SAV1Offsets offsets)
    {
        if (raw.Length != ExpectedSize)
            return false;
        if (raw[offsets.Party] != CorruptedPartyCount || raw[offsets.Party + 1] != CorruptedPartyCount)
            return false;
        return IsChecksumValid(raw, offsets);
    }

    /// <summary>Backup file name used by every front-end: &lt;file&gt;.bak-yyyyMMdd-HHmmss (same as the standalone tool).</summary>
    public static string GetBackupName(string fileName, DateTime now) => $"{fileName}.bak-{now:yyyyMMdd-HHmmss}";

    /// <summary>Plain-words warning shown by every front-end when the editor has unsaved changes.</summary>
    public const string UnsavedEditsWarning =
        "You have UNSAVED EDITS. They are NOT included: the file is corrupted exactly as it is on disk right now. " +
        "Cancel and save/export the file first if you want those edits in the corrupted save.";

    /// <summary>The in-game steps after the corrupted save has been written, shown by every front-end.</summary>
    public const string RouteReminder =
        "2-swap reminder: CONTINUE, then START > POKEMON. Never move the cursor past index 54. " +
        "Swap slot 7 with 21, then 20 with 22, then mash B.";
}
