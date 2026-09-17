using CircuitBreaker.Core;
using Microsoft.Extensions.Logging;

namespace CircuitBreaker.Demo;

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

		builder.Services.AddSingleton<IClock, SystemClock>();

		// A breaker is shared state by definition: every caller of the protected service must see
		// the same one, or each holds its own opinion about whether the service is healthy.
		builder.Services.AddSingleton(provider => new ServiceCircuitBreaker(
			new CircuitBreakerPolicy(FailureThreshold: 3, OpenDuration: TimeSpan.FromSeconds(15)),
			provider.GetRequiredService<IClock>(),
			failure => failure is ServiceDownException));

		builder.Services.AddSingleton<FailingService>();

		builder.Services.AddTransient(provider => new ServiceHealthViewModel(
			provider.GetRequiredService<ServiceCircuitBreaker>(),
			provider.GetRequiredService<FailingService>().CallAsync));

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

internal sealed class SystemClock : IClock
{
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>A failure the policy counts.</summary>
public sealed class ServiceDownException() : Exception("The service is down.");

/// <summary>
/// Stands in for a service that is unwell, so the breaker has something to react to.
/// </summary>
/// <remarks>
/// <b>Nothing is called.</b> The outcome is decided here, and no request leaves this machine.
/// Failing while a toggle is set lets a reader drive the breaker through all three states by hand.
/// </remarks>
public sealed class FailingService
{
	public bool IsHealthy { get; set; }

	public Task<string> CallAsync(CancellationToken cancellationToken) =>
		IsHealthy
			? Task.FromResult($"The service answered at {DateTimeOffset.Now:HH:mm:ss}.")
			: throw new ServiceDownException();
}
