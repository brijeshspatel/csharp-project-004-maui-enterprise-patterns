namespace Retry.Core;

/// <summary>
/// Runs an operation, and tries again when the failure is one that might not repeat.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a circuit breaker.</b> It holds a policy, a scheduler and a jitter source, and
/// <b>no failure counter, no history and no open or closed state</b>. Nothing here survives a call.
/// Remembering that a service is failing, and declining to call it for a while, is a separate
/// catalogue entry.
/// </para>
/// </remarks>
public sealed class RetryExecutor
{
    private readonly RetryPolicy _policy;
    private readonly IDelayScheduler _scheduler;
    private readonly IJitterSource _jitter;

    public RetryExecutor(RetryPolicy policy, IDelayScheduler scheduler, IJitterSource jitter)
    {
        _policy = policy;
        _scheduler = scheduler;
        _jitter = jitter;
    }

    /// <summary>
    /// Runs <paramref name="operation"/>, retrying while <paramref name="shouldRetry"/> says the
    /// failure might not repeat.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The name states a precondition the caller must meet.</b> Retrying a read is safe;
    /// retrying a write may happen twice, because an attempt whose response was lost may well have
    /// succeeded. A payment, an order or a message can be duplicated that way.
    /// </para>
    /// <para>
    /// <b>Nothing here checks it.</b> The name is in the caller's own code and in every review of
    /// that code, which a warning in a README is not. That is the whole of what it buys.
    /// </para>
    /// </remarks>
    public async Task<T> ExecuteIdempotentAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<Exception, bool> shouldRetry,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            // Checked before every attempt, including the first. A caller that has already gone
            // does not want the work started, never mind repeated.
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation(cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException
                                            && attempt < _policy.MaxAttempts
                                            && shouldRetry(failure))
            {
                // Reached only when another attempt will follow. The delay therefore never runs
                // after the final failure: waiting out a backoff and then throwing anyway costs the
                // user time for nothing, and the attempt count alone cannot tell the two apart.
                await _scheduler.DelayAsync(_policy.DelayBefore(attempt + 1, _jitter), cancellationToken);
            }
        }
    }
}
