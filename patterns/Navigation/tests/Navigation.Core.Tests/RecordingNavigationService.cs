using Navigation.Core;

namespace Navigation.Core.Tests;

/// <summary>
/// One navigation that was asked for.
/// </summary>
internal sealed record NavigationRequest(string Route, IReadOnlyDictionary<string, object>? Parameters)
{
    /// <summary>The route used for a request to go back.</summary>
    public const string BackRoute = "..";
}

/// <summary>
/// An <see cref="INavigationService"/> that records what was asked of it.
/// </summary>
/// <remarks>
/// It records and does not assert. A fake that asserts internally reports a failure from its own
/// stack frame rather than from the test, and cannot be reused by a test that expects something
/// different.
/// </remarks>
internal sealed class RecordingNavigationService : INavigationService
{
    private readonly List<NavigationRequest> _requests = [];

    public IReadOnlyList<NavigationRequest> Requests => _requests;

    public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
    {
        _requests.Add(new NavigationRequest(route, parameters));
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        _requests.Add(new NavigationRequest(NavigationRequest.BackRoute, null));
        return Task.CompletedTask;
    }
}
