using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PKHeX.Core;

/// PKHaX: emulator save-state container access - locates raw console memory and embedded cart saves.
public enum SaveStateKind
{
    MgbaPng,
    MgbaRaw,
    Bess,
    VbaM,
    DeSmuME,
    MelonDS,
    CitraCst,
    RawMemory,
}

public enum StateConsole
{
    GB,
    GBA,
    NDS,
    N3DS,
}

public sealed class SaveStateFile
{
    public const uint MgbaMagicGB = 0x00400000;
    public const uint MgbaMagicGBA = 0x01000000;
    public const int MgbaStateSizeGB = 0x11800;
    public const int MgbaStateSizeGBA = 0x61000;
    public const int MgbaWramGB = 0x04400;
    public const int MgbaIwramGBA = 0x19000;
    public const int MgbaEwramGBA = 0x21000;

    public SaveStateKind Kind { get; }
    public StateConsole Console { get; }
    public byte[] FileData { get; }
    public byte[] State { get; }
    public string FilePath { get; set; } = string.Empty;

    public int WramOffset { get; private set; } = -1;
    public int WramSize { get; private set; }
    public int EwramOffset { get; private set; } = -1;
    public int IwramOffset { get; private set; } = -1;
    public int MainRamOffset { get; internal set; } = -1;
    public int MainRamSize { get; internal set; }

    public byte[]? EmbeddedSave { get; private set; }
    private int _embeddedSaveOffset = -1;
    private int _mgbaSaveChunkIndex = -1;

    private readonly List<(byte[] Type, byte[] Payload)> _pngChunks = [];
    private readonly List<(uint Tag, int Offset, int Size)> _rawExtdata = [];

    private SaveStateFile(SaveStateKind kind, StateConsole console, byte[] file, byte[] state)
    {
        Kind = kind;
        Console = console;
        FileData = file;
        State = state;
    }

    public bool HasScannableMemory => WramOffset >= 0 || EwramOffset >= 0 || MainRamOffset >= 0 || Kind == SaveStateKind.RawMemory;

    /// Flat offset in State for a GB CPU address in 0xC000-0xDFFF (bank 0 at C000, bank 1 at D000).
    public int GetGBOffset(int address)
    {
        if (WramOffset < 0 || address is < 0xC000 or > 0xDFFF)
            return -1;
        int rel = address - 0xC000;
        if (rel >= WramSize)
            return -1;
        return WramOffset + rel;
    }

    public int GetGBAOffset(uint address)
    {
        if (address is >= 0x02000000 and < 0x02040000 && EwramOffset >= 0)
            return EwramOffset + (int)(address - 0x02000000);
        if (address is >= 0x03000000 and < 0x03008000 && IwramOffset >= 0)
            return IwramOffset + (int)(address - 0x03000000);
        return -1;
    }

    public static bool TryParse(ReadOnlySpan<byte> input, string path, out SaveStateFile? result)
    {
        result = null;
        if (input.Length < 0x100)
            return false;
        if (input is [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..])
            return TryParseMgbaPng(input, path, out result);
        if (input.Length >= 8)
        {
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(input);
            if (magic - MgbaMagicGB < 0x100 && input.Length >= MgbaStateSizeGB)
                return TryParseMgbaRaw(input, path, StateConsole.GB, out result);
            if (magic - MgbaMagicGBA < 0x100 && input.Length >= MgbaStateSizeGBA)
                return TryParseMgbaRaw(input, path, StateConsole.GBA, out result);
        }
        if (input.EndsWith("BESS"u8))
            return TryParseBess(input, input.Length, path, out result);
        if (input.StartsWith("EMAS"u8) || input.StartsWith("BGB1.0"u8))
        {
            int mark = input.LastIndexOf("BESS"u8);
            if (mark >= 8)
                return TryParseBess(input, mark + 4, path, out result);
            return false;
        }
        if (input is [0x1F, 0x8B, ..])
            return TryParseVbaM(input, path, out result);
        if (input.StartsWith("DeSmuME SState"u8))
            return TryParseDeSmuME(input, path, out result);
        if (input.StartsWith("MELN"u8))
            return TryParseMelonDS(input, path, out result);
        if (input is [0x43, 0x53, 0x54, 0x1B, ..])
            return TryParseCitra(input, path, out result);
        if (input is [0x50, 0x4B, 0x03, 0x04, ..] || input is [0x28, 0xB5, 0x2F, 0xFD, ..])
            return false;
        return TryParseRaw(input, path, out result);
    }

