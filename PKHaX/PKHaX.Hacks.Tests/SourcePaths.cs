using System.IO;
using System.Runtime.CompilerServices;

namespace PKHaX.Hacks.Tests;

internal static class SourcePaths
{
    public static string PKHaXRoot { get; } = Locate();

    private static string Locate([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
}
