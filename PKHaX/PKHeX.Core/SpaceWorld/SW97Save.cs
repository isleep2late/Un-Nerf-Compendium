using System;
using System.IO;

namespace PKHeX.Core;

/// PKHaX: Space World '97 demo party editing, for emulator save states and the 32 KB battery file.
public sealed class SW97Save
{
    public const int MonSize = 48;
    public const int PartyLength = 6;
    public const int NameLength = 6;

    private const int WramStart = 0xC000;
    private const int AddressPartyCount = 0xD6AA;
    private const int AddressPlayerName = 0xCE67;
    private const int AddressBattleMon = 0xCA02;
    private const int AddressEnemyMon = 0xCDD9;

    private const int BatterySize = 0x8000;
    private const int BatteryParty = 0x2000;
    private const int BatteryPartyLength = 0x3D9;
    private const int BatteryGameData = 0x0608;
    private const int BatteryGameDataLength = 0x7D2;
    private const int BatteryGameData2 = BatteryGameData + BatteryGameDataLength;
    private const int BatteryGameData2Length = 0x71;
    private const int BatteryChecksum = 0x7FFD;

    public const int BattleSpecies = 0x00;
    public const int BattleLevel = 0x0D;
    public const int BattleType1 = 0x1E;
    public const int BattleType2 = 0x1F;

    public byte[] Data { get; }
    public int PartyOffset { get; }
    public bool IsBattery { get; }
    public string FilePath { get; set; } = string.Empty;
    public SaveStateFile? Container { get; }

    private SW97Save(byte[] data, int partyOffset, bool battery, SaveStateFile? container = null)
    {
        Data = data;
        PartyOffset = partyOffset;
        IsBattery = battery;
        Container = container;
    }

    public static bool TryLoad(ReadOnlySpan<byte> input, string path, out SW97Save? result)
    {
        result = null;
        if (input.Length < BatteryParty + BatteryPartyLength)
            return false;

        if (input.Length == BatterySize && IsBatteryValid(input))
        {
            result = new SW97Save(input.ToArray(), BatteryParty, true) { FilePath = path };
            return true;
        }

        if (SaveStateFile.TryParse(input, path, out var container) && container is not null && container.Kind != SaveStateKind.RawMemory)
        {
            if (container.WramOffset < 0)
                return false;
            int partyBase = container.GetGBOffset(AddressPartyCount);
            if (partyBase < 0 || !IsPartyShaped(container.State, partyBase))
                return false;
            var save = new SW97Save(container.State, partyBase, false, container) { FilePath = path };
            if (!save.IsWramAnchorValid)
                return false;
            result = save;
            return true;
        }

        if (SaveStateFile.IsCompressedContainer(input))
            return false;

        var data = input.ToArray();
        int offset = FindParty(data);
        if (offset < 0)
            return false;
        result = new SW97Save(data, offset, false) { FilePath = path };
        return result.IsWramAnchorValid;
    }

    private static bool IsBatteryValid(ReadOnlySpan<byte> data)
    {
        if (!IsPartyShaped(data, BatteryParty))
            return false;
        return data[BatteryChecksum] == Checksum(data.Slice(BatteryParty, BatteryPartyLength))
            && data[BatteryChecksum + 1] == Checksum(data.Slice(BatteryGameData2, BatteryGameData2Length))
            && data[BatteryChecksum + 2] == Checksum(data.Slice(BatteryGameData, BatteryGameDataLength));
    }

    private static int FindParty(ReadOnlySpan<byte> data)
    {
        int limit = data.Length - (8 + (PartyLength * MonSize));
        int found = -1;
        for (int i = 0; i < limit; i++)
        {
            if (!IsPartyShaped(data, i))
                continue;
            if (found >= 0)
                return found;
            found = i;
        }
        return found;
    }

