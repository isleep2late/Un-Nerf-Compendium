using Microsoft.Extensions.Logging;
using PKHaX.Mobile.Services;

namespace PKHaX.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>();

		builder.Services.AddSingleton<SaveManager>();
		builder.Services.AddSingleton<ISaveFileGateway>(SaveFileGatewayFactory.Create());
		builder.Services.AddSingleton<IAppInstaller>(AppInstallerFactory.Create());
		builder.Services.AddSingleton<UpdateService>();
		builder.Services.AddSingleton<GameLists>();

		builder.Services.AddTransient<Views.MainPage>();
		builder.Services.AddTransient<Views.BoxPage>();
		builder.Services.AddTransient<Views.PartyPage>();
		builder.Services.AddTransient<Views.TrainerPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
