using ContainerServices.Core;
using Microsoft.Extensions.Logging;

namespace ContainerServices.Demo;

public static class MauiProgram
{
	/// <summary>
	/// The services this application would talk to, and the ports their containers publish.
	/// </summary>
	/// <remarks>
	/// These match <c>docker-compose.yml</c> beside this pattern's README. <b>No container is
	/// started here and nothing is called</b> — the demonstration resolves addresses and shows
	/// them.
	/// </remarks>
	private static readonly ContainerService[] Services =
	[
		new("orders", 5001),
		new("catalogue", 5002),
	];

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

		builder.Services.AddSingleton<IRuntimePlatform, MauiRuntimePlatform>();
		builder.Services.AddSingleton(provider =>
			new ServiceCatalogueViewModel(provider.GetRequiredService<IRuntimePlatform>(), Services));

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	/// <summary>
	/// Whether the self-signed development certificate may be accepted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>#if DEBUG</c>, so a release build cannot enable it. <b>This is not the safeguard.</b> The
	/// safeguard is <see cref="DevelopmentCertificatePolicy"/>, which requires the certificate's
	/// issuer to be the development certificate's as well — enabling the bypass is never
	/// sufficient on its own.
	/// </para>
	/// <para>
	/// Kept here, unused by the screen, because the composition root is where this decision belongs
	/// and a reader looking for it should find it in one place. A client that called a containerized
	/// service over HTTPS would pass <see cref="DevelopmentCertificatePolicy.IsAcceptable"/> to an
	/// <c>HttpClientHandler.ServerCertificateCustomValidationCallback</c>, with this flag.
	/// </para>
	/// </remarks>
	public static bool DevelopmentCertificateBypassEnabled =>
#if DEBUG
		true;
#else
		false;
#endif
}
