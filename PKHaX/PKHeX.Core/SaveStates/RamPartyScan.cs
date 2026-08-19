using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace PKHeX.Core;

/// PKHaX: locates encrypted Gen 3/4 party structures inside raw console memory by checksum validation.
public sealed class RamParty
{
    public required SaveStateFile State { get; init; }
    public required int Offset { get; init; }
    public required int Count { get; init; }
    public required int EntrySize { get; init; }
    public required byte Generation { get; init; }

    public PKM GetSlot(int slot)
    {
        var raw = State.State.AsSpan(Offset + (slot * EntrySize), EntrySize).ToArray();
        if (Generation == 3)
        {
            PokeCrypto.DecryptIfEncrypted3(raw);
            return new PK3(raw);
        }
        PokeCrypto.DecryptIfEncrypted45(raw);
        return new PK4(raw);
    }

    public void SetSlot(int slot, PKM pk)
    {
        pk.RefreshChecksum();
        var dest = State.State.AsSpan(Offset + (slot * EntrySize), EntrySize);
        pk.WriteEncryptedDataParty(dest);
    }
}

public static class RamPartyScan
{
    public static List<RamParty> FindGen3Parties(SaveStateFile state)
    {
        var results = new List<RamParty>();
        if (state.EwramOffset >= 0)
        {
            Scan(state, state.EwramOffset, 0x40000, 3, PokeCrypto.SIZE_3PARTY, results);
            if (state.IwramOffset >= 0)
                Scan(state, state.IwramOffset, 0x8000, 3, PokeCrypto.SIZE_3PARTY, results);
        }
        else if (state.Kind is SaveStateKind.VbaM or SaveStateKind.RawMemory)
        {
            Scan(state, 0, state.State.Length, 3, PokeCrypto.SIZE_3PARTY, results);
        }
        return results;
    }

    public static List<RamParty> FindGen4Parties(SaveStateFile state)
    {
        var results = new List<RamParty>();
        if (state.MainRamOffset >= 0)
            Scan(state, state.MainRamOffset, state.MainRamSize, 4, PokeCrypto.SIZE_4PARTY, results);
        return results;
    }

    private static void Scan(SaveStateFile state, int start, int length, byte generation, int entrySize, List<RamParty> results)
    {
        var data = state.State;
        int end = Math.Min(start + length, data.Length) - entrySize;
        int pos = start;
        while (pos <= end)
        {
            if (!IsValidMon(data, pos, generation))
            {
                pos += 4;
                continue;
            }
            int count = 1;
            while (count < 6 && pos + ((count + 1) * entrySize) <= start + length && IsValidMon(data, pos + (count * entrySize), generation))
                count++;
            results.Add(new RamParty
            {
                State = state,
                Offset = pos,
                Count = count,
                EntrySize = entrySize,
                Generation = generation,
            });
            pos += count * entrySize;
            pos = (pos + 3) & ~3;
        }
    }

    private static bool IsValidMon(ReadOnlySpan<byte> data, int offset, byte generation)
    {
        return generation == 3 ? IsValidMon3(data[offset..]) : IsValidMon4(data[offset..]);
    }

    private static bool IsValidMon3(ReadOnlySpan<byte> data)
    {
        uint pid = BinaryPrimitives.ReadUInt32LittleEndian(data);
        uint oid = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        if (pid == 0 && oid == 0)
            return false;
        Span<byte> copy = stackalloc byte[PokeCrypto.SIZE_3STORED];
        data[..PokeCrypto.SIZE_3STORED].CopyTo(copy);
        PokeCrypto.DecryptIfEncrypted3(copy);
        if (PokeCrypto.IsEncrypted3(copy))
            return false;
        ushort species = BinaryPrimitives.ReadUInt16LittleEndian(copy[0x20..]);
        if (species is 0 or > 440)
            return false;
        ushort move1 = BinaryPrimitives.ReadUInt16LittleEndian(copy[0x2C..]);
        if (move1 > 354)
            return false;
        int level = data[0x54];
        if (level is < 2 or > 100)
            return false;
        ushort maxHP = BinaryPrimitives.ReadUInt16LittleEndian(data[0x58..]);
        ushort curHP = BinaryPrimitives.ReadUInt16LittleEndian(data[0x56..]);
        return maxHP is 1 or (>= 10 and < 1000) && curHP <= maxHP;
    }

    private static bool IsValidMon4(ReadOnlySpan<byte> data)
    {
        uint pid = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (pid == 0)
            return false;
        Span<byte> copy = stackalloc byte[PokeCrypto.SIZE_4PARTY];
        data[..PokeCrypto.SIZE_4PARTY].CopyTo(copy);
        if (PokeCrypto.IsEncrypted45(copy))
            PokeCrypto.Decrypt45(copy);
        var chk = Checksums.Add16(copy[8..PokeCrypto.SIZE_4STORED]);
        if (chk != BinaryPrimitives.ReadUInt16LittleEndian(copy[6..]))
            return false;
        ushort species = BinaryPrimitives.ReadUInt16LittleEndian(copy[8..]);
        if (species is 0 or > 507)
            return false;
        int level = copy[0x8C];
        if (level is < 2 or > 100)
            return false;
        ushort maxHP = BinaryPrimitives.ReadUInt16LittleEndian(copy[0x90..]);
        ushort curHP = BinaryPrimitives.ReadUInt16LittleEndian(copy[0x8E..]);
        return maxHP is 1 or (>= 10 and < 1000) && curHP <= maxHP;
    }
}
