using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RemoteData.Core;

/// <summary>
/// A list of orders, and an honest statement of where they came from.
/// </summary>
/// <remarks>
/// A screen that cannot tell live data from stale will present a month-old answer as current. The
/// source is part of the answer, not a detail of how it was fetched.
/// </remarks>
public sealed partial class OrderListViewModel : ObservableObject
{
    private readonly IOrderCatalogue _catalogue;

    public OrderListViewModel(IOrderCatalogue catalogue)
    {
        _catalogue = catalogue;
    }

    public ObservableCollection<OrderDto> Orders { get; } = [];

    [ObservableProperty]
    private string _status = "Not loaded.";

    [ObservableProperty]
    private bool _isShowingStaleData;

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        // Not caught here. A cancelled refresh has no result to show, and the command
        // infrastructure already understands a cancelled task.
        var result = await _catalogue.GetOrdersAsync(cancellationToken);

        if (!result.Succeeded)
        {
            IsShowingStaleData = false;
            Status = result.Problem!;
            return;
        }

        Orders.Clear();
        foreach (var order in result.Orders!)
        {
            Orders.Add(order);
        }

        IsShowingStaleData = result.Source == DataSource.Cache;
        Status = IsShowingStaleData
            ? "The service did not answer. Showing the last orders received."
            : $"{Orders.Count} order(s), current as of this refresh.";
    }
}
