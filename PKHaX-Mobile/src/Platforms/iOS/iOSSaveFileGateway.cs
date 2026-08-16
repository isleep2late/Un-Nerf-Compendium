using Foundation;
using PKHaX.Mobile.Services;
using UIKit;

namespace PKHaX.Mobile.Platforms.iOS;

/// <summary>
/// Reads and writes a save via the iOS document picker with a security-scoped bookmark, so PKHaX can write
/// the file back to wherever the user picked it (Files app, a Folder location an emulator like Provenance or
/// Delta exposes). The bookmark is what lets us re-open the same location for writing after the pick ends.
/// </summary>
public sealed class iOSSaveFileGateway : ISaveFileGateway
{
	public async Task<PickedSave?> PickSaveAsync()
	{
		var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a save file" });
		if (result is null)
			return null;

		var url = NSUrl.FromFilename(result.FullPath);
		string token = result.FullPath;

		bool scoped = url.StartAccessingSecurityScopedResource();
		try
		{
			var bookmark = url.CreateBookmarkData(NSUrlBookmarkCreationOptions.SuitableForBookmarkFile, null, null, out _);
			if (bookmark is not null)
				token = bookmark.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
		}
		catch { /* fall back to the plain path token */ }
		finally
		{
			if (scoped) url.StopAccessingSecurityScopedResource();
		}

		await using var stream = await result.OpenReadAsync();
		using var ms = new MemoryStream();
		await stream.CopyToAsync(ms);

		return new PickedSave(ms.ToArray(), new SaveFileHandle(result.FileName, token));
	}

	public Task WriteSaveAsync(SaveFileHandle handle, byte[] data)
	{
		var url = ResolveUrl(handle.PlatformToken);
		bool scoped = url.StartAccessingSecurityScopedResource();
		try
		{
			NSData.FromArray(data).Save(url, NSDataWritingOptions.Atomic, out var error);
			if (error is not null)
				throw new InvalidOperationException(error.LocalizedDescription);
		}
		finally
		{
			if (scoped) url.StopAccessingSecurityScopedResource();
		}
		return Task.CompletedTask;
	}

	private static NSUrl ResolveUrl(string token)
	{
		// A base64 bookmark round-trips to the original security-scoped location; a plain path is a fallback.
		var bytes = TryDecode(token);
		if (bytes is not null)
		{
			var data = NSData.FromArray(bytes);
			var url = NSUrl.FromBookmarkData(data, NSUrlBookmarkResolutionOptions.WithoutUI, null, out _, out var error);
			if (error is null && url is not null)
				return url;
		}
		return NSUrl.FromFilename(token);
	}

	private static byte[]? TryDecode(string token)
	{
		try { return Convert.FromBase64String(token); }
		catch { return null; }
	}
}
