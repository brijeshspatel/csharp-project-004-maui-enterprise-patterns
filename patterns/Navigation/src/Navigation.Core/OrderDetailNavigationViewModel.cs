using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Navigation.Core;

/// <summary>
/// The order detail screen. It is reached by navigation and told which order to show.
/// </summary>
public sealed partial class OrderDetailNavigationViewModel : ObservableObject, INavigationParameterReceiver
{
    private readonly INavigationService _navigation;
    private readonly IReadOnlyList<OrderSummary> _orders;

    /// <summary>
    /// Takes its own orders, as a screen fed by its own query would. No repository abstraction is
    /// introduced here: reaching a remote source is a separate catalogue entry with its own run.
    /// </summary>
    public OrderDetailNavigationViewModel(INavigationService navigation, IReadOnlyList<OrderSummary> orders)
    {
        _navigation = navigation;
        _orders = orders;
    }

    [ObservableProperty]
    private OrderSummary? _order;

    /// <summary>
    /// Set when parameters arrived but no order could be shown, so the screen can say why instead
    /// of appearing empty for no stated reason.
    /// </summary>
    [ObservableProperty]
    private string _problem = string.Empty;

    /// <inheritdoc />
    /// <remarks>
    /// Defensive on purpose. The key may be absent, and the value may be of any type: passed as
    /// single-use object data an <see cref="int"/> arrives as an <see cref="int"/>, but through a
    /// query string every value arrives as a <see cref="string"/>. Both are accepted, so a later
    /// change of navigation style cannot break this silently.
    /// </remarks>
    public void ApplyParameters(IReadOnlyDictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue(OrderRoutes.OrderIdParameter, out var raw))
        {
            Order = null;
            Problem = "No order was specified.";
            return;
        }

        if (!TryReadOrderId(raw, out var orderId))
        {
            Order = null;
            Problem = $"The order identifier was not a number: '{raw}'.";
            return;
        }

        var match = _orders.FirstOrDefault(order => order.Id == orderId);
        if (match is null)
        {
            Order = null;
            Problem = $"Order {orderId} was not found.";
            return;
        }

        Order = match;
        Problem = string.Empty;
    }

    private static bool TryReadOrderId(object raw, out int orderId)
    {
        switch (raw)
        {
            case int value:
                orderId = value;
                return true;
            case string text:
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out orderId);
            default:
                orderId = 0;
                return false;
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();
}
