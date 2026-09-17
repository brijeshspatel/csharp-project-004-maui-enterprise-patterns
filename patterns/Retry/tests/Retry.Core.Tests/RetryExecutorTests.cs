using System.Net;
using Retry.Core;

namespace Retry.Core.Tests;

public class RetryExecutorTests
{
    private static readonly RetryPolicy ThreeAttempts =
        new(MaxAttempts: 3, BaseDelay: TimeSpan.FromSeconds(1), MaxDelay: TimeSpan.FromSeconds(30));

    private static bool Transient(Exception failure) =>
        failure is ServiceFailureException service && TransientFailure.IsTransient(service.Status);

    private static RetryExecutor Executor(RecordingDelayScheduler scheduler, double jitter = 1.0, RetryPolicy? policy = null) =>
        new(policy ?? ThreeAttempts, scheduler, new FixedJitter(jitter));

    [Fact]
    public async Task AnOperationThatSucceeds_IsAttemptedOnce_AndNothingIsDelayed()
    {
        var scheduler = new RecordingDelayScheduler();
        var attempts = 0;

        var result = await Executor(scheduler).ExecuteIdempotentAsync(
            _ => { attempts++; return Task.FromResult("ok"); },
            Transient,
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(1, attempts);
        Assert.Empty(scheduler.Delays);
    }

    [Fact]
    public async Task ATransientFailureThenSuccess_IsTwoAttemptsAndOneDelay()
    {
        var scheduler = new RecordingDelayScheduler();
        var attempts = 0;

        var result = await Executor(scheduler).ExecuteIdempotentAsync(
            _ =>
            {
                attempts++;
                return attempts == 1
                    ? throw new ServiceFailureException(HttpStatusCode.ServiceUnavailable)
                    : Task.FromResult("ok");
            },
            Transient,
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
        Assert.Single(scheduler.Delays);
    }

    [Fact]
    public async Task WhenEveryAttemptFails_TheLastFailureIsThrown_AndTheDelayNeverFollowsTheLastAttempt()
    {
        // The delay count is the assertion that matters. A loop that delays after the final failure
        // makes exactly the same number of attempts, so counting attempts alone cannot tell the two
        // apart — and the user waits out a backoff before getting the exception anyway.
        var scheduler = new RecordingDelayScheduler();
        var attempts = 0;

        var failure = await Assert.ThrowsAsync<ServiceFailureException>(
            () => Executor(scheduler).ExecuteIdempotentAsync<string>(
                _ =>
                {
                    attempts++;
                    throw new ServiceFailureException(
                        attempts == 3 ? HttpStatusCode.GatewayTimeout : HttpStatusCode.ServiceUnavailable);
                },
                Transient,
                CancellationToken.None));

        Assert.Equal(3, attempts);
        Assert.Equal(2, scheduler.Delays.Count);
        Assert.Equal(HttpStatusCode.GatewayTimeout, failure.Status);
    }

    [Fact]
    public async Task ANonRetryableFailure_IsAttemptedOnceAndRethrownImmediately()
    {
        // The pattern's central rule. A 400 will not succeed on a second attempt, and sending it
        // again costs the user time, the device battery and the server capacity.
        var scheduler = new RecordingDelayScheduler();
        var attempts = 0;

        var failure = await Assert.ThrowsAsync<ServiceFailureException>(
            () => Executor(scheduler).ExecuteIdempotentAsync<string>(
                _ => { attempts++; throw new ServiceFailureException(HttpStatusCode.BadRequest); },
                Transient,
                CancellationToken.None));

        Assert.Equal(1, attempts);
        Assert.Empty(scheduler.Delays);
        Assert.Equal(HttpStatusCode.BadRequest, failure.Status);
    }

    [Fact]
    public async Task TheDelaysGrowExponentially_AndAreCapped()
    {
        var policy = new RetryPolicy(MaxAttempts: 5, BaseDelay: TimeSpan.FromSeconds(1), MaxDelay: TimeSpan.FromSeconds(4));
        var scheduler = new RecordingDelayScheduler();

        await Assert.ThrowsAsync<ServiceFailureException>(
            () => Executor(scheduler, jitter: 1.0, policy: policy).ExecuteIdempotentAsync<string>(
                _ => throw new ServiceFailureException(HttpStatusCode.ServiceUnavailable),
                Transient,
                CancellationToken.None));

        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4)],
            scheduler.Delays);
    }

    [Fact]
    public async Task JitterScalesTheDelay()
    {
        var scheduler = new RecordingDelayScheduler();

        await Assert.ThrowsAsync<ServiceFailureException>(
            () => Executor(scheduler, jitter: 0.5).ExecuteIdempotentAsync<string>(
                _ => throw new ServiceFailureException(HttpStatusCode.ServiceUnavailable),
                Transient,
                CancellationToken.None));

        Assert.Equal([TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)], scheduler.Delays);
    }

    [Fact]
    public async Task AJitterOfZero_ProducesAnImmediateRetry()
    {
        // Legal, and intended: full jitter spans zero to the capped delay. Stated here so the
        // property is explicit rather than discovered by someone whose retry landed in the same
        // millisecond as the failure.
        var scheduler = new RecordingDelayScheduler();

        await Assert.ThrowsAsync<ServiceFailureException>(
            () => Executor(scheduler, jitter: 0.0).ExecuteIdempotentAsync<string>(
                _ => throw new ServiceFailureException(HttpStatusCode.ServiceUnavailable),
                Transient,
                CancellationToken.None));

        Assert.All(scheduler.Delays, delay => Assert.Equal(TimeSpan.Zero, delay));
    }

    [Fact]
    public async Task TwoDifferentJitterSources_ProduceDifferentSchedules()
    {
        // The reason jitter exists. Without it every client that failed together returns together.
        var first = new RecordingDelayScheduler();
        var second = new RecordingDelayScheduler();

        foreach (var (scheduler, jitter) in new[] { (first, 0.25), (second, 0.75) })
        {
            await Assert.ThrowsAsync<ServiceFailureException>(
                () => Executor(scheduler, jitter).ExecuteIdempotentAsync<string>(
                    _ => throw new ServiceFailureException(HttpStatusCode.ServiceUnavailable),
                    Transient,
                    CancellationToken.None));
        }

        Assert.NotEqual(first.Delays, second.Delays);
    }

    [Fact]
    public async Task CancellingDuringADelay_StopsEarly_WithFewerAttemptsThanThePolicyAllows()
    {
        // Cancellation is landed from inside the scheduler, so the test controls exactly when it
        // happens and waits on nothing.
        using var cts = new CancellationTokenSource();
        var scheduler = new RecordingDelayScheduler(cts, (delayNumber, source) =>
        {
            if (delayNumber == 1)
            {
                source.Cancel();
            }
        });

        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Executor(scheduler).ExecuteIdempotentAsync<string>(
                _ => { attempts++; throw new ServiceFailureException(HttpStatusCode.ServiceUnavailable); },
                Transient,
                cts.Token));

        // One attempt, not the three the policy allows.
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ACancelledCaller_StopsTheOperationBeingEnteredAtAll()
    {
        // Checked before the first attempt as well. A caller that has already gone does not want
        // the work started, never mind repeated.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Executor(new RecordingDelayScheduler()).ExecuteIdempotentAsync<string>(
                _ => { attempts++; return Task.FromResult("ok"); },
                Transient,
                cts.Token));

        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task ACancellationRaisedByTheOperation_IsNotTreatedAsARetryableFailure()
    {
        // Otherwise a cancelled operation would be retried, which is the opposite of what the
        // caller asked for.
        var scheduler = new RecordingDelayScheduler();
        var attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Executor(scheduler).ExecuteIdempotentAsync<string>(
                _ => { attempts++; throw new OperationCanceledException(); },
                _ => true,
                CancellationToken.None));

        Assert.Equal(1, attempts);
        Assert.Empty(scheduler.Delays);
    }
}

public class TransientFailureTests
{
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public void AFailureTheServerMayRecoverFrom_IsRetried(HttpStatusCode status) =>
        Assert.True(TransientFailure.IsTransient(status));

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Conflict)]
    public void AFailureAboutTheRequestItself_IsNotRetried(HttpStatusCode status) =>
        Assert.False(TransientFailure.IsTransient(status));
}
