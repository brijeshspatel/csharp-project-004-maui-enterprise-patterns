using System.Net;

namespace Retry.Core;

/// <summary>
/// Waits. Injected so a test never does.
/// </summary>
/// <remarks>
/// A retry with backoff is mostly waiting, so a test that really waits is both slow and unreliable.
/// The same shape as the injected clock in Dependency Injection, for the same reason.
/// </remarks>
public interface IDelayScheduler
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

/// <summary>
/// Supplies the randomness that spreads retries out.
/// </summary>
public interface IJitterSource
{
    /// <summary>A value in the range 0 inclusive to 1 exclusive.</summary>
    double NextFraction();
}

/// <summary>
/// How many times to try, and how long to wait between attempts.
/// </summary>
public sealed record RetryPolicy(int MaxAttempts, TimeSpan BaseDelay, TimeSpan MaxDelay)
{
    /// <summary>
    /// The delay before attempt number <paramref name="nextAttempt"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exponential, capped, and multiplied by the jitter fraction — the form usually called full
    /// jitter.
    /// </para>
    /// <para>
    /// <b>Jitter is not decoration.</b> Every client that failed at the same moment computes the
    /// same schedule without it, so they all come back together and fail together. A jitter of zero
    /// is legal and produces an immediate retry; that is the documented behaviour of this algorithm
    /// rather than an accident.
    /// </para>
    /// </remarks>
    public TimeSpan DelayBefore(int nextAttempt, IJitterSource jitter)
    {
        var exponent = Math.Max(0, nextAttempt - 2);
        var uncapped = BaseDelay * Math.Pow(2, exponent);
        var capped = uncapped > MaxDelay ? MaxDelay : uncapped;

        return capped * jitter.NextFraction();
    }
}

/// <summary>
/// Which failures are worth trying again.
/// </summary>
public static class TransientFailure
{
    /// <summary>
    /// The usual default: a failure the server may recover from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A default the caller may replace, not a law.</b> Three common exceptions:
    /// </para>
    /// <list type="bullet">
    /// <item>A <c>401</c> is retryable <em>after refreshing a token</em>, which is authentication's
    /// business and not a blanket rule.</item>
    /// <item>A <c>409</c> may be retryable after re-reading whatever conflicted.</item>
    /// <item>A <c>429</c> is retryable only when the server's <c>Retry-After</c> is honoured, which
    /// is a different delay from this policy's.</item>
    /// </list>
    /// </remarks>
    public static bool IsTransient(HttpStatusCode status) => status is
        HttpStatusCode.RequestTimeout
        or HttpStatusCode.TooManyRequests
        or HttpStatusCode.InternalServerError
        or HttpStatusCode.BadGateway
        or HttpStatusCode.ServiceUnavailable
        or HttpStatusCode.GatewayTimeout;
}

/// <summary>
/// A failure carrying the status the service returned.
/// </summary>
public sealed class ServiceFailureException(HttpStatusCode status)
    : Exception($"The service answered {(int)status}.")
{
    public HttpStatusCode Status { get; } = status;
}
