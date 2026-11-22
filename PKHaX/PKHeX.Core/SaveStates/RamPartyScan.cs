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
    public int Stride { get; init; }

    private int SlotOffset(int slot) => Offset + (slot * (Stride == 0 ? EntrySize : Stride));

    public PKM GetSlot(int slot)
    {
        var raw = State.State.AsSpan(SlotOffset(slot), EntrySize).ToArray();
        if (Generation == 3)
        {
            PokeCrypto.DecryptIfEncrypted3(raw);
            return new PK3(raw);
        }
        if (Generation is 6 or 7)
        {
            PokeCrypto.DecryptIfEncrypted67(raw);
            return Generation == 7 ? new PK7(raw) : new PK6(raw);
        }
        PokeCrypto.DecryptIfEncrypted45(raw);
        return Generation == 5 ? new PK5(raw) : new PK4(raw);
    }

    public void SetSlot(int slot, PKM pk)
    {
        pk.RefreshChecksum();
        var dest = State.State.AsSpan(SlotOffset(slot), EntrySize);
        pk.WriteEncryptedDataParty(dest);
    }
}

public static class RamPartyScan
{
    public static List<RamParty> FindGen3Parties(SaveStateFile state) => FindGen3Parties(state, RamScanLimits.Default);

    public static List<RamParty> FindGen3Parties(SaveStateFile state, RamScanLimits limits)
    {
        var results = new List<RamParty>();
        if (state.EwramOffset >= 0)
        {
            Scan(state, state.EwramOffset, 0x40000, 3, PokeCrypto.SIZE_3PARTY, results, limits);
            if (state.IwramOffset >= 0)
                Scan(state, state.IwramOffset, 0x8000, 3, PokeCrypto.SIZE_3PARTY, results, limits);
        }
        else if (state.Kind is SaveStateKind.VbaM or SaveStateKind.RawMemory)
        {
            Scan(state, 0, state.State.Length, 3, PokeCrypto.SIZE_3PARTY, results, limits);
        }
        return results;
    }

    public static List<RamParty> FindGen67Parties(SaveStateFile state) => FindGen67Parties(state, RamScanLimits.Default);

    public static List<RamParty> FindGen67Parties(SaveStateFile state, RamScanLimits limits)
    {
        var results = new List<RamParty>();
        if (state.Console != StateConsole.N3DS || state.MainRamOffset < 0)
            return results;
        var data = state.State;
        const int stride = 0x1E4;
        int end = data.Length - PokeCrypto.SIZE_6PARTY;
        int pos = 0;
        while (pos <= end)
        {
            if (!IsValidMon67(data, pos, limits))
            {
                pos += 4;
                continue;
            }
            int count = 1;
            while (count < 6 && pos + (count * stride) + PokeCrypto.SIZE_6PARTY <= data.Length && IsValidMon67(data, pos + (count * stride), limits))
                count++;
            byte generation = VersionGeneration67(data, pos);
            results.Add(new RamParty
            {
                State = state,
                Offset = pos,
                Count = count,
                EntrySize = PokeCrypto.SIZE_6PARTY,
                Generation = generation,
                Stride = stride,
            });
            pos += count * stride;
            pos = (pos + 3) & ~3;
        }
        results.RemoveAll(p => p.Count == 1 && results.Exists(o => o != p && p.Offset > o.Offset && p.Offset < o.Offset + (6 * stride)));
        return results;
    }

    private static bool IsValidMon67(ReadOnlySpan<byte> data, int offset, RamScanLimits limits)
    {
        var slice = data[offset..];
        uint pid = BinaryPrimitives.ReadUInt32LittleEndian(slice);
        if (pid == 0)
            return false;
        if (BinaryPrimitives.ReadUInt16LittleEndian(slice[4..]) != 0)
            return false;
        Span<byte> copy = stackalloc byte[PokeCrypto.SIZE_6PARTY];
        slice[..PokeCrypto.SIZE_6PARTY].CopyTo(copy);
        PokeCrypto.DecryptIfEncrypted67(copy);
        var chk = Checksums.Add16(copy[8..PokeCrypto.SIZE_6STORED]);
        if (chk != BinaryPrimitives.ReadUInt16LittleEndian(copy[6..]))
            return false;
        ushort species = BinaryPrimitives.ReadUInt16LittleEndian(copy[8..]);
        if (species == 0 || species > limits.MaxSpecies67)
            return false;
        int level = copy[0xEC];
        if (level < limits.MinLevel67 || level > limits.MaxLevel)
            return false;
        ushort maxHP = BinaryPrimitives.ReadUInt16LittleEndian(copy[0xF2..]);
        ushort curHP = BinaryPrimitives.ReadUInt16LittleEndian(copy[0xF0..]);
        return maxHP is 1 or (>= 10 and < 1000) && curHP <= maxHP;
    }

    private static byte VersionGeneration67(ReadOnlySpan<byte> data, int offset)
    {
        Span<byte> copy = stackalloc byte[PokeCrypto.SIZE_6STORED];
        data.Slice(offset, PokeCrypto.SIZE_6STORED).CopyTo(copy);
        PokeCrypto.DecryptIfEncrypted67(copy);
        byte version = copy[0xDF];
        return version is >= 30 and <= 33 ? (byte)7 : (byte)6;
    }

