using System;
using System.Collections.Generic;
using System.Globalization;

namespace PKHaX.Hacks.Analysis;

public sealed class SymFile
{
    public const int BankSize = 0x4000;
    public const int MaxBank = 0x1FF;

    private readonly Dictionary<string, (int Bank, int Address)> labels;

    private SymFile(Dictionary<string, (int Bank, int Address)> labels) => this.labels = labels;

    public int Count => labels.Count;

    public static SymFile Parse(string text)
    {
        var map = new Dictionary<string, (int, int)>(StringComparer.Ordinal);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            var semi = line.IndexOf(';');
            if (semi >= 0)
                line = line[..semi].Trim();
            if (line.Length == 0)
                continue;
            var space = line.IndexOfAny([' ', '\t']);
            if (space <= 0)
                continue;
            var at = line[..space];
            var name = line[(space + 1)..].Trim();
            var colon = at.IndexOf(':');
            if (colon <= 0 || name.Length == 0)
                continue;
            if (!uint.TryParse(at[..colon], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var ubank)
                || !uint.TryParse(at[(colon + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var uaddress)
                || ubank > MaxBank || uaddress > 0xFFFF)
                continue;
            var bank = (int)ubank;
            var address = (int)uaddress;
            map.TryAdd(name, (bank, address));
        }
        return new SymFile(map);
    }

    public (int Bank, int Address)? Label(string name) => labels.TryGetValue(name, out var v) ? v : null;

    public int? RomOffset(string name)
    {
        if (Label(name) is not { } l || l.Address >= 0x8000)
            return null;
        if (l.Bank == 0)
            return l.Address < BankSize ? l.Address : null;
        return l.Address >= BankSize ? l.Bank * BankSize + (l.Address - BankSize) : null;
    }
}
