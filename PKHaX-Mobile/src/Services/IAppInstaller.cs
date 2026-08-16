namespace PKHaX.Mobile.Services;

/// <summary>Platform hand-off for an in-place app upgrade.</summary>
public interface IAppInstaller
{
	Task InstallAsync(string url, IProgress<double>? progress, CancellationToken ct);
}

public static class AppInstallerFactory
{
	public static IAppInstaller Create() =>
#if ANDROID
		new Platforms.Android.AndroidAppInstaller();
#elif IOS
		new Platforms.iOS.iOSAppInstaller();
#else
		throw new PlatformNotSupportedException("PKHaX Mobile targets Android and iOS only.");
#endif
}
