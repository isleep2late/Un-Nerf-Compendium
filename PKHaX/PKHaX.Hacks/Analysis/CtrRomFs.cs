using System;
using System.Collections.Generic;
using System.IO;

namespace PKHaX.Hacks.Analysis;

public sealed class CtrRomFs
{
    private readonly string[] roots;

    public IReadOnlyList<string> Roots => roots;

    public CtrRomFs(string baseRomFs, string? overlayRomFs = null)
    {
        if (!Directory.Exists(baseRomFs))
            throw new ArgumentException($"the base romfs folder {baseRomFs} does not exist.");
        if (overlayRomFs is not null && !Directory.Exists(overlayRomFs))
            throw new ArgumentException($"the overlay romfs folder {overlayRomFs} does not exist.");
        roots = overlayRomFs is null ? [baseRomFs] : [overlayRomFs, baseRomFs];
    }

    public static string? RomFsFolderOf(string modFolder)
    {
        var romfs = Path.Combine(modFolder, "romfs");
        return Directory.Exists(romfs) ? romfs : null;
    }

    public bool Has(string path) => Resolve(path) is not null;

    public bool FromOverlay(string path) => roots.Length == 2 && Exists(roots[0], path);

    public string? Resolve(string path)
    {
        foreach (var r in roots)
        {
            if (Exists(r, path))
                return Path.Combine(r, path.Replace('/', Path.DirectorySeparatorChar));
        }
        return null;
    }

    public ReadOnlyMemory<byte>? File(string path)
    {
        if (Resolve(path) is not { } p)
            return null;
        return new ReadOnlyMemory<byte>(System.IO.File.ReadAllBytes(p));
    }

    private static bool Exists(string root, string path)
    {
        var full = Path.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
        var rootFull = Path.GetFullPath(root);
        return full.StartsWith(rootFull, StringComparison.Ordinal) && System.IO.File.Exists(full);
    }
}
