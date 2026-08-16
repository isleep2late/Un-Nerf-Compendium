using Android.App;
using Android.Content;
using Uri = Android.Net.Uri;

namespace PKHaX.Mobile.Platforms.Android;

/// <summary>
/// Bridges a Storage Access Framework intent to an awaitable Task. MainActivity forwards its
/// OnActivityResult here so the gateway can `await` the picked document uri.
/// </summary>
public static class SafBridge
{
	private const int RequestCode = 0x5A4E; // "ZN"
	private static TaskCompletionSource<Uri?>? pending;

	public static Task<Uri?> PickAsync(Intent intent)
	{
		var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity
			?? throw new InvalidOperationException("No current activity.");

		pending?.TrySetResult(null);
		pending = new TaskCompletionSource<Uri?>();
		activity.StartActivityForResult(intent, RequestCode);
		return pending.Task;
	}

	public static bool Handle(int requestCode, Result resultCode, Intent? data)
	{
		if (requestCode != RequestCode || pending is null)
			return false;

		var tcs = pending;
		pending = null;
		tcs.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
		return true;
	}
}
