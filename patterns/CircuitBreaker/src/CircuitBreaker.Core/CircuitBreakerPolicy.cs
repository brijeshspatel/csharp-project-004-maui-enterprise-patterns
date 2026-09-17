namespace CircuitBreaker.Core;

/// <summary>
/// The time, as a dependency, so a test can move it.
/// </summary>
/// <remarks>
/// A breaker asks "has enough time passed to try again". That is a clock, not a delay — nothing
/// here waits, and no test should either.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>What the breaker is currently doing.</summary>
public enum CircuitState
{
    /// <summary>Calls pass. Consecutive failures are counted.</summary>
    Closed,

    /// <summary>Calls are rejected without being attempted.</summary>
    Open,

    /// <summary>One call at a time is admitted, as a trial.</summary>
    HalfOpen,
}

/// <summary>
/// How many consecutive failures open the breaker, and for how long.
/// </summary>
public sealed record CircuitBreakerPolicy(int FailureThreshold, TimeSpan OpenDuration);

/// <summary>
/// Thrown instead of calling a service the breaker is protecting.
/// </summary>
/// <remarks>
/// <para>
/// A distinct type on purpose. A retry policy composed with this breaker must be able to name it
/// and decline to retry it — otherwise the retry spends its whole schedule against a breaker that is
/// deliberately refusing, and the caller waits out the backoff to be told no.
/// </para>
/// <para>
/// It carries <see cref="RetryAfter"/> so a caller can say something better than "it failed".
/// </para>
/// </remarks>
public sealed class CircuitOpenException(DateTimeOffset retryAfter)
    : Exception($"The circuit is open. The next attempt is allowed at {retryAfter:u}.")
{
    /// <summary>When the breaker will next admit a trial call.</summary>
    public DateTimeOffset RetryAfter { get; } = retryAfter;
}
