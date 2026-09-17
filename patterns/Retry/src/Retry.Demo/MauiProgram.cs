using System.Net;
using Microsoft.Extensions.Logging;
using Retry.Core;

namespace Retry.Demo;

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

		builder.Services.AddSingleton<IDelayScheduler, TaskDelayScheduler>();
		builder.Services.AddSingleton<IJitterSource, RandomJitter>();

		builder.Services.AddSingleton(provider => new RetryExecutor(
			new RetryPolicy(MaxAttempts: 4, BaseDelay: TimeSpan.FromMilliseconds(400), MaxDelay: TimeSpan.FromSeconds(3)),
			provider.GetRequiredService<IDelayScheduler>(),
			provider.GetRequiredService<IJitterSource>()));

		// A service that fails twice and then works, so the retry is visible. Nothing is called:
		// the outcome is decided here, and no request leaves this machine.
		builder.Services.AddTransient(provider => new FlakyOperationViewModel(
			provider.GetRequiredService<RetryExecutor>(),
			attempt => attempt switch
			{
				1 => HttpStatusCode.ServiceUnavailable,
				2 => HttpStatusCode.GatewayTimeout,
				_ => null,
			}));

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

/// <summary>The real delay, used by the application and by nothing in the tests.</summary>
internal sealed class TaskDelayScheduler : IDelayScheduler
{
	public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
		Task.Delay(delay, cancellationToken);
}

/// <summary>
/// Real randomness, which is exactly what a test cannot have.
/// </summary>
/// <remarks>
/// <c>Random.Shared</c> is thread-safe, so one instance serves the whole application. This is the
/// half of the pattern that must be injected: an assertion about a schedule is impossible while the
/// schedule is random.
/// </remarks>
internal sealed class RandomJitter : IJitterSource
{
	public double NextFraction() => Random.Shared.NextDouble();
}
