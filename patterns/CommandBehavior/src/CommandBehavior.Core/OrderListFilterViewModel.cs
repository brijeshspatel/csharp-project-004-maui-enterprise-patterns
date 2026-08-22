using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommandBehavior.Core;

/// <summary>
/// A quick-filter bar over an order list. Demonstrates both halves of the pattern: a directly
/// bound command (<see cref="ClearFilterCommand"/>, for a control that supports commands
/// natively) and a behaviour-bridged one (<see cref="ApplyFilterCommand"/>, invoked from an
/// event a control does not natively expose as a command).
/// </summary>
public sealed partial class OrderListFilterViewModel : ObservableObject
{
    private readonly IReadOnlyList<FilterableOrder> _allOrders;

    public OrderListFilterViewModel(IReadOnlyList<FilterableOrder> allOrders)
    {
        _allOrders = allOrders;
        FilteredOrders = new ObservableCollection<FilterableOrder>(allOrders);
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearFilterCommand))]
    private string _filterText = string.Empty;

    public ObservableCollection<FilterableOrder> FilteredOrders { get; }

    [RelayCommand]
    private void ApplyFilter()
    {
        var matches = string.IsNullOrWhiteSpace(FilterText)
            ? _allOrders
            : _allOrders.Where(o => o.CustomerReference.Contains(FilterText, StringComparison.OrdinalIgnoreCase));

        FilteredOrders.Clear();
        foreach (var order in matches)
        {
            FilteredOrders.Add(order);
        }
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void ClearFilter()
    {
        FilterText = string.Empty;
        ApplyFilter();
    }

    private bool CanClear() => !string.IsNullOrEmpty(FilterText);
}
