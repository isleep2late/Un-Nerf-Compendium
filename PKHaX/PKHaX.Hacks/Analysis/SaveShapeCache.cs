using System;
using System.IO;

namespace PKHaX.Hacks.Analysis;

public static class SaveShapeCache
{
    public const string FileName = "shape.json";

    public static string PathFor(string root, string imageKey) => Path.Combine(root, "pkhax", "hacks", imageKey, FileName);

    public static SaveShape? Load(string root, string imageKey) => Load(root, imageKey, SaveShape.CurrentVersion);

    public static SaveShape? Load(string root, string imageKey, int? wantedVersion)
    {
        var path = PathFor(root, imageKey);
        try
        {
            return File.Exists(path) ? SaveShape.FromJson(File.ReadAllText(path), imageKey, wantedVersion) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string root, SaveShape shape)
    {
        var path = PathFor(root, shape.RomSha1);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, shape.ToJson());
        File.Move(temp, path, overwrite: true);
    }

    public static SaveShape ReadOrBuild(string root, ReadOnlyMemory<byte> rom) => ReadOrBuild(root, rom, out _);

    public static SaveShape ReadOrBuild(string root, ReadOnlyMemory<byte> rom, out bool built)
    {
        var key = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(rom.Span));
        if (Load(root, key) is { } cached)
        {
            built = false;
            return cached;
        }
        built = true;
        var shape = Gen3ShapeFinder.Derive(rom);
        try
        {
            Save(root, shape);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return shape with { Notes = [.. shape.Notes, $"the shape cache could not be written ({ex.Message})"] };
        }
        return shape;
    }
}
