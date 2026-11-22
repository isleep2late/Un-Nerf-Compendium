using System;
using System.IO;
using System.Linq;
using Xunit;

namespace PKHaX.Hacks.Tests;

public class HookAnchorTests
{
    private static readonly string[][] Rows = File.ReadAllLines(Path.Combine(SourcePaths.PKHaXRoot, "PKHAX_HOOKS.txt"))
        .Where(line => line.Length != 0)
        .Select(line => line.Split('|'))
        .ToArray();

    [Fact]
    public void HeaderAndShape()
    {
        Assert.Equal(["hook", "file", "symbol", "token"], Rows[0]);
        Assert.All(Rows, row => Assert.Equal(4, row.Length));
    }

    [Fact]
    public void EveryHookIsListed()
    {
        var hooks = Rows.Skip(1).Select(r => r[0]).Distinct().Order().ToArray();
        Assert.Equal(["H1", "H2", "H3", "H4", "H5", "H6"], hooks);
    }

    [Fact]
    public void TheMoveHookCoversBothLookupsAndItsScope()
    {
        var moveRows = Rows.Skip(1).Where(r => r[0] == "H6").Select(r => r[2]).Order().ToArray();
        Assert.Equal(["MoveInfo.GetPP", "MoveInfo.GetType", "MoveOverride.Use"], moveRows);
    }

    [Fact]
    public void EveryTokenIsStillInItsFile()
    {
        var missing = Rows.Skip(1)
            .Where(r => !File.Exists(Path.Combine(SourcePaths.PKHaXRoot, r[1]))
                     || !File.ReadAllText(Path.Combine(SourcePaths.PKHaXRoot, r[1])).Contains(r[3], StringComparison.Ordinal))
            .Select(r => $"{r[0]} {r[1]} {r[2]}")
            .ToArray();
        Assert.Empty(missing);
    }

    [Fact]
    public void EverySymbolNameIsInItsFile()
    {
        var missing = Rows.Skip(1)
            .Where(r => !File.ReadAllText(Path.Combine(SourcePaths.PKHaXRoot, r[1])).Contains(SimpleName(r[2]), StringComparison.Ordinal))
            .Select(r => $"{r[0]} {r[1]} {r[2]}")
            .ToArray();
        Assert.Empty(missing);
    }

    private static string SimpleName(string symbol)
    {
        var name = symbol.Split('(')[0];
        return name[(name.LastIndexOf('.') + 1)..];
    }
}
