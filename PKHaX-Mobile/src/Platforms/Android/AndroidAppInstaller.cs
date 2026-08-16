using Android.Content;

using PKHaX.Mobile.Services;
using AApplication = Android.App.Application;
using AUri = Android.Net.Uri;

namespace PKHaX.Mobile.Platforms.Android;

/// <summary>
/// Downloads the new APK and hands it to the package installer. Because the APK is signed with the same key
/// and carries the same applicationId, Android performs an IN-PLACE UPGRADE — the existing app is replaced,
/// its data is kept, and no uninstall happens. The user taps "Update" once (an OS requirement).
/// </summary>
public sealed class AndroidAppInstaller : IAppInstaller
{
	public async Task InstallAsync(string url, IProgress<double>? progress, CancellationToken ct)
	{
		var ctx = AApplication.Context;
		var dir = ctx.GetExternalFilesDir(null)?.AbsolutePath ?? Path.GetTempPath();
		var apk = Path.Combine(dir, "pkhax-update.apk");

		using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
		using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
		{
			resp.EnsureSuccessStatusCode();
			var total = resp.Content.Headers.ContentLength ?? 0L;
			await using var src = await resp.Content.ReadAsStreamAsync(ct);
			await using var dst = File.Create(apk);
			var buffer = new byte[81920];
			long read = 0;
			int n;
			while ((n = await src.ReadAsync(buffer, ct)) > 0)
			{
				await dst.WriteAsync(buffer.AsMemory(0, n), ct);
				read += n;
				if (total > 0) progress?.Report((double)read / total);
			}
		}

		var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
			ctx, $"{ctx.PackageName}.fileprovider", new Java.IO.File(apk));
		var intent = new Intent(Intent.ActionView);
		intent.SetDataAndType(uri, "application/vnd.android.package-archive");
		intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
		ctx.StartActivity(intent);
	}
}
