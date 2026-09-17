using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CircuitBreaker.Core;

/// <summary>
/// Calls a service through the breaker, and shows what the breaker is doing.
/// </summary>
/// <remarks>
/// A breaker is invisible when it works: the user sees a fast failure and assumes the service is
/// broken. Showing the state, the count and the next attempt time turns "it is broken" into "it is
/// being left alone until 14:32".
/// </remarks>
public sealed partial class ServiceHealthViewModel : ObservableObject
{
    private readonly ServiceCircuitBreaker _breaker;
    private readonly Func<CancellationToken, Task<string>> _operation;

    public ServiceHealthViewModel(ServiceCircuitBreaker breaker, Func<CancellationToken, Task<string>> operation)
    {
        _breaker = breaker;
        _operation = operation;
        Refresh();
    }

    [ObservableProperty]
    private string _status = "Not called.";

    [ObservableProperty]
    private CircuitState _state;

    [ObservableProperty]
    private int _consecutiveFailures;

    [ObservableProperty]
    private string _nextAttempt = string.Empty;

    [RelayCommand]
    private async Task CallAsync(CancellationToken cancellationToken)
    {
        try
        {
            Status = await _breaker.ExecuteAsync(_operation, cancellationToken);
        }
        catch (CircuitOpenException rejected)
        {
            // Distinguished from a service failure on purpose: nothing was called, and the user can
            // be told when it will be.
            Status = $"Not called. The circuit is open until {rejected.RetryAfter:HH:mm:ss}.";
        }
        catch (Exception failure)
        {
            Status = $"The call failed: {failure.Message}";
        }
        finally
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        State = _breaker.State;
        ConsecutiveFailures = _breaker.ConsecutiveFailures;
        // The culture is named rather than defaulted. This string is read by a person, so the
        // current culture is the right one — which is the opposite of the rule for a stored value,
        // where Application Settings Management stores UTC for the same reason of not guessing.
        NextAttempt = State == CircuitState.Open
            ? _breaker.OpensAt.ToString("HH:mm:ss", CultureInfo.CurrentCulture)
            : string.Empty;
    }
}
