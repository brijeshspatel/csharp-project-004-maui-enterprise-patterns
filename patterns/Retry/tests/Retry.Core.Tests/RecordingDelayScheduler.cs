using Retry.Core;

namespace Retry.Core.Tests;

/// <summary>
/// Records what it was asked to wait for, and returns immediately.
/// </summary>
/// <remarks>
/// <b>No test in this project waits on real time.</b> The schedule is asserted from what was
/// requested, which is exact, rather than from elapsed time, which is not.
/// </remarks>
internal sealed class RecordingDelayScheduler : IDelayScheduler
{
    private readonly Action<int, CancellationTokenSource>? _onDelay;
    private readonly CancellationTokenSource? _source;

    public RecordingDelayScheduler()
    {
    }

    /// <summary>
    /// Cancels <paramref name="source"/> from inside the delay before a chosen attempt.
    /// </summary>
    /// <remarks>
    /// This is how a cancellation test controls exactly when cancellation lands, without any
    /// dependence on wall-clock timing.
    /// </remarks>
    public RecordingDelayScheduler(CancellationTokenSource source, Action<int, CancellationTokenSource> onDelay)
    {
        _source = source;
        _onDelay = onDelay;
    }

    public List<TimeSpan> Delays { get; } = [];

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        Delays.Add(delay);

        if (_source is not null && _onDelay is not null)
        {
            _onDelay(Delays.Count, _source);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

/// <summary>A jitter source that always returns the same fraction, so assertions are exact.</summary>
internal sealed class FixedJitter(double fraction) : IJitterSource
{
    public double NextFraction() => fraction;
}
