using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PKHaX.Hacks.Intake;
using PKHaX.Hacks.Profiles;

namespace PKHaX.Linux.Views;

public sealed class DropPanel : UserControl
{
	public const long MaxImageBytes = 1L << 30;

	public const long MaxSymBytes = 8L * 1024 * 1024;

	internal static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#EAF0FA"));
	internal static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#93A1BC"));
	internal static readonly IBrush Header = new SolidColorBrush(Color.Parse("#E4572E"));
	internal static readonly IBrush Card = new SolidColorBrush(Color.Parse("#141D2E"));
	internal static readonly IBrush Well = new SolidColorBrush(Color.Parse("#0B1220"));
	internal static readonly IBrush Good = new SolidColorBrush(Color.Parse("#2FA36B"));
	internal static readonly IBrush Blue = new SolidColorBrush(Color.Parse("#2F6FA3"));
	internal static readonly IBrush Lit = new SolidColorBrush(Color.Parse("#E4B02E"));

	private readonly HackKitStore store;
	private readonly HackIntake intake;
	private readonly Func<string, OpenHint?, Task> openSave;
	private readonly Border target;
	private readonly TextBlock status;
	private readonly StackPanel kits = new() { Spacing = 6 };
	private bool busy;

	public DropPanel(HackKitStore store, Func<string, OpenHint?, Task> openSave)
	{
		this.store = store;
		this.openSave = openSave;
		intake = new HackIntake(store, maxRomBytes: MaxImageBytes);
		status = new TextBlock { Foreground = Ink, FontSize = 12, TextWrapping = TextWrapping.Wrap };
		var browse = MakeButton("Browse...", Blue);
		browse.Click += async (_, _) =>
		{
			try
			{
				await HandleAsync(await MainWindow.PickAsync(this, IntakeText.Target, true));
			}
			catch (Exception ex)
			{
				Say($"Nothing was picked: {ex.Message}");
			}
		};
		target = new Border
		{
			Background = Well,
			BorderBrush = Muted,
			BorderThickness = new Thickness(2),
			CornerRadius = new CornerRadius(10),
			Padding = new Thickness(16),
			MinHeight = 110,
			Child = new StackPanel
			{
				Spacing = 8,
				VerticalAlignment = VerticalAlignment.Center,
				Children =
				{
					new TextBlock { Text = IntakeText.Target, Foreground = Ink, FontSize = 15, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center },
					new TextBlock { Text = "a ROM (GB, GBC, GBA, DS), an IPS, UPS, BPS or xdelta patch, a .pkhaxprofile.json or .is2lprofile, or a save", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center },
					browse,
				},
			},
		};
		var panel = new StackPanel { Spacing = 10, Margin = new Thickness(12) };
		panel.Children.Add(Title(IntakeText.Title));
		panel.Children.Add(target);
		panel.Children.Add(new TextBlock { Text = IntakeText.Stored, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap });
		panel.Children.Add(status);
		panel.Children.Add(Title("STORED HACKS"));
		panel.Children.Add(kits);
		Content = new ScrollViewer { Content = panel };
		RefreshKits();
	}

	internal static TextBlock Title(string text) => new() { Text = text.ToUpperInvariant(), FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Header };

	internal static Button MakeButton(string text, IBrush background) => new() { Content = text, Background = background, Foreground = Brushes.White, CornerRadius = new CornerRadius(8) };

	public void SetHighlight(bool on) => target.BorderBrush = on ? Lit : Muted;

	public void Say(string text) => status.Text = text;

	public async Task HandleAsync(IReadOnlyList<string> paths)
	{
		if (paths.Count == 0)
			return;
		if (busy)
		{
			Say("Still reading the last file; drop it again when this one is done.");
			return;
		}
		busy = true;
		try
		{
			foreach (var path in paths)
				await HandleOneAsync(path);
		}
		catch (Exception ex)
		{
			Say($"The file stopped being read: {ex.Message}");
		}
		finally
		{
			busy = false;
			RefreshKits();
		}
	}

