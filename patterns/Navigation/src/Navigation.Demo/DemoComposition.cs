using Navigation.Core;

namespace Navigation.Demo;

/// <summary>
/// Where this demonstration is wired together.
/// </summary>
/// <remarks>
/// A single place that knows both halves, so no page or view model has to. In an application this
/// would be the dependency injection container; a static class is enough for one screen pair and
/// keeps the pattern, rather than the wiring, in view.
/// </remarks>
internal static class DemoComposition
{
    /// <summary>The one Shell-backed navigation service the pages share.</summary>
    public static INavigationService NavigationService { get; } = new ShellNavigationService();

    /// <summary>
    /// The orders each screen loads for itself, as separate queries would return.
    /// </summary>
    public static IReadOnlyList<OrderSummary> Orders() =>
    [
        new(1, "ACME-01", 100m, OrderState.Outstanding),
        new(2, "ACME-02", 250m, OrderState.Cancelled),
        new(3, "GLOBEX-01", 400m, OrderState.Outstanding),
    ];
}
