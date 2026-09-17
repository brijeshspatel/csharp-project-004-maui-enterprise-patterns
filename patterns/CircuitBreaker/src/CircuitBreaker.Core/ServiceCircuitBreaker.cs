namespace CircuitBreaker.Core;

/// <summary>
/// Stops calling a service that is failing, and finds out when it has recovered.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the state the Retry pattern refused to hold.</b> A retry tries again within one
/// logical call and remembers nothing; this remembers across calls, which is the whole point.
/// </para>
/// <para>
/// Composed with a retry, the retry goes <b>outside</b> and must decline to retry
/// <see cref="CircuitOpenException"/>. See this pattern's README.
/// </para>
/// </remarks>
public sealed class ServiceCircuitBreaker
{
    private readonly CircuitBreakerPolicy _policy;
    private readonly IClock _clock;
    private readonly Func<Exception, bool> _countsAsFailure;

    private int _consecutiveFailures;
    private DateTimeOffset _openedUntil;
    private bool _trialInFlight;

    /// <param name="countsAsFailure">
    /// Which failures count towards opening. A 400 should not: the service is healthy and the
    /// request was wrong, and a breaker that counts everything will open because one screen sends a
    /// malformed request — taking a working feature down with it.
    /// </param>
    public ServiceCircuitBreaker(CircuitBreakerPolicy policy, IClock clock, Func<Exception, bool> countsAsFailure)
    {
        _policy = policy;
        _clock = clock;
        _countsAsFailure = countsAsFailure;
    }

    /// <summary>What the breaker is doing, as of now.</summary>
    public CircuitState State
    {
        get
        {
            if (_consecutiveFailures < _policy.FailureThreshold)
            {
                return CircuitState.Closed;
            }

            // The boundary is inclusive: at exactly the stored moment, a trial is admitted.
            return _clock.UtcNow >= _openedUntil ? CircuitState.HalfOpen : CircuitState.Open;
        }
    }

    /// <summary>Consecutive failures since the last success.</summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>When the breaker will next admit a trial, while it is open.</summary>
    public DateTimeOffset OpensAt => _openedUntil;

    /// <summary>
    /// Runs <paramref name="operation"/>, unless the breaker is refusing.
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var state = State;

        if (state == CircuitState.Open)
        {
            throw new CircuitOpenException(_openedUntil);
        }

        // Half-open admits ONE call at a time. Without this, everything that queued up while the
        // breaker was open arrives at once on a service that has only just recovered — which is the
        // stampede the breaker exists to prevent, delivered at the worst possible moment.
        var isTrial = state == CircuitState.HalfOpen;
        if (isTrial)
        {
            if (_trialInFlight)
            {
                throw new CircuitOpenException(_openedUntil);
            }

            _trialInFlight = true;
        }

        try
        {
            var result = await operation(cancellationToken);
            _consecutiveFailures = 0;
            return result;
        }
        catch (Exception failure) when (failure is not OperationCanceledException && _countsAsFailure(failure))
        {
            _consecutiveFailures++;

            if (_consecutiveFailures >= _policy.FailureThreshold)
            {
                // Re-opened from now, not from when it first opened. A service that keeps failing
                // its trials is therefore retried on a fixed interval rather than drifting.
                _openedUntil = _clock.UtcNow + _policy.OpenDuration;
            }

            throw;
        }
        finally
        {
            // Cleared unconditionally, and that is not tidiness. A trial that is cancelled, or that
            // fails with something the policy does not count, goes down neither the success nor the
            // failure path — and a flag left set would make the breaker refuse every call for the
            // rest of the process while reporting itself half-open. That is worse than the outage
            // it was protecting against, and it would not show up in a short test.
            if (isTrial)
            {
                _trialInFlight = false;
            }
        }
    }
}
