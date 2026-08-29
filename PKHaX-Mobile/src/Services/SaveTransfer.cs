using PKHeX.Core;

namespace PKHaX.Mobile.Services;

/// <summary>
/// Moves one Pokemon record toward a save that may not speak its format - between the two saves the
/// transfer page holds open, or from a .pk1-.pk9 file into a slot.
///
/// This exists because <c>SaveFile.SetBoxSlotAtIndex</c> and its party twin write the bytes
/// AS THEY STAND: they assume the record already matches <see cref="SaveFile.PKMType"/>, so a PK7
/// dropped raw into a SAV3 is silent garbage, not an error. The sequence here is the one the
/// desktop fork's slot manager runs when a file lands on a slot: convert to the destination's own
/// type, refuse with the converter's own words when it refuses, hold Gen 1/2 language pairs apart
/// - a Japanese Gen 1/2 record in a Western save corrupts the name bytes both ways - and then
/// surface the save's compatibility complaints as WARNINGS rather than refusals, because the
/// person moving the Pokemon can see the warning and decide.
///
/// With illegal mode on (the default - the HaX in the name) the converter is told to allow every
/// conversion it can express at all; with it off, the incompatible ones are refused.
/// </summary>
public static class SaveTransfer
{
	/// <param name="Converted">The record in the destination's own format, ready to set - or null.</param>
	/// <param name="Refusal">Why nothing can be written, in one sentence - or null when Converted is set.</param>
	/// <param name="Warnings">The destination save's compatibility complaints. Never blocking.</param>
	public readonly record struct Outcome(PKM? Converted, string? Refusal, IReadOnlyList<string> Warnings)
	{
		public bool Ok => Converted is not null;
	}

	/// <summary>Converts <paramref name="source"/> for <paramref name="destination"/> without writing anything.</summary>
	public static Outcome Prepare(PKM source, SaveFile destination, bool illegalMode)
	{
		if (source.Species == 0)
			return new Outcome(null, "That slot is empty — there is nothing to move.", []);

		EntityConverter.AllowIncompatibleConversion = illegalMode
			? EntityCompatibilitySetting.AllowIncompatibleAll
			: EntityCompatibilitySetting.DisallowIncompatible;

		var converted = EntityConverter.ConvertToType(source, destination.PKMType, out var result);
		if (converted is null)
			return new Outcome(null, result.GetDisplayString(source, destination.PKMType), []);

		// Gen 1/2 store nicknames and OT names as language-specific character codes; a Japanese
		// record in a Western save (or the reverse) is mojibake in both name fields. The converter
		// does not police this, so it is held apart here the way the desktop slot manager does.
		if (destination is ILangDeviantSave gb && !EntityConverter.IsCompatibleGB(converted, gb.Japanese, converted.Japanese))
			return new Outcome(null, "Gen 1/2 saves keep Japanese and Western text apart — this Pokémon's "
				+ "language does not match the destination save, and its name bytes would corrupt.", []);

		converted.RefreshChecksum();
		return new Outcome(converted, null, destination.EvaluateCompatibility(converted));
	}
}
