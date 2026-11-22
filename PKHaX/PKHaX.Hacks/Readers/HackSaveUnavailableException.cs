using System;

namespace PKHaX.Hacks.Readers;

public sealed class HackSaveUnavailableException(string profileId, string saveVariant, string message) : Exception(message)
{
    public string ProfileId { get; } = profileId;
    public string SaveVariant { get; } = saveVariant;

    public static HackSaveUnavailableException NoReader(HackSaveContext context) => new(context.ProfileId, context.SaveVariant,
        $"This build has no reader for the {context.SaveVariant} save format of {context.ProfileId}, so the save stays closed.");

    public static HackSaveUnavailableException ShiftNotEmerald(HackSaveContext context, PKHeX.Core.GameVersion found) => new(context.ProfileId, context.SaveVariant,
        $"{context.ProfileId} moves the Battle Frontier fields by {context.FrontierShift}, which only an Emerald save has, but this file is a {found} save, so it stays closed.");

    public static HackSaveUnavailableException NotRead(HackSaveContext context) => new(context.ProfileId, context.SaveVariant,
        $"The {context.SaveVariant} reader did not accept this file as a save of {context.ProfileId}, so the save stays closed.");
}
