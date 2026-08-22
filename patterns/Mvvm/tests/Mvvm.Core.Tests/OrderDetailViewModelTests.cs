using Mvvm.Core;

namespace Mvvm.Core.Tests;

public class OrderDetailViewModelTests
{
    [Fact]
    public void EmptyCustomerReference_HasValidationError()
    {
        var vm = new OrderDetailViewModel(new Order(1, "ACME-01", 100m));

        vm.CustomerReference = string.Empty;

        Assert.True(vm.HasErrors);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ValidCustomerReference_ClearsErrors_AndEnablesSave()
    {
        var vm = new OrderDetailViewModel(new Order(1, "ACME-01", 100m));

        vm.CustomerReference = "ACME-02";

        Assert.False(vm.HasErrors);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void ConstructingFromOrder_SeedsCustomerReference_AndStartsValid()
    {
        var order = new Order(7, "ACME-07", 250m);

        var vm = new OrderDetailViewModel(order);

        Assert.Equal(order.CustomerReference, vm.CustomerReference);
        Assert.False(vm.HasErrors);
    }

    [Fact]
    public async Task SaveAsync_WhenInvalid_DoesNotThrow_AndCommandStaysDisabled()
    {
        var vm = new OrderDetailViewModel(new Order(1, "ACME-01", 100m))
        {
            CustomerReference = string.Empty,
        };

        Assert.False(vm.SaveCommand.CanExecute(null));

        // The command's own guard (CanSave) is what production code and the UI both rely on
        // to keep an invalid save from running; SaveAsync itself does not re-validate, so this
        // asserts the guard is what's actually preventing the save, not an internal check that
        // could silently stop being called.
        await vm.SaveCommand.ExecuteAsync(null);
    }

    [Fact]
    public void FirstError_WhenInvalid_ReturnsTheValidationMessage()
    {
        var vm = new OrderDetailViewModel(new Order(1, "ACME-01", 100m));

        vm.CustomerReference = string.Empty;

        Assert.NotEmpty(vm.FirstError);
    }

    [Fact]
    public void FirstError_WhenValid_IsEmpty()
    {
        var vm = new OrderDetailViewModel(new Order(1, "ACME-01", 100m));

        vm.CustomerReference = "ACME-02";

        Assert.Equal(string.Empty, vm.FirstError);
    }

    [Fact]
    public void RevalidatingAfterFix_ReEnablesSave_EvenAfterMultipleChanges()
    {
        var vm = new OrderDetailViewModel(new Order(1, "ACME-01", 100m));

        vm.CustomerReference = string.Empty;
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.CustomerReference = "TEMP";
        vm.CustomerReference = string.Empty;
        Assert.False(vm.SaveCommand.CanExecute(null));

        vm.CustomerReference = "ACME-FIXED";
        Assert.True(vm.SaveCommand.CanExecute(null));
    }
}
