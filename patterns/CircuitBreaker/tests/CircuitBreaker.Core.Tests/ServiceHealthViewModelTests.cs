using CircuitBreaker.Core;

namespace CircuitBreaker.Core.Tests;

/// <summary>
/// The screen that makes a breaker's state visible.
/// </summary>
/// <remarks>
/// A breaker is invisible when it works: the user sees a fast failure and concludes the service is
/// broken, rather than that it is deliberately being left alone.
/// </remarks>
public class ServiceHealthViewModelTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OpenFor = TimeSpan.FromSeconds(30);

    private static (ServiceHealthViewModel Screen, TestClock Clock) Build(Func<bool> healthy, int threshold = 2)
    {
        var clock = new TestClock(Noon);
        var breaker = new ServiceCircuitBreaker(
            new CircuitBreakerPolicy(threshold, OpenFor),
            clock,
            failure => failure is ServiceDownException);

        return (
            new ServiceHealthViewModel(
                breaker,
                _ => healthy() ? Task.FromResult("answered") : throw new ServiceDownException()),
            clock);
    }

    [Fact]
    public async Task ASuccessfulCall_ShowsTheResultAndAClosedCircuit()
    {
        var (screen, _) = Build(() => true);

        await screen.CallCommand.ExecuteAsync(null);

        Assert.Equal("answered", screen.Status);
        Assert.Equal(CircuitState.Closed, screen.State);
        Assert.Equal(0, screen.ConsecutiveFailures);
        Assert.Equal(string.Empty, screen.NextAttempt);
    }

    [Fact]
    public async Task AFailedCall_ShowsTheFailureAndCountsIt()
    {
        var (screen, _) = Build(() => false);

        await screen.CallCommand.ExecuteAsync(null);

        Assert.Contains("The call failed", screen.Status);
        Assert.Equal(1, screen.ConsecutiveFailures);
        Assert.Equal(CircuitState.Closed, screen.State);
    }

    [Fact]
    public async Task OnceOpen_TheScreenSaysNothingWasCalled_AndWhenItWillBe()
    {
        // The distinction that matters to a user: this is not the service failing again, it is the
        // service deliberately not being called.
        var (screen, _) = Build(() => false, threshold: 2);

        await screen.CallCommand.ExecuteAsync(null);
        await screen.CallCommand.ExecuteAsync(null);
        await screen.CallCommand.ExecuteAsync(null);

        Assert.Contains("Not called", screen.Status);
        Assert.Contains("circuit is open", screen.Status);
        Assert.Equal(CircuitState.Open, screen.State);
        Assert.NotEqual(string.Empty, screen.NextAttempt);
    }

    [Fact]
    public async Task WhenTheServiceRecovers_TheTrialClosesTheCircuitAndClearsTheNotice()
    {
        var healthy = false;
        var (screen, clock) = Build(() => healthy, threshold: 2);

        await screen.CallCommand.ExecuteAsync(null);
        await screen.CallCommand.ExecuteAsync(null);
        Assert.Equal(CircuitState.Open, screen.State);

        healthy = true;
        clock.Advance(OpenFor);

        await screen.CallCommand.ExecuteAsync(null);

        Assert.Equal("answered", screen.Status);
        Assert.Equal(CircuitState.Closed, screen.State);
        Assert.Equal(0, screen.ConsecutiveFailures);
        Assert.Equal(string.Empty, screen.NextAttempt);
    }
}
