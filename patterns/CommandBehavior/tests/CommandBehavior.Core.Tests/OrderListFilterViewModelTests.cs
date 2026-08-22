using CommandBehavior.Core;

namespace CommandBehavior.Core.Tests;

public class OrderListFilterViewModelTests
{
    private static OrderListFilterViewModel CreateViewModel() => new(
    [
        new FilterableOrder(1, "ACME-01", 100m),
        new FilterableOrder(2, "ACME-02", 200m),
        new FilterableOrder(3, "GLOBEX-01", 300m),
    ]);

    [Fact]
    public void ApplyFilterCommand_FiltersByCustomerReference_CaseInsensitiveSubstring()
    {
        var vm = CreateViewModel();
        vm.FilterText = "acme";

        vm.ApplyFilterCommand.Execute(null);

        Assert.Equal(2, vm.FilteredOrders.Count);
        Assert.All(vm.FilteredOrders, o => Assert.Contains("ACME", o.CustomerReference));
    }

    [Fact]
    public void ApplyFilterCommand_WithEmptyText_ShowsAllOrders()
    {
        var vm = CreateViewModel();
        vm.FilterText = "acme";
        vm.ApplyFilterCommand.Execute(null);

        vm.FilterText = string.Empty;
        vm.ApplyFilterCommand.Execute(null);

        Assert.Equal(3, vm.FilteredOrders.Count);
    }

    [Fact]
    public void ClearFilterCommand_CanExecuteTracksFilterTextEmptiness()
    {
        var vm = CreateViewModel();

        Assert.False(vm.ClearFilterCommand.CanExecute(null));

        vm.FilterText = "acme";
        Assert.True(vm.ClearFilterCommand.CanExecute(null));

        vm.ClearFilterCommand.Execute(null);
        Assert.False(vm.ClearFilterCommand.CanExecute(null));
    }

    [Fact]
    public void ClearFilterCommand_ResetsFilterTextAndShowsAllOrders()
    {
        var vm = CreateViewModel();
        vm.FilterText = "globex";
        vm.ApplyFilterCommand.Execute(null);
        Assert.Single(vm.FilteredOrders);

        vm.ClearFilterCommand.Execute(null);

        Assert.Equal(string.Empty, vm.FilterText);
        Assert.Equal(3, vm.FilteredOrders.Count);
    }
}