    public static List<RamParty> FindGen4Parties(SaveStateFile state) => FindGen4Parties(state, RamScanLimits.Default);

    public static List<RamParty> FindGen4Parties(SaveStateFile state, RamScanLimits limits)
    {
        var results = new List<RamParty>();
        if (state.MainRamOffset < 0)
            return results;
        Scan(state, state.MainRamOffset, state.MainRamSize, 4, PokeCrypto.SIZE_4PARTY, results, limits);
        Scan(state, state.MainRamOffset, state.MainRamSize, 5, PokeCrypto.SIZE_5PARTY, results, limits);
        results.RemoveAll(p => p.Count == 1 && results.Exists(o => o != p && o.Offset == p.Offset && o.Count > 1));
        results.RemoveAll(p => p.Generation != VersionGeneration(state, p.Offset, p.Generation));
        return results;
    }

    private static byte VersionGeneration(SaveStateFile state, int offset, byte fallback)
    {
        Span<byte> copy = stackalloc byte[PokeCrypto.SIZE_4STORED];
        state.State.AsSpan(offset, PokeCrypto.SIZE_4STORED).CopyTo(copy);
        if (PokeCrypto.IsEncrypted45(copy))
            PokeCrypto.Decrypt45(copy);
        byte version = copy[0x5F];
        return version switch
        {
            >= 20 and <= 23 => 5,
            (>= 7 and <= 8) or (>= 10 and <= 12) => 4,
            _ => fallback,
        };
    }

    private static void Scan(SaveStateFile state, int start, int length, byte generation, int entrySize, List<RamParty> results, RamScanLimits limits)
    {
        var data = state.State;
        int end = Math.Min(start + length, data.Length) - entrySize;
        int pos = start;
        while (pos <= end)
        {
            if (!IsValidMon(data, pos, generation, limits))
            {
                pos += 4;
                continue;
            }
            int count = 1;
            while (count < 6 && pos + ((count + 1) * entrySize) <= start + length && IsValidMon(data, pos + (count * entrySize), generation, limits))
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

    private static bool IsValidMon(ReadOnlySpan<byte> data, int offset, byte generation, RamScanLimits limits)
    {
        return generation == 3 ? IsValidMon3(data[offset..], limits) : IsValidMon45(data[offset..], generation, limits);
    }

    private static bool IsValidMon3(ReadOnlySpan<byte> data, RamScanLimits limits)
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
        if (species == 0 || species > limits.MaxSpecies3)
            return false;
        ushort move1 = BinaryPrimitives.ReadUInt16LittleEndian(copy[0x2C..]);
        if (move1 > limits.MaxMove3)
            return false;
        int level = data[0x54];
        if (level < limits.MinLevel || level > limits.MaxLevel)
            return false;
        ushort maxHP = BinaryPrimitives.ReadUInt16LittleEndian(data[0x58..]);
        ushort curHP = BinaryPrimitives.ReadUInt16LittleEndian(data[0x56..]);
        if (!(maxHP is 1 or (>= 10 and < 1000) && curHP <= maxHP))
            return false;
        if (limits.Gen3Validator is not { } validator)
            return true;
        var party = new byte[PokeCrypto.SIZE_3PARTY];
        copy.CopyTo(party);
        data[PokeCrypto.SIZE_3STORED..PokeCrypto.SIZE_3PARTY].CopyTo(party.AsSpan(PokeCrypto.SIZE_3STORED));
        return validator(new PK3(party));
    }

    private static bool IsValidMon45(ReadOnlySpan<byte> data, byte generation, RamScanLimits limits)
    {
        uint pid = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (pid == 0)
            return false;
        int partySize = generation == 4 ? PokeCrypto.SIZE_4PARTY : PokeCrypto.SIZE_5PARTY;
        Span<byte> copy = stackalloc byte[PokeCrypto.SIZE_4PARTY];
        copy = copy[..partySize];
        data[..partySize].CopyTo(copy);
        if (PokeCrypto.IsEncrypted45(copy))
            PokeCrypto.Decrypt45(copy);
        var chk = Checksums.Add16(copy[8..PokeCrypto.SIZE_4STORED]);
        if (chk != BinaryPrimitives.ReadUInt16LittleEndian(copy[6..]))
            return false;
        ushort species = BinaryPrimitives.ReadUInt16LittleEndian(copy[8..]);
        if (species == 0 || species > (generation == 4 ? limits.MaxSpecies4 : limits.MaxSpecies5))
            return false;
        int level = copy[0x8C];
        if (level < limits.MinLevel || level > limits.MaxLevel)
            return false;
        ushort maxHP = BinaryPrimitives.ReadUInt16LittleEndian(copy[0x90..]);
        ushort curHP = BinaryPrimitives.ReadUInt16LittleEndian(copy[0x8E..]);
        return maxHP is 1 or (>= 10 and < 1000) && curHP <= maxHP;
    }
}