	private async Task HandleOneAsync(string path)
	{
		var name = Path.GetFileName(path);
		DropVerdict verdict;
		long length;
		try
		{
			length = new FileInfo(path).Length;
			verdict = DropClassifier.Classify(ReadHead(path, length), length, name);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Say($"{name} could not be read: {ex.Message}");
			return;
		}
		switch (verdict.Kind)
		{
			case DropKind.Save:
				await openSave(path, null);
				return;
			case DropKind.Unknown:
				Say(verdict.Sentence ?? IntakeText.Unknown(name));
				return;
			case DropKind.CtrImage:
				Say(IntakeText.CannotRead);
				return;
			case DropKind.Patch:
				await FromPatchAsync(path, length);
				return;
			case DropKind.Profile or DropKind.Bundle:
				await FromProfileAsync(path, length, verdict.Kind == DropKind.Bundle ? ProfileBundleReader.MaxBundleBytes : ProfileStore.MaxProfileBytes);
				return;
			default:
				await DeriveAsync(path, length);
				return;
		}
	}

	private static byte[] ReadHead(string path, long length)
	{
		if (length <= DropClassifier.WholeFileBelow)
			return File.ReadAllBytes(path);
		var head = new byte[DropClassifier.HeadBytes];
		using var stream = File.OpenRead(path);
		var read = 0;
		while (read < head.Length)
		{
			var n = stream.Read(head, read, head.Length - read);
			if (n <= 0)
				break;
			read += n;
		}
		return read == head.Length ? head : head[..read];
	}

