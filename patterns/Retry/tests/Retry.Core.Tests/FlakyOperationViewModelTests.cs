using System.Net;
using Retry.Core;

namespace Retry.Core.Tests;

/// <summary>
/// The screen that makes a retry visible.
/// </summary>
public class FlakyOperationViewModelTests
{
    private static FlakyOperationViewModel Screen(Func<int, HttpStatusCode?> outcome, int maxAttempts = 3) =>
        new(
            new RetryExecutor(
                new RetryPolicy(maxAttempts, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)),
                new RecordingDelayScheduler(),
                new FixedJitter(1.0)),
            outcome);

    [Fact]
    public async Task AnOperationThatSucceedsFirstTime_RecordsOneAttempt()
    {
        var screen = Screen(_ => null);

        await screen.RunCommand.ExecuteAsync(null);

        var only = Assert.Single(screen.Attempts);
        Assert.Equal(1, only.Attempt);
        Assert.Equal("Succeeded", only.Outcome);
        Assert.Contains("Succeeded on attempt 1", screen.Status);
    }

    [Fact]
    public async Task AnOperationThatRecovers_RecordsEveryAttemptIncludingTheFailures()
    {
        // The point of the screen: a user otherwise reports "it was slow" rather than
        // "it failed twice".
        var screen = Screen(attempt => attempt < 3 ? HttpStatusCode.ServiceUnavailable : null);

        await screen.RunCommand.ExecuteAsync(null);

        Assert.Collection(
            screen.Attempts,
            first => Assert.Contains("will retry", first.Outcome),
            second => Assert.Contains("will retry", second.Outcome),
            third => Assert.Equal("Succeeded", third.Outcome));
        Assert.Contains("Succeeded on attempt 3", screen.Status);
    }

    [Fact]
    public async Task ANonRetryableFailure_StopsAtOneAttempt_AndSaysWhy()
    {
        var screen = Screen(_ => HttpStatusCode.BadRequest);

        await screen.RunCommand.ExecuteAsync(null);

        var only = Assert.Single(screen.Attempts);
        Assert.Contains("not retryable", only.Outcome);
        Assert.Contains("Gave up after 1 attempt", screen.Status);
        Assert.Contains("400", screen.Status);
    }

    [Fact]
    public async Task WhenEveryAttemptFails_ItGivesUpAtThePolicysLimit()
    {
        var screen = Screen(_ => HttpStatusCode.ServiceUnavailable, maxAttempts: 3);

        await screen.RunCommand.ExecuteAsync(null);

        Assert.Equal(3, screen.Attempts.Count);
        Assert.Contains("Gave up after 3 attempt", screen.Status);
    }

    [Fact]
    public async Task RunningAgain_StartsFromAnEmptyList()
    {
        var screen = Screen(attempt => attempt < 2 ? HttpStatusCode.ServiceUnavailable : null);

        await screen.RunCommand.ExecuteAsync(null);
        Assert.Equal(2, screen.Attempts.Count);

        await screen.RunCommand.ExecuteAsync(null);

        Assert.Equal(2, screen.Attempts.Count);
    }
}
