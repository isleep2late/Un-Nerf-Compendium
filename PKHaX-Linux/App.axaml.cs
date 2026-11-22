using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PKHaX.Linux.Views;

namespace PKHaX.Linux;

public partial class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			desktop.MainWindow = new MainWindow(desktop.Args is { Length: > 0 } args ? args[0] : null);
		base.OnFrameworkInitializationCompleted();
	}
}
