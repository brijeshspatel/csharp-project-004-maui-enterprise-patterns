using CircuitBreaker.Core;

namespace CircuitBreaker.Core.Tests;

/// <summary>A clock the test moves. Nothing here waits.</summary>
internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>A failure the policy counts.</summary>
internal sealed class ServiceDownException() : Exception("The service is down.");

/// <summary>A failure the policy does not count — the request was wrong, not the service.</summary>
internal sealed class BadRequestException() : Exception("The request was malformed.");

public class ServiceCircuitBreakerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OpenFor = TimeSpan.FromSeconds(30);

    private static (ServiceCircuitBreaker Breaker, TestClock Clock) Build(int threshold = 3)
    {
        var clock = new TestClock(Noon);
        return (
            new ServiceCircuitBreaker(
                new CircuitBreakerPolicy(threshold, OpenFor),
                clock,
                failure => failure is ServiceDownException),
            clock);
    }

    private static Task<string> Succeeds(CancellationToken _) => Task.FromResult("ok");

    private static Task<string> Fails(CancellationToken _) => throw new ServiceDownException();

    private static async Task FailTimes(ServiceCircuitBreaker breaker, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await Assert.ThrowsAsync<ServiceDownException>(() => breaker.ExecuteAsync(Fails, CancellationToken.None));
        }
    }

    [Fact]
    public async Task AClosedBreaker_PassesTheCallThroughAndReturnsItsResult()
    {
        var (breaker, _) = Build();

        Assert.Equal("ok", await breaker.ExecuteAsync(Succeeds, CancellationToken.None));
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task FailuresBelowTheThreshold_LeaveItClosed()
    {
        var (breaker, _) = Build(threshold: 3);

        await FailTimes(breaker, 2);

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(2, breaker.ConsecutiveFailures);
    }

    [Fact]
    public async Task ASuccessResetsTheConsecutiveCount()
    {
        // Consecutive, not total. A breaker counting total failures opens eventually on any service
        // that ever fails, however healthy, and the threshold stops meaning anything.
        var (breaker, _) = Build(threshold: 3);

        await FailTimes(breaker, 2);
        await breaker.ExecuteAsync(Succeeds, CancellationToken.None);
        await FailTimes(breaker, 2);

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(2, breaker.ConsecutiveFailures);
    }

    [Fact]
    public async Task ReachingTheThreshold_OpensIt()
    {
        var (breaker, _) = Build(threshold: 3);

        await FailTimes(breaker, 3);

        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.Equal(Noon + OpenFor, breaker.OpensAt);
    }

    [Fact]
    public async Task AnOpenBreaker_RejectsWithoutEnteringTheOperation()
    {
        // The whole value of an open breaker is that the failing service is not called. A test that
        // only asserted the exception would pass against an implementation that called it anyway.
        var (breaker, _) = Build(threshold: 2);
        await FailTimes(breaker, 2);

        var entered = 0;

        var rejection = await Assert.ThrowsAsync<CircuitOpenException>(
            () => breaker.ExecuteAsync(_ => { entered++; return Task.FromResult("ok"); }, CancellationToken.None));

        Assert.Equal(0, entered);
        Assert.Equal(Noon + OpenFor, rejection.RetryAfter);
    }

    [Fact]
    public async Task AtExactlyTheBoundary_ATrialIsAdmitted_AndOneTickBeforeItIsNot()
    {
        var (breaker, clock) = Build(threshold: 2);
        await FailTimes(breaker, 2);

        clock.Advance(OpenFor - TimeSpan.FromTicks(1));
        Assert.Equal(CircuitState.Open, breaker.State);

        clock.Advance(TimeSpan.FromTicks(1));
        Assert.Equal(CircuitState.HalfOpen, breaker.State);
    }

    [Fact]
    public async Task ATrialThatSucceeds_ClosesTheBreakerAndResetsTheCount()
    {
        var (breaker, clock) = Build(threshold: 2);
        await FailTimes(breaker, 2);
        clock.Advance(OpenFor);

        Assert.Equal("ok", await breaker.ExecuteAsync(Succeeds, CancellationToken.None));

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(0, breaker.ConsecutiveFailures);
    }

    [Fact]
    public async Task ATrialThatFails_ReopensFromNow()
    {
        var (breaker, clock) = Build(threshold: 2);
        await FailTimes(breaker, 2);
        clock.Advance(OpenFor);

        await Assert.ThrowsAsync<ServiceDownException>(() => breaker.ExecuteAsync(Fails, CancellationToken.None));

        Assert.Equal(CircuitState.Open, breaker.State);
        Assert.Equal(Noon + OpenFor + OpenFor, breaker.OpensAt);
    }

    [Fact]
    public async Task WhileATrialIsInFlight_ASecondCallIsRejected()
    {
        // Without this, everything that queued while the breaker was open arrives at once on a
        // service that has only just recovered.
        var (breaker, clock) = Build(threshold: 2);
        await FailTimes(breaker, 2);
        clock.Advance(OpenFor);

        var holdTrialOpen = new TaskCompletionSource<string>();
        var trial = breaker.ExecuteAsync(_ => holdTrialOpen.Task, CancellationToken.None);

        await Assert.ThrowsAsync<CircuitOpenException>(
            () => breaker.ExecuteAsync(Succeeds, CancellationToken.None));

        holdTrialOpen.SetResult("ok");
        Assert.Equal("ok", await trial);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task AFailureThePolicyDoesNotCount_NeverOpensTheBreaker()
    {
        // A 400 means the request was wrong, not that the service is unwell. A breaker that counted
        // it would open because one screen sends a malformed request.
        var (breaker, _) = Build(threshold: 2);

        for (var i = 0; i < 10; i++)
        {
            await Assert.ThrowsAsync<BadRequestException>(
                () => breaker.ExecuteAsync<string>(_ => throw new BadRequestException(), CancellationToken.None));
        }

        Assert.Equal(CircuitState.Closed, breaker.State);
        Assert.Equal(0, breaker.ConsecutiveFailures);
    }

    [Fact]
    public async Task ACancelledOperation_IsNotCountedAsAFailure()
    {
        var (breaker, _) = Build(threshold: 2);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => breaker.ExecuteAsync<string>(_ => throw new OperationCanceledException(), CancellationToken.None));

        Assert.Equal(0, breaker.ConsecutiveFailures);
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task ATrialThatIsCancelled_DoesNotWedgeTheBreakerShut()
    {
        // The flag that admits one trial is cleared in a finally. A cancelled trial goes down
        // neither the success nor the failure path, and a flag left set would make the breaker
        // refuse every call for the rest of the process while reporting itself half-open.
        var (breaker, clock) = Build(threshold: 2);
        await FailTimes(breaker, 2);
        clock.Advance(OpenFor);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => breaker.ExecuteAsync<string>(_ => throw new OperationCanceledException(), CancellationToken.None));

        Assert.Equal("ok", await breaker.ExecuteAsync(Succeeds, CancellationToken.None));
        Assert.Equal(CircuitState.Closed, breaker.State);
    }

    [Fact]
    public async Task ATrialThatFailsWithAnUncountedException_DoesNotWedgeTheBreakerShut()
    {
        var (breaker, clock) = Build(threshold: 2);
        await FailTimes(breaker, 2);
        clock.Advance(OpenFor);

        await Assert.ThrowsAsync<BadRequestException>(
            () => breaker.ExecuteAsync<string>(_ => throw new BadRequestException(), CancellationToken.None));

        Assert.Equal("ok", await breaker.ExecuteAsync(Succeeds, CancellationToken.None));
        Assert.Equal(CircuitState.Closed, breaker.State);
    }
}
