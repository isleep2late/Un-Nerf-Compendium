using System.Text.Json;

namespace PKHaX.Mobile.Services;

public sealed record UpdateInfo(int Build, string Version, string Url, string Notes);

/// <summary>
/// Checks the version manifest the Giga buttons publish to hackmons.com/downloads and, when a newer build
/// exists, hands the platform installer a URL. Both platforms then do an IN-PLACE upgrade: same bundle id +
/// same signature means the app is replaced, not reinstalled — icon, settings and saves all stay put. The
/// single confirmation tap is an OS requirement (Apple forbids silent native installs; Android needs
/// device-owner privileges), so it cannot be removed.
/// </summary>
public sealed class UpdateService
{
	private const string ManifestUrl = "https://hackmons.com/downloads/pkhax-latest.json";

	private readonly IAppInstaller installer;
	private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(15) };

	public UpdateService(IAppInstaller installer) => this.installer = installer;

	public int CurrentBuild =>
		int.TryParse(AppInfo.Current.BuildString, out var b) ? b : 0;

	public string CurrentVersion => AppInfo.Current.VersionString;

	/// <summary>Returns the newer build if one is published, else null. Never throws.</summary>
	public async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
	{
		try
		{
			var json = await http.GetStringAsync($"{ManifestUrl}?t={DateTime.UtcNow.Ticks}", ct);
			using var doc = JsonDocument.Parse(json);
			var root = doc.RootElement;

			// Each platform carries its own build number so Android (built locally by the Giga button) and
			// iOS (built in the cloud, arrives later) can advance independently.
			var platform = OperatingSystem.IsAndroid() ? "android" : "ios";
			if (!root.TryGetProperty(platform, out var p))
				return null;
			if (!p.TryGetProperty("build", out var buildProp) || !p.TryGetProperty("url", out var urlProp))
				return null;

			var build = buildProp.GetInt32();
			if (build <= CurrentBuild)
				return null;

			return new UpdateInfo(
				build,
				p.TryGetProperty("version", out var v) ? v.GetString() ?? $"build {build}" : $"build {build}",
				urlProp.GetString() ?? "",
				root.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "");
		}
		catch
		{
			return null;
		}
	}

	/// <summary>Downloads (Android) or hands off (iOS) and launches the in-place upgrade.</summary>
	public Task InstallAsync(UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default) =>
		installer.InstallAsync(info.Url, progress, ct);
}
