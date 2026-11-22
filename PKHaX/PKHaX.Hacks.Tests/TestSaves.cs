using System;
using System.Buffers.Binary;
using PKHeX.Core;

namespace PKHaX.Hacks.Tests;

internal static class TestSaves
{
    private const int SectorSize = 0x1000;
    private const int MainSectors = 14;
    private const uint SectorSignature = 0x08012025;

    public static byte[] Gen3(GameVersion version)
    {
        var raw = new byte[SaveUtil.SIZE_G3RAW];
        for (int i = 0; i < MainSectors; i++)
        {
            var sector = raw.AsSpan(i * SectorSize, SectorSize);
            BinaryPrimitives.WriteInt16LittleEndian(sector[0xFF4..], (short)i);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[0xFF8..], SectorSignature);
            BinaryPrimitives.WriteUInt32LittleEndian(sector[0xFFC..], 1);
        }
        SAV3 sav = version switch
        {
            GameVersion.E => new SAV3E(raw),
            GameVersion.FR or GameVersion.LG => new SAV3FRLG(raw),
            GameVersion.R or GameVersion.S => new SAV3RS(raw),
            _ => throw new ArgumentOutOfRangeException(nameof(version), version, "A Gen 3 cartridge version."),
        };
        sav.OT = "PKHAX";
        switch (version)
        {
            case GameVersion.E:
                BinaryPrimitives.WriteUInt32LittleEndian(sav.Small[0xAC..], 0x5A0C3E21);
                sav.Small[0x900] = 1;
                break;
            case GameVersion.FR or GameVersion.LG:
                BinaryPrimitives.WriteUInt32LittleEndian(sav.Small[0xAC..], 1);
                break;
        }
        return sav.Write().ToArray();
    }
}
