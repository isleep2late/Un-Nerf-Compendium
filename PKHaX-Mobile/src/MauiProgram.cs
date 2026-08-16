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

		builder.Services.AddTransient<Views.MainPage>();
		builder.Services.AddTransient<Views.BoxPage>();
		builder.Services.AddTransient<Views.EntityEditorPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
