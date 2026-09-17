using DependencyInjection.Core;
using Microsoft.Extensions.Logging;

namespace DependencyInjection.Demo;

/// <summary>
/// The composition root, and the only place in this pattern that names a concrete type.
/// </summary>
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

		// Singleton: shared state. Both screens must see one audit log, which is the whole point.
		builder.Services.AddSingleton<IAuditLog, InMemoryAuditLog>();

		// Singleton: stateless. Registered rather than read directly so a test can fix the time.
		builder.Services.AddSingleton<IClock, SystemClock>();

		// Transient: a fresh instance per navigation, as Microsoft's guidance recommends for pages
		// and view models. AddScoped would NOT give that — .NET MAUI creates no scope during
		// navigation, so a scoped registration resolves from the root provider and returns the same
		// instance every time. See this pattern's README for the measured evidence.
		builder.Services.AddTransient<OrderEntryViewModel>();
		builder.Services.AddTransient<AuditTrailViewModel>();
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

/// <summary>
/// The clock the application runs on.
/// </summary>
/// <remarks>
/// <see cref="DateTimeOffset.UtcNow"/>, not <c>Now</c>. <c>Now</c> carries the device's offset, so
/// two devices in different time zones would stamp the same moment differently — and an audit trail
/// is exactly where that matters.
/// </remarks>
internal sealed class SystemClock : IClock
{
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
