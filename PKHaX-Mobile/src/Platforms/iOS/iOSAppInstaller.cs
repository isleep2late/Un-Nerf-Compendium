using Foundation;
using PKHaX.Mobile.Services;
using UIKit;

namespace PKHaX.Mobile.Platforms.iOS;

/// <summary>
/// Opens the ad-hoc `itms-services://` manifest, which iOS installs OVER the existing app. Same bundle id +
/// same provisioning team means an IN-PLACE UPGRADE: the icon stays, saves and settings are kept, and there
/// is no uninstall. iOS shows one confirmation prompt, which Apple does not allow an app to bypass.
/// </summary>
public sealed class iOSAppInstaller : IAppInstaller
{
	public Task InstallAsync(string url, IProgress<double>? progress, CancellationToken ct)
	{
		// url is the https:// location of the manifest.plist the Giga button published.
		var itms = url.StartsWith("itms-services://", StringComparison.OrdinalIgnoreCase)
			? url
			: $"itms-services://?action=download-manifest&url={Uri.EscapeDataString(url)}";

		var nsurl = new NSUrl(itms);
		UIApplication.SharedApplication.OpenUrl(nsurl, new UIApplicationOpenUrlOptions(), null);
		return Task.CompletedTask;
	}
}
