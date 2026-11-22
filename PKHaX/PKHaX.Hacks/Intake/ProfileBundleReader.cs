using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using PKHaX.Hacks.Analysis;
using PKHaX.Hacks.Profiles;

namespace PKHaX.Hacks.Intake;

public sealed record BundleRead(PkhaxProfile? Profile, HackData? Data, string? Refusal, IReadOnlyList<string> Members)
{
    public bool Ok => Profile is not null && Data is not null;

    public static BundleRead Refused(string why, IReadOnlyList<string>? members = null) => new(null, null, why, members ?? []);
}

public static class ProfileBundleReader
{
    public const string Extension = ".is2lprofile";
    public const string PkhaxMember = "pkhax-profile.json";
    public const string DerivedPrefix = "derived/";
    public const int MaxMembers = 64;
    public const long MaxBundleBytes = 64L * 1024 * 1024;
    public const long MaxMemberBytes = ProfileStore.MaxProfileBytes;
    public const long MaxInflatedBytes = 48L * 1024 * 1024;
    public const string Source = "isleep2late-Files/src/Services/HackImport/Export/ProfileBundle.cs: a zip holding bundle.json, profile.json, pkhax-profile.json, report.json, derived/*, game-profile.schema.json and PROVENANCE.txt";

    public static BundleRead Read(ReadOnlyMemory<byte> zip)
    {
        if (zip.Length > MaxBundleBytes)
            return BundleRead.Refused($"the bundle is {zip.Length:N0} bytes, above the {MaxBundleBytes:N0} bytes PKHaX reads");
        try
        {
            using var stream = new MemoryStream(zip.ToArray(), writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entries = archive.Entries;
            var names = entries.Select(e => e.FullName).ToList();
            if (entries.Count > MaxMembers)
                return BundleRead.Refused($"the bundle has {entries.Count:N0} members, more than the {MaxMembers} an .is2lprofile holds, so nothing in it is read", names);
            var wanted = entries.Where(e => e.FullName == PkhaxMember || (e.FullName.StartsWith(DerivedPrefix, StringComparison.Ordinal) && e.FullName.EndsWith(".json", StringComparison.Ordinal))).ToList();
            if (wanted.FirstOrDefault(e => e.Length > MaxMemberBytes || e.Length < 0) is { } big)
                return BundleRead.Refused($"{big.FullName} states {big.Length:N0} bytes, above the {MaxMemberBytes:N0} bytes a member may hold, so nothing in the bundle is read", names);
            if (wanted.Sum(e => e.Length) > MaxInflatedBytes)
                return BundleRead.Refused($"the members PKHaX reads state {wanted.Sum(e => e.Length):N0} bytes in all, above {MaxInflatedBytes:N0}, so nothing in the bundle is read", names);
            if (entries.Count(e => e.FullName == PkhaxMember) != 1)
                return BundleRead.Refused($"the bundle does not hold exactly one {PkhaxMember}", names);
            var total = 0L;
            string? Inflate(ZipArchiveEntry entry, out string? why)
            {
                why = null;
                using var s = entry.Open();
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                while (true)
                {
                    var n = s.Read(chunk, 0, chunk.Length);
                    if (n == 0)
                        break;
                    buffer.Write(chunk, 0, n);
                    total += n;
                    if (buffer.Length > MaxMemberBytes || buffer.Length > entry.Length || total > MaxInflatedBytes)
                    {
                        why = $"{entry.FullName} inflates past what its header states or past the cap, so the bundle is refused";
                        return null;
                    }
                }
                return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            }
            var profileEntry = entries.First(e => e.FullName == PkhaxMember);
            if (Inflate(profileEntry, out var profileWhy) is not { } profileText)
                return BundleRead.Refused(profileWhy ?? "the profile member did not read", names);
            var loaded = PkhaxProfileReader.Load(profileText);
            if (loaded.Profile is not { } profile)
                return BundleRead.Refused($"{PkhaxMember} does not load: {loaded.Refusal}", names);
            var key = KeyOf(profile);
            if (key is null)
                return new BundleRead(profile, null, "the profile names no ROM image (match sha1 or data.romSha1), so its tables cannot be keyed", names);
            if (Embedded(profile, key) is { } embedded)
                return new BundleRead(profile, embedded, null, names);
            foreach (var entry in wanted.Where(e => e.FullName != PkhaxMember).OrderBy(e => e.FullName, StringComparer.Ordinal))
            {
                if (Inflate(entry, out var why) is not { } text)
                    return BundleRead.Refused(why ?? $"{entry.FullName} did not read", names);
                if (HackDataCache.FromJson(text, key) is { } data)
                    return new BundleRead(profile, data, null, names);
            }
            return new BundleRead(profile, null, $"the bundle holds no {HackData.Schema} tables for {key} (neither data.embedded in {PkhaxMember} nor a {DerivedPrefix}*.json member)", names);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            return BundleRead.Refused($"the file is not a readable zip ({ex.Message})");
        }
    }

    public static string? KeyOf(PkhaxProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (HackKitStore.IsKey(profile.DataRomSha1))
            return profile.DataRomSha1;
        return profile.Match.Select(m => m.Sha1).FirstOrDefault(HackKitStore.IsKey);
    }

    public static HackData? Embedded(PkhaxProfile profile, string key)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Data?["embedded"] is JsonObject embedded ? HackDataCache.FromJson(embedded.ToJsonString(), key) : null;
    }
}
