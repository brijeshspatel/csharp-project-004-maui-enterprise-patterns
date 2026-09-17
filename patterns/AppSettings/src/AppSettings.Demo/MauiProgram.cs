using AppSettings.Core;
using Microsoft.Extensions.Logging;

namespace AppSettings.Demo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// The platform store, and the adapter that keeps it out of AppSettings.Core.
		builder.Services.AddSingleton(Preferences.Default);
		builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();

		// Settings are shared: one instance reading one store.
		builder.Services.AddSingleton<ApplicationSettings>();

		// The screen is transient, so reopening it re-reads the store rather than showing what was
		// loaded the first time.
		builder.Services.AddTransient<SettingsViewModel>();
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