    private static bool IsPartyShaped(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset + 8 + (PartyLength * MonSize) > data.Length)
            return false;
        int count = data[offset];
        if (count is < 1 or > PartyLength)
            return false;
        if (data[offset + 1 + count] != 0xFF)
            return false;
        for (int i = 0; i < count; i++)
        {
            int species = data[offset + 1 + i];
            if (species is 0 or 0xFF)
                return false;
            int mon = offset + 8 + (i * MonSize);
            if (data[mon] != species)
                return false;
            if (data[mon + SW97Mon.OffsetLevel] == 0)
                return false;
        }
        return true;
    }

    private static byte Checksum(ReadOnlySpan<byte> data)
    {
        byte sum = 0;
        foreach (var b in data)
            sum += b;
        return (byte)~sum;
    }

    public int PartyCount
    {
        get => Data[PartyOffset];
        set
        {
            int count = Math.Clamp(value, 0, PartyLength);
            Data[PartyOffset] = (byte)count;
            for (int i = count; i < PartyLength; i++)
                Data[PartyOffset + 1 + i] = 0;
            Data[PartyOffset + 1 + count] = 0xFF;
        }
    }

    /// True when the loaded game is an English translation patch (retail Latin charset instead of the demo's Japanese one).
    public bool IsEnglish
    {
        get
        {
            int offset = IsBattery ? -1 : GetWramOffset(AddressPlayerName);
            if (offset >= 0 && SW97Data.IsLatinName(Data.AsSpan(offset, NameLength)))
                return true;
            if (PartyCount > 0)
            {
                int ot = PartyOffset + 8 + (PartyLength * MonSize);
                return SW97Data.IsLatinName(Data.AsSpan(ot, NameLength));
            }
            return false;
        }
    }

    public SW97Mon GetMon(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)slot, (uint)PartyLength);
        int mon = PartyOffset + 8 + (slot * MonSize);
        int ot = PartyOffset + 8 + (PartyLength * MonSize) + (slot * NameLength);
        int nick = ot + (PartyLength * NameLength);
        return new SW97Mon(Data, mon, ot, nick) { English = IsEnglish };
    }

    public void SyncSpeciesList()
    {
        int count = PartyCount;
        for (int i = 0; i < count; i++)
            Data[PartyOffset + 1 + i] = (byte)GetMon(i).Species;
        Data[PartyOffset + 1 + count] = 0xFF;
    }

    public void InitializeSlot(int slot)
    {
        var mon = GetMon(slot);
        mon.Clear();
        mon.Species = 1;
        mon.Level = 5;
        mon.Happiness = 70;
        if (slot != 0)
        {
            var donor = GetMon(0);
            donor.OTNameRaw.CopyTo(mon.OTNameRaw);
        }
        else
        {
            mon.OTNameRaw.Fill(0x50);
        }
        mon.NicknameRaw.Fill(0x50);
    }

    public int WramBase => PartyOffset - (AddressPartyCount - WramStart);

    private int GetWramOffset(int address)
    {
        int offset = WramBase + (address - WramStart);
        if (offset < 0 || offset + 0x20 > Data.Length)
            return -1;
        return offset;
    }

    public bool IsWramAnchorValid
    {
        get
        {
            if (IsBattery)
                return false;
            int offset = GetWramOffset(AddressPlayerName);
            if (offset < 0)
                return false;
            return SW97Data.IsCleanName(Data.AsSpan(offset, NameLength));
        }
    }

    public string PlayerName
    {
        get
        {
            int offset = GetWramOffset(AddressPlayerName);
            return offset < 0 ? string.Empty : SW97Data.DecodeName(Data.AsSpan(offset, NameLength), IsEnglish);
        }
    }

    public Span<byte> GetBattleMon(bool enemy)
    {
        int offset = GetWramOffset(enemy ? AddressEnemyMon : AddressBattleMon);
        return offset < 0 ? [] : Data.AsSpan(offset, 0x20);
    }

    public bool IsBattleActive
    {
        get
        {
            if (IsBattery)
                return false;
            var battler = GetBattleMon(false);
            if (battler.Length == 0)
                return false;
            int species = battler[BattleSpecies];
            int level = battler[BattleLevel];
            if (species == 0 || level == 0)
                return false;
            for (int i = 0; i < PartyCount; i++)
            {
                var mon = GetMon(i);
                if (mon.Species == species && mon.Level == level)
                    return true;
            }
            return false;
        }
    }

    public void FixChecksums()
    {
        if (!IsBattery)
            return;
        Data[BatteryChecksum] = Checksum(Data.AsSpan(BatteryParty, BatteryPartyLength));
        Data[BatteryChecksum + 1] = Checksum(Data.AsSpan(BatteryGameData2, BatteryGameData2Length));
        Data[BatteryChecksum + 2] = Checksum(Data.AsSpan(BatteryGameData, BatteryGameDataLength));
    }

    public byte[] PrepareForWrite()
    {
        SyncSpeciesList();
        FixChecksums();
        return Container?.Serialize() ?? Data;
    }

    public void Export(string path)
    {
        var output = PrepareForWrite();
        if (path == FilePath && !File.Exists(path + ".bak"))
            File.Copy(path, path + ".bak");
        File.WriteAllBytes(path, output);
    }
}
