using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Retry.Core;

/// <summary>One attempt, and what came of it.</summary>
public sealed record AttemptRecord(int Attempt, string Outcome);

/// <summary>
/// Runs an operation through the retry executor and shows every attempt.
/// </summary>
/// <remarks>
/// A retry is invisible when it works, which is why a user reports "it was slow" rather than
/// "it failed twice". Showing the attempts makes the policy inspectable.
/// </remarks>
public sealed partial class FlakyOperationViewModel : ObservableObject
{
    private readonly RetryExecutor _executor;
    private readonly Func<int, HttpStatusCode?> _outcomeForAttempt;
    private int _attempts;

    /// <param name="outcomeForAttempt">
    /// What the service returns on a given attempt: a status for a failure, or null for success.
    /// </param>
    public FlakyOperationViewModel(RetryExecutor executor, Func<int, HttpStatusCode?> outcomeForAttempt)
    {
        _executor = executor;
        _outcomeForAttempt = outcomeForAttempt;
    }

    public ObservableCollection<AttemptRecord> Attempts { get; } = [];

    [ObservableProperty]
    private string _status = "Not run.";

    [RelayCommand]
    private async Task RunAsync(CancellationToken cancellationToken)
    {
        Attempts.Clear();
        _attempts = 0;

        try
        {
            var result = await _executor.ExecuteIdempotentAsync(
                Attempt,
                failure => failure is ServiceFailureException service && TransientFailure.IsTransient(service.Status),
                cancellationToken);

            Status = result;
        }
        catch (ServiceFailureException failure)
        {
            Status = $"Gave up after {_attempts} attempt(s). {failure.Message}";
        }
    }

    private Task<string> Attempt(CancellationToken cancellationToken)
    {
        _attempts++;
        var status = _outcomeForAttempt(_attempts);

        if (status is null)
        {
            Attempts.Add(new AttemptRecord(_attempts, "Succeeded"));
            return Task.FromResult($"Succeeded on attempt {_attempts}.");
        }

        var retryable = TransientFailure.IsTransient(status.Value);
        Attempts.Add(new AttemptRecord(
            _attempts,
            $"Failed {(int)status.Value} — {(retryable ? "will retry" : "not retryable, giving up")}"));

        throw new ServiceFailureException(status.Value);
    }
}