	private async Task DeriveAsync(string path, long length)
	{
		var name = Path.GetFileName(path);
		if (length > MaxImageBytes)
		{
			Say(IntakeText.TooLarge(length, MaxImageBytes));
			return;
		}
		var progress = new Progress<string>(s => Say($"{name}: {s}"));
		try
		{
			var rom = await Task.Run(() => File.ReadAllBytes(path));
			var sym = SymFor(path);
			Show(await Task.Run(() => intake.Derive(rom, name, sym, progress, path)));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
		{
			Say($"{name} could not be read: {ex.Message}");
		}
	}

	private static string? SymFor(string path)
	{
		var sym = Path.ChangeExtension(path, ".sym");
		try
		{
			return File.Exists(sym) && new FileInfo(sym).Length <= MaxSymBytes ? File.ReadAllText(sym) : null;
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return null;
		}
	}

	private async Task FromProfileAsync(string path, long length, long cap)
	{
		var name = Path.GetFileName(path);
		if (length > cap)
		{
			Say(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{name} is {length:N0} bytes, above the {cap:N0} bytes PKHaX reads for a profile."));
			return;
		}
		try
		{
			var file = await Task.Run(() => File.ReadAllBytes(path));
			Show(await Task.Run(() => intake.FromProfile(file, name)));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Say($"{name} could not be read: {ex.Message}");
		}
	}

	private async Task FromPatchAsync(string path, long length)
	{
		var name = Path.GetFileName(path);
		if (length > MaxImageBytes)
		{
			Say(IntakeText.TooLarge(length, MaxImageBytes));
			return;
		}
		byte[] patch;
		PatchProbe probe;
		var patcher = Isleep2late.Files.Services.Pkhax.PkhaxRomPatcher.Shared;
		try
		{
			patch = await Task.Run(() => File.ReadAllBytes(path));
			probe = patcher.Inspect(patch);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			Say($"{name} could not be read: {ex.Message}");
			return;
		}
		catch (RomPatchRefusedException ex)
		{
			Say($"{name}: the patch does not read: {ex.Message}");
			return;
		}
		if (!probe.Known)
		{
			Say($"{name}: the file is not a patch PKHaX applies (IPS, IPS32, UPS, BPS or xdelta).");
			return;
		}
		var ask = AskFor(probe);
		var basePath = probe.SourceCrc32 is { } crc ? store.FindBase(crc, probe.SourceSize)?.Path : null;
		if (basePath is null)
		{
			Say(ask);
			var picked = await MainWindow.PickAsync(this, ask, false);
			if (picked.Count == 0)
				return;
			basePath = picked[0];
		}
		else
		{
			Say($"{name}: using the clean game remembered at {basePath}.");
		}
		var progress = new Progress<string>(s => Say($"{name}: {s}"));
		try
		{
			var baseInfo = new FileInfo(basePath);
			if (baseInfo.Length > MaxImageBytes)
			{
				Say(IntakeText.TooLarge(baseInfo.Length, MaxImageBytes));
				return;
			}
			var baseRom = await Task.Run(() => File.ReadAllBytes(basePath));
			var chosen = basePath;
			Show(await Task.Run(() => intake.FromPatch(patch, baseRom, patcher, name, chosen, progress)));
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OutOfMemoryException)
		{
			Say($"{Path.GetFileName(basePath)} could not be read: {ex.Message}");
		}
	}

	public static string AskFor(PatchProbe probe)
	{
		ArgumentNullException.ThrowIfNull(probe);
		if (probe.Format is PatchKind.Ips or PatchKind.Ips32)
			return IntakeText.IpsPatch;
		return probe.SourceCrc32 is { } crc && probe.SourceSize is { } size ? IntakeText.Patch(probe.Name, crc, size) : IntakeText.PatchNoCrc(probe.Name);
	}

	private void Show(IntakeResult result)
	{
		var lines = new List<string> { result.Sentence ?? result.Refusal ?? "Nothing was read." };
		if (result.FromStore && result.Ok)
			lines.Add("These tables were already stored on this device.");
		lines.AddRange(result.Notes);
		if (result.StoreProblem is { } problem)
			lines.Add(problem);
		Say(string.Join("\n", lines));
		RefreshKits();
	}

	public void RefreshKits()
	{
		kits.Children.Clear();
		IReadOnlyList<StoredKit> stored;
		try
		{
			stored = store.List();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			kits.Children.Add(new TextBlock { Text = $"The stored hacks could not be listed: {ex.Message}", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
			return;
		}
		if (stored.Count == 0)
		{
			kits.Children.Add(new TextBlock { Text = "None yet. Drop a ROM hack or a patch above.", Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap });
			return;
		}
		foreach (var kit in stored)
			kits.Children.Add(KitRow(kit));
	}

	private Control KitRow(StoredKit kit)
	{
		var stack = new StackPanel { Spacing = 4 };
		stack.Children.Add(new TextBlock { Text = kit.Kit.Summary, Foreground = Ink, FontSize = 12, TextWrapping = TextWrapping.Wrap });
		stack.Children.Add(new TextBlock { Text = $"{kit.Kit.At.LocalDateTime:yyyy-MM-dd HH:mm} · {kit.Kit.Key[..8]}", Foreground = Muted, FontSize = 11 });
		if (kit.Problem is { } problem)
			stack.Children.Add(new TextBlock { Text = problem, Foreground = Lit, FontSize = 11, TextWrapping = TextWrapping.Wrap });
		var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
		if (kit.Usable)
		{
			var open = MakeButton("Open a save for this hack...", Good);
			var key = kit.Kit.Key;
			open.Click += async (_, _) =>
			{
				try
				{
					var picked = await MainWindow.PickAsync(this, $"A save of {kit.Kit.Name}", false);
					if (picked.Count > 0)
						await openSave(picked[0], new OpenHint { KitKey = key, Answered = true });
				}
				catch (Exception ex)
				{
					Say($"No save was opened: {ex.Message}");
				}
			};
			buttons.Children.Add(open);
		}
		var forget = MakeButton("Forget", Blue);
		forget.Click += (_, _) =>
		{
			try
			{
				store.Forget(kit.Kit.Key);
				Say($"{kit.Kit.Name}: its stored tables and pairings were removed from this device.");
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				Say($"{kit.Kit.Name} could not be forgotten: {ex.Message}");
			}
			RefreshKits();
		};
		buttons.Children.Add(forget);
		stack.Children.Add(buttons);
		return new Border { Background = Card, CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = stack };
	}
}
