using Navigation.Core;

namespace Navigation.Demo;

/// <summary>
/// The Shell half of the navigation seam, and the only class in this pattern that knows Shell
/// exists.
/// </summary>
/// <remarks>
/// <para>
/// Parameters are passed as a <see cref="ShellNavigationQueryParameters"/> rather than as an
/// <c>IDictionary&lt;string, object&gt;</c>, and the difference is not cosmetic. Microsoft
/// documents that data passed as a dictionary "is retained in memory for the lifetime of the page,
/// and isn't released until the page is removed from the navigation stack" — and that it is
/// delivered <em>again</em> when a later page navigates back to it.
/// </para>
/// <para>
/// An order identifier is single-use navigation data. Receiving it a second time on the way back
/// would re-run whatever the receiving view model does with it.
/// <see cref="ShellNavigationQueryParameters"/> is cleared after navigation, which is the behaviour
/// wanted here.
/// </para>
/// </remarks>
internal sealed class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return Shell.Current.GoToAsync(route);
        }

        var single = new ShellNavigationQueryParameters();
        foreach (var pair in parameters)
        {
            single.Add(pair.Key, pair.Value);
        }

        return Shell.Current.GoToAsync(route, single);
    }

    // ".." is Shell's own syntax for backwards navigation. Tab.Stack is read-only, so this is the
    // supported way to go back rather than a shortcut.
    public Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
