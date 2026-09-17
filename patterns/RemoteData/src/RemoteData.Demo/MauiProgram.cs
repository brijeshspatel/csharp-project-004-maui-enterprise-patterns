using Microsoft.Extensions.Logging;
using RemoteData.Core;

namespace RemoteData.Demo;

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

		// One HttpClient for the life of the application, not one per call. A client created per
		// call holds its socket open after disposal and can exhaust the connection pool under load;
		// a single long-lived one never notices DNS changing. IHttpClientFactory solves both, and
		// lives in Microsoft.Extensions.Http, which this repository does not carry -- see the
		// pattern's README. For one base address a singleton is the correct simple answer.
		builder.Services.AddSingleton(_ => new HttpClient(new SampleDataHandler())
		{
			// A host that does not resolve, because nothing here may reach a network. The handler
			// answers before any resolution would be attempted.
			BaseAddress = new Uri("https://orders.invalid/"),
			Timeout = TimeSpan.FromSeconds(10),
		});

		builder.Services.AddSingleton<IOrderCache, InMemoryOrderCache>();
		builder.Services.AddSingleton<IOrderCatalogue, HttpOrderCatalogue>();

		builder.Services.AddTransient<OrderListViewModel>();
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
