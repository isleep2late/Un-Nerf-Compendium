using Android.Content;
using PKHaX.Mobile.Services;
using AApplication = Android.App.Application;
using Uri = Android.Net.Uri;

namespace PKHaX.Mobile.Platforms.Android;

/// <summary>
/// Reads and writes a save at an arbitrary location via the Storage Access Framework directly, so PKHaX
/// edits the emulator's save (Delta, Skyline, a Documents folder) IN PLACE. MAUI's FilePicker copies the
/// pick into a cache dir and hides the original content:// uri, which would make save-back write to the
/// wrong file — so this uses ACTION_OPEN_DOCUMENT and keeps a persistable read/write grant on the real uri.
/// </summary>
public sealed class AndroidSaveFileGateway : ISaveFileGateway
{
	public async Task<PickedSave?> PickSaveAsync()
	{
		var intent = new Intent(Intent.ActionOpenDocument);
		intent.AddCategory(Intent.CategoryOpenable);
		intent.SetType("*/*");
		intent.PutExtra(Intent.ExtraAllowMultiple, false);
		intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission |
			ActivityFlags.GrantPersistableUriPermission);

		var uri = await SafBridge.PickAsync(intent);
		if (uri is null)
			return null;

		TryPersistPermission(uri);

		var resolver = AApplication.Context.ContentResolver
			?? throw new InvalidOperationException("No ContentResolver.");
		using var input = resolver.OpenInputStream(uri)
			?? throw new InvalidOperationException("Could not open the save for reading.");
		using var ms = new MemoryStream();
		await input.CopyToAsync(ms);

		return new PickedSave(ms.ToArray(), new SaveFileHandle(DisplayNameOf(uri), uri.ToString()!));
	}

	public Task WriteSaveAsync(SaveFileHandle handle, byte[] data)
	{
		var resolver = AApplication.Context.ContentResolver
			?? throw new InvalidOperationException("No ContentResolver.");
		var uri = Uri.Parse(handle.PlatformToken)
			?? throw new InvalidOperationException("Bad save location.");

		// "rwt" truncates then writes — a clean in-place overwrite of the original file.
		using var pfd = resolver.OpenFileDescriptor(uri, "rwt")
			?? throw new InvalidOperationException("Could not open the save for writing.");
		using var output = new Java.IO.FileOutputStream(pfd.FileDescriptor);
		output.Write(data);
		output.Flush();
		return Task.CompletedTask;
	}

	private static void TryPersistPermission(Uri uri)
	{
		try
		{
			const ActivityFlags flags = ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission;
			AApplication.Context.ContentResolver?.TakePersistableUriPermission(uri, flags);
		}
		catch
		{
			// Not every provider supports persistable grants; the in-session grant still allows this write.
		}
	}

	private static string DisplayNameOf(Uri uri)
	{
		try
		{
			var resolver = AApplication.Context.ContentResolver;
			using var cursor = resolver?.Query(uri, null, null, null, null);
			if (cursor is not null && cursor.MoveToFirst())
			{
				int i = cursor.GetColumnIndex(global::Android.Provider.IOpenableColumns.DisplayName);
				if (i >= 0) return cursor.GetString(i) ?? "save";
			}
		}
		catch { /* fall through */ }
		return uri.LastPathSegment ?? "save";
	}
}
