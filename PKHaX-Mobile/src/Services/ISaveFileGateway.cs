namespace PKHaX.Mobile.Services;

/// <summary>
/// A location a save file was opened from, so it can be written straight back in place. The payload is
/// platform-specific: on Android it is a content:// SAF uri string, on iOS a security-scoped bookmark
/// (base64). The app never copies saves into its sandbox — it edits the emulator's file where it lives.
/// </summary>
public readonly record struct SaveFileHandle(string DisplayName, string PlatformToken);

public readonly record struct PickedSave(byte[] Bytes, SaveFileHandle Handle);

/// <summary>Platform bridge for reading/writing a save file at an arbitrary user-chosen location.</summary>
public interface ISaveFileGateway
{
	/// <summary>Prompts the user to pick a save file. Returns null if they cancel.</summary>
	Task<PickedSave?> PickSaveAsync();

	/// <summary>Writes bytes back to the exact location a handle refers to (overwrites in place).</summary>
	Task WriteSaveAsync(SaveFileHandle handle, byte[] data);
}

/// <summary>Resolves the concrete gateway at startup, keeping MauiProgram free of #if noise.</summary>
public static class SaveFileGatewayFactory
{
	public static ISaveFileGateway Create() =>
#if ANDROID
		new Platforms.Android.AndroidSaveFileGateway();
#elif IOS
		new Platforms.iOS.iOSSaveFileGateway();
#else
		throw new PlatformNotSupportedException("PKHaX Mobile targets Android and iOS only.");
#endif
}
