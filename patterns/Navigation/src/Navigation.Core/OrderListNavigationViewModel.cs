using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Navigation.Core;

/// <summary>
/// The order list. It decides where opening an order goes, and it is the only place that decision
/// is made.
/// </summary>
public sealed partial class OrderListNavigationViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    public OrderListNavigationViewModel(INavigationService navigation, IEnumerable<OrderSummary> orders)
    {
        _navigation = navigation;
        Orders = new ObservableCollection<OrderSummary>(orders);
    }

    public ObservableCollection<OrderSummary> Orders { get; }

    /// <summary>
    /// Opens an order, on the screen its state allows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cancelled order is a financial record. Editing it would change what was already reported,
    /// so it opens read-only.
    /// </para>
    /// <para>
    /// The rule is enforced here, at the navigation decision, rather than inside the editable
    /// screen. That way there is exactly one place the editable screen can be reached from, and it
    /// refuses. Enforcing it in the destination instead would leave the editable screen reachable
    /// and relying on every future caller to remember.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private async Task OpenOrderAsync(OrderSummary? order)
    {
        if (order is null)
        {
            return;
        }

        var route = order.State == OrderState.Cancelled
            ? OrderRoutes.OrderAudit
            : OrderRoutes.OrderDetail;

        await _navigation.GoToAsync(
            route,
            new Dictionary<string, object> { [OrderRoutes.OrderIdParameter] = order.Id });
    }
}
