namespace Navigation.Core;

/// <summary>
/// How a view model asks to move to another screen.
/// </summary>
/// <remarks>
/// <para>
/// This exists so navigation logic can be tested. A view model that calls
/// <c>Shell.Current.GoToAsync</c> cannot be: <c>Shell.Current</c> is <c>null</c> outside a running
/// application, and this project does not reference <c>Microsoft.Maui.Controls</c> at all.
/// </para>
/// <para>
/// The implementation over Shell lives in the demonstration project, which is the only place that
/// knows Shell exists.
/// </para>
/// </remarks>
public interface INavigationService
{
    /// <summary>
    /// Moves to <paramref name="route"/>, optionally carrying <paramref name="parameters"/>.
    /// </summary>
    Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null);

    /// <summary>
    /// Returns to the previous screen.
    /// </summary>
    Task GoBackAsync();
}

/// <summary>
/// How a view model receives the values a navigation carried.
/// </summary>
/// <remarks>
/// The inbound half of the same seam. .NET MAUI's own interface for this is
/// <c>IQueryAttributable</c>, which is a <c>Microsoft.Maui.Controls</c> type and therefore cannot
/// be implemented here. The page implements it and forwards to this.
/// </remarks>
public interface INavigationParameterReceiver
{
    /// <summary>
    /// Applies the values a navigation carried.
    /// </summary>
    /// <remarks>
    /// Nothing about <paramref name="parameters"/> is guaranteed by the compiler. A key may be
    /// absent, and a value may be of any type — with a query string every value arrives as a
    /// <see cref="string"/>. An implementation must survive both.
    /// </remarks>
    void ApplyParameters(IReadOnlyDictionary<string, object> parameters);
}

/// <summary>
/// The route names, declared once.
/// </summary>
/// <remarks>
/// A route name is used where the route is registered, where navigation is requested, and in the
/// tests. As three string literals they can disagree, and nothing would report it until run time.
/// As one constant they cannot.
/// </remarks>
public static class OrderRoutes
{
    /// <summary>The editable order detail screen.</summary>
    public const string OrderDetail = "orderdetail";

    /// <summary>The read-only audit screen, for orders that must not be edited.</summary>
    public const string OrderAudit = "orderaudit";

    /// <summary>The parameter key carrying which order to show.</summary>
    public const string OrderIdParameter = "orderId";
}