    /// True when the file is a compressed container that must never be structurally scanned raw.
    public static bool IsCompressedContainer(ReadOnlySpan<byte> input) => input.Length >= 4 &&
        (input is [0x89, 0x50, 0x4E, 0x47, ..] || input is [0x1F, 0x8B, ..] || input is [0x50, 0x4B, 0x03, 0x04, ..] || input is [0x28, 0xB5, 0x2F, 0xFD, ..]);

    private static bool TryParseMgbaPng(ReadOnlySpan<byte> input, string path, out SaveStateFile? result)
    {
        result = null;
        byte[]? state = null;
        var chunks = new List<(byte[] Type, byte[] Payload)>();
        int pos = 8;
        while (pos + 12 <= input.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(input[pos..]);
            if (length < 0 || pos + 12 + length > input.Length)
                return false;
            var type = input.Slice(pos + 4, 4).ToArray();
            var payload = input.Slice(pos + 8, length).ToArray();
            chunks.Add((type, payload));
            pos += 12 + length;
        }
        foreach (var (type, payload) in chunks)
        {
            if (type.AsSpan().SequenceEqual("gbAs"u8))
                state = Inflate(payload, MgbaStateSizeGBA);
        }
        if (state is null)
            return false;
        StateConsole console;
        if (state.Length == MgbaStateSizeGB)
            console = StateConsole.GB;
        else if (state.Length == MgbaStateSizeGBA)
            console = StateConsole.GBA;
        else
            return false;

        var file = new SaveStateFile(SaveStateKind.MgbaPng, console, input.ToArray(), state) { FilePath = path };
        file._pngChunks.AddRange(chunks);
        file.SetMgbaRegions();
        for (int i = 0; i < chunks.Count; i++)
        {
            var (type, payload) = chunks[i];
            if (!type.AsSpan().SequenceEqual("gbAx"u8) || payload.Length < 8)
                continue;
            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(payload);
            int size = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(4));
            if (tag != 2 || size <= 0 || size > 0x400000)
                continue;
            var sav = Inflate(payload.AsSpan(8), size);
            if (sav?.Length == size)
            {
                file.EmbeddedSave = sav;
                file._mgbaSaveChunkIndex = i;
            }
        }
        result = file;
        return true;
    }

    private static bool TryParseMgbaRaw(ReadOnlySpan<byte> input, string path, StateConsole console, out SaveStateFile? result)
    {
        result = null;
        int stateSize = console == StateConsole.GB ? MgbaStateSizeGB : MgbaStateSizeGBA;
        if (input.Length < stateSize)
            return false;
        var file = new SaveStateFile(SaveStateKind.MgbaRaw, console, input.ToArray(), input[..stateSize].ToArray()) { FilePath = path };
        file.SetMgbaRegions();
        int pos = stateSize;
        while (pos + 16 <= input.Length)
        {
            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(input[pos..]);
            int size = BinaryPrimitives.ReadInt32LittleEndian(input[(pos + 4)..]);
            long offset = BinaryPrimitives.ReadInt64LittleEndian(input[(pos + 8)..]);
            if (tag == 0)
                break;
            if (size > 0 && offset > 0 && offset + size <= input.Length)
            {
                file._rawExtdata.Add((tag, (int)offset, size));
                if (tag == 2)
                {
                    file.EmbeddedSave = input.Slice((int)offset, size).ToArray();
                    file._embeddedSaveOffset = (int)offset;
                }
            }
            pos += 16;
        }
        result = file;
        return true;
    }

    private void SetMgbaRegions()
    {
        if (Console == StateConsole.GB)
        {
            WramOffset = MgbaWramGB;
            WramSize = 0x8000;
        }
        else
        {
            IwramOffset = MgbaIwramGBA;
            EwramOffset = MgbaEwramGBA;
        }
    }

    private static bool TryParseBess(ReadOnlySpan<byte> input, int footerEnd, string path, out SaveStateFile? result)
    {
        result = null;
        int start = BinaryPrimitives.ReadInt32LittleEndian(input[(footerEnd - 8)..]);
        if (start < 0 || start >= footerEnd - 8)
            return false;
        int wramOffset = -1, wramSize = 0, sramOffset = -1, sramSize = 0;
        int pos = start;
        while (pos + 8 <= footerEnd - 8)
        {
            var name = input.Slice(pos, 4);
            int length = BinaryPrimitives.ReadInt32LittleEndian(input[(pos + 4)..]);
            if (length < 0 || pos + 8 + length > input.Length)
                return false;
            if (name.SequenceEqual("END "u8))
                break;
            if (name.SequenceEqual("CORE"u8) && length >= 0xD0)
            {
                var core = input.Slice(pos + 8, length);
                wramSize = BinaryPrimitives.ReadInt32LittleEndian(core[0x98..]);
                wramOffset = BinaryPrimitives.ReadInt32LittleEndian(core[0x9C..]);
                sramSize = BinaryPrimitives.ReadInt32LittleEndian(core[0xA8..]);
                sramOffset = BinaryPrimitives.ReadInt32LittleEndian(core[0xAC..]);
            }
            pos += 8 + length;
        }
        if (wramOffset < 0 || wramSize is not (0x2000 or 0x8000) || wramOffset + wramSize > input.Length)
            return false;
        var file = new SaveStateFile(SaveStateKind.Bess, StateConsole.GB, input.ToArray(), input.ToArray())
        {
            FilePath = path,
            WramOffset = wramOffset,
            WramSize = wramSize,
        };
        if (sramOffset > 0 && sramSize > 0 && sramOffset + sramSize <= input.Length)
        {
            file.EmbeddedSave = input.Slice(sramOffset, sramSize).ToArray();
            file._embeddedSaveOffset = sramOffset;
        }
        result = file;
        return true;
    }

    private static bool TryParseVbaM(ReadOnlySpan<byte> input, string path, out SaveStateFile? result)
    {
        result = null;
        byte[] blob;
        try
        {
            using var ms = new MemoryStream(input.ToArray());
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var outStream = new MemoryStream();
            gz.CopyTo(outStream);
            blob = outStream.ToArray();
        }
        catch (Exception)
        {
            return false;
        }
        if (blob.Length < 0x2000)
            return false;
        var console = blob.Length > 0x50000 ? StateConsole.GBA : StateConsole.GB;
        result = new SaveStateFile(SaveStateKind.VbaM, console, input.ToArray(), blob) { FilePath = path };
        return true;
    }

    private static bool TryParseDeSmuME(ReadOnlySpan<byte> input, string path, out SaveStateFile? result)
    {
        result = null;
        if (input.Length < 0x20)
            return false;
        int len = BinaryPrimitives.ReadInt32LittleEndian(input[0x18..]);
        uint comprLen = BinaryPrimitives.ReadUInt32LittleEndian(input[0x1C..]);
        byte[] blob;
        if (comprLen == 0xFFFFFFFF)
        {
            blob = input[0x20..].ToArray();
        }
        else
        {
            if (len <= 0 || len > 0x8000000)
                return false;
            blob = Inflate(input[0x20..], len) ?? [];
            if (blob.Length != len)
                return false;
        }
        var file = new SaveStateFile(SaveStateKind.DeSmuME, StateConsole.NDS, input.ToArray(), blob) { FilePath = path };
        ReadOnlySpan<byte> wramTag = [0x57, 0x52, 0x41, 0x4D, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00];
        int pos = 0;
        while (pos + 4 <= blob.Length)
        {
            uint sectionId = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(pos));
            if (sectionId == 0xFFFFFFFF)
                break;
            if (pos + 8 > blob.Length)
                break;
            int sectionSize = BinaryPrimitives.ReadInt32LittleEndian(blob.AsSpan(pos + 4));
            if (sectionSize < 0 || pos + 8 + sectionSize > blob.Length)
                break;
            var payload = blob.AsSpan(pos + 8, sectionSize);
            if (sectionId == 4)
            {
                int tag = payload.IndexOf(wramTag);
                if (tag >= 0 && tag + 12 + 0x400000 <= payload.Length)
                {
                    file.MainRamOffset = pos + 8 + tag + 12;
                    file.MainRamSize = 0x400000;
                }
            }
            else if (sectionId == 61 && sectionSize >= 0x20)
            {
                int saveLen = BinaryPrimitives.ReadInt32LittleEndian(payload[0x18..]);
                if (saveLen > 0 && 0x1C + saveLen <= sectionSize)
                {
                    file.EmbeddedSave = payload.Slice(0x1C, saveLen).ToArray();
                    file._embeddedSaveOffset = pos + 8 + 0x1C;
                }
            }
            pos += 8 + sectionSize;
        }
        if (file.MainRamOffset < 0)
            return false;
        result = file;
        return true;
    }

    private static bool TryParseMelonDS(ReadOnlySpan<byte> input, string path, out SaveStateFile? result)
    {
        result = null;
        var file = new SaveStateFile(SaveStateKind.MelonDS, StateConsole.NDS, input.ToArray(), input.ToArray()) { FilePath = path };
        int pos = 0x10;
        while (pos + 16 <= input.Length)
        {
            var magic = input.Slice(pos, 4);
            int length = BinaryPrimitives.ReadInt32LittleEndian(input[(pos + 4)..]);
            if (length < 16 || pos + length > input.Length)
                break;
            if (magic.SequenceEqual("NDSG"u8))
            {
                file.MainRamOffset = pos + 20;
                file.MainRamSize = Math.Min(0x1000000, length - 20);
            }
            else if (magic.SequenceEqual("NDCS"u8) && length >= 16 + 27)
            {
                int payload = pos + 16;
                int sramLength = BinaryPrimitives.ReadInt32LittleEndian(input[(payload + 23)..]);
                if (sramLength > 0 && payload + 27 + sramLength <= pos + length)
                {
                    file.EmbeddedSave = input.Slice(payload + 27, sramLength).ToArray();
                    file._embeddedSaveOffset = payload + 27;
                }
            }
            pos += length;
        }
        if (file.MainRamOffset < 0)
            return false;
        result = file;
        return true;
    }

    private static bool TryParseCitra(ReadOnlySpan<byte> input, string path, out SaveStateFile? result)
    {
        result = null;
        if (input.Length < 0x200)
            return false;
        byte[] payload;
        try
        {
            using var decompressor = new ZstdSharp.Decompressor();
            payload = decompressor.Unwrap(input[0x100..]).ToArray();
        }
        catch (Exception)
        {
            return false;
        }
        if (payload.Length < 0x600000)
            return false;
        var file = new SaveStateFile(SaveStateKind.CitraCst, StateConsole.N3DS, input.ToArray(), payload) { FilePath = path };
        file.MainRamOffset = 0;
        file.MainRamSize = payload.Length;
        result = file;
        return true;
    }

    private static bool TryParseRaw(ReadOnlySpan<byte> input, string path, out SaveStateFile? result)
    {
        result = new SaveStateFile(SaveStateKind.RawMemory, StateConsole.GB, input.ToArray(), input.ToArray()) { FilePath = path };
        return true;
    }

    public void SetEmbeddedSave(ReadOnlySpan<byte> data)
    {
        if (EmbeddedSave is null || data.Length != EmbeddedSave.Length)
            throw new InvalidOperationException("Embedded save size mismatch.");
        data.CopyTo(EmbeddedSave);
    }

    public byte[] Serialize()
    {
        switch (Kind)
        {
            case SaveStateKind.MgbaPng:
                return SerializeMgbaPng();
            case SaveStateKind.MgbaRaw:
            {
                var output = FileData.ToArray();
                State.CopyTo(output, 0);
                if (EmbeddedSave is not null && _embeddedSaveOffset >= 0)
                    EmbeddedSave.CopyTo(output, _embeddedSaveOffset);
                return output;
            }
            case SaveStateKind.Bess:
            case SaveStateKind.MelonDS:
            case SaveStateKind.RawMemory:
            {
                var output = State.ToArray();
                if (EmbeddedSave is not null && _embeddedSaveOffset >= 0)
                    EmbeddedSave.CopyTo(output, _embeddedSaveOffset);
                return output;
            }
            case SaveStateKind.VbaM:
            {
                using var ms = new MemoryStream();
                using (var gz = new GZipStream(ms, CompressionLevel.Optimal, true))
                    gz.Write(State);
                return ms.ToArray();
            }
            case SaveStateKind.CitraCst:
            {
                using var compressor = new ZstdSharp.Compressor(3);
                var compressed = compressor.Wrap(State).ToArray();
                var output = new byte[0x100 + compressed.Length];
                FileData.AsSpan(0, 0x100).CopyTo(output);
                compressed.CopyTo(output, 0x100);
                return output;
            }
            case SaveStateKind.DeSmuME:
            {
                var blob = State.ToArray();
                if (EmbeddedSave is not null && _embeddedSaveOffset >= 0)
                    EmbeddedSave.CopyTo(blob, _embeddedSaveOffset);
                var output = new byte[0x20 + blob.Length];
                FileData.AsSpan(0, 0x20).CopyTo(output);
                BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(0x18), output.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0x1C), 0xFFFFFFFF);
                blob.CopyTo(output, 0x20);
                return output;
            }
            default:
                throw new InvalidOperationException();
        }
    }

    private byte[] SerializeMgbaPng()
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        for (int i = 0; i < _pngChunks.Count; i++)
        {
            var (type, payload) = _pngChunks[i];
            if (type.AsSpan().SequenceEqual("gbAs"u8))
            {
                payload = Deflate(State);
            }
            else if (i == _mgbaSaveChunkIndex && EmbeddedSave is not null)
            {
                var fresh = new byte[8].Concat(Deflate(EmbeddedSave));
                BinaryPrimitives.WriteUInt32LittleEndian(fresh, 2);
                BinaryPrimitives.WriteInt32LittleEndian(fresh.AsSpan(4), EmbeddedSave.Length);
                payload = fresh;
            }
            WritePngChunk(ms, type, payload);
        }
        return ms.ToArray();
    }

    private static void WritePngChunk(Stream stream, byte[] type, byte[] payload)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, payload.Length);
        stream.Write(len);
        stream.Write(type);
        stream.Write(payload);
        uint crc = 0xFFFFFFFF;
        crc = Crc32Update(crc, type);
        crc = Crc32Update(crc, payload);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        stream.Write(crcBytes);
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    private static uint Crc32Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static byte[]? Inflate(ReadOnlySpan<byte> compressed, int maxSize)
    {
        try
        {
            using var ms = new MemoryStream(compressed.ToArray());
            using var z = new ZLibStream(ms, CompressionMode.Decompress);
            using var outStream = new MemoryStream();
            z.CopyTo(outStream);
            var blob = outStream.ToArray();
            return blob.Length <= maxSize + 0x1000 ? blob : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static byte[] Deflate(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
            z.Write(raw);
        return ms.ToArray();
    }
}

file static class ByteArrayExtensions
{
    public static byte[] Concat(this byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result, 0);
        second.CopyTo(result, first.Length);
        return result;
    }
}
