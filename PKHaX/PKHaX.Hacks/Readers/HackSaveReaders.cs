using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using PKHeX.Core;

namespace PKHaX.Hacks.Readers;

public static class HackSaveReaders
{
    private static readonly object gate = new();
    private static IHackSaveVariantReader[] variants = [];
    private static readonly HackSaveReader reader = new();

    [ThreadStatic]
    private static bool vanillaOnly;

    public static IReadOnlyList<IHackSaveVariantReader> Variants => Volatile.Read(ref variants);

    public static bool IsRegistered
    {
        get
        {
            lock (SaveUtil.CustomSaveReaders)
                return SaveUtil.CustomSaveReaders.Contains(reader);
        }
    }

    public static void Register()
    {
        lock (SaveUtil.CustomSaveReaders)
        {
            if (!SaveUtil.CustomSaveReaders.Contains(reader))
                SaveUtil.CustomSaveReaders.Add(reader);
        }
    }

    public static void Unregister()
    {
        lock (SaveUtil.CustomSaveReaders)
            SaveUtil.CustomSaveReaders.Remove(reader);
    }

    public static void Add(IHackSaveVariantReader variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        lock (gate)
        {
            if (variants.Any(v => string.Equals(v.VariantId, variant.VariantId, StringComparison.Ordinal)))
                throw new ArgumentException($"A reader for save variant {variant.VariantId} is already registered.", nameof(variant));
            variants = [.. variants, variant];
        }
    }

    public static bool Remove(string variantId)
    {
        lock (gate)
        {
            var kept = variants.Where(v => !string.Equals(v.VariantId, variantId, StringComparison.Ordinal)).ToArray();
            var removed = kept.Length != variants.Length;
            variants = kept;
            return removed;
        }
    }

    public static IHackSaveVariantReader? Find(string variantId) =>
        Volatile.Read(ref variants).FirstOrDefault(v => string.Equals(v.VariantId, variantId, StringComparison.Ordinal));

    public static SaveFile? Open(Memory<byte> data, HackSaveContext? context, string? path = null)
    {
        if (context is null)
            return SaveUtil.TryGetSaveFile(data, out var detected, path) ? detected : null;
        if (context.IsVanillaContainer)
        {
            var vanilla = OpenVanilla(data, path);
            if (context.FrontierShift == 0 || vanilla is null)
                return vanilla;
            if (vanilla is not SAV3E)
                throw HackSaveUnavailableException.ShiftNotEmerald(context, vanilla.Version);
            return new SAV3E(data, context.FrontierShift);
        }
        var variant = Find(context.SaveVariant) ?? throw HackSaveUnavailableException.NoReader(context);
        if (!variant.IsRecognized(data.Length) || !variant.TryRead(data, context, out var result))
            throw HackSaveUnavailableException.NotRead(context);
        return result;
    }

    public static SaveFile? OpenFile(string path, HackSaveContext? context)
    {
        ArgumentNullException.ThrowIfNull(path);
        var result = Open(File.ReadAllBytes(path), context, path);
        if (result is null)
            return null;
        result.Metadata.SetExtraInfo(path);
        if (result.Generation <= 3)
            SaveLanguage.TryRevise(result);
        return result;
    }

    private static SaveFile? OpenVanilla(Memory<byte> data, string? path)
    {
        var outer = vanillaOnly;
        vanillaOnly = true;
        try
        {
            return SaveUtil.TryGetSaveFile(data, out var result, path) ? result : null;
        }
        finally
        {
            vanillaOnly = outer;
        }
    }

    private sealed class HackSaveReader : ISaveReader
    {
        public bool IsRecognized(long dataLength) => !vanillaOnly && Volatile.Read(ref variants).Any(v => v.IsRecognized(dataLength));

        public bool TryRead(Memory<byte> data, [NotNullWhen(true)] out SaveFile? result, string? path = null)
        {
            result = null;
            if (vanillaOnly)
                return false;
            foreach (var variant in Volatile.Read(ref variants))
            {
                if (!variant.IsRecognized(data.Length) || !variant.IsSelfIdentifying(data.Span))
                    continue;
                if (variant.TryRead(data, null, out result))
                    return true;
            }
            return false;
        }
    }
}
