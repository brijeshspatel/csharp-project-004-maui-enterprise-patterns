using Navigation.Core;

namespace Navigation.Core.Tests;

/// <summary>
/// Every command here is asynchronous, so every test awaits <c>ExecuteAsync</c>. Calling
/// <c>Execute</c> returns before the work finishes, and an assertion after it would pass or fail on
/// timing.
/// </summary>
public class OrderNavigationTests
{
    private static List<OrderSummary> Orders() =>
    [
        new(1, "ACME-01", 100m, OrderState.Outstanding),
        new(2, "ACME-02", 250m, OrderState.Cancelled),
        new(3, "GLOBEX-01", 400m, OrderState.Outstanding),
    ];

    [Fact]
    public void TheListExposesTheOrdersItWasGiven_InOrder()
    {
        var list = new OrderListNavigationViewModel(new RecordingNavigationService(), Orders());

        Assert.Collection(
            list.Orders,
            order => Assert.Equal("ACME-01", order.CustomerReference),
            order => Assert.Equal("ACME-02", order.CustomerReference),
            order => Assert.Equal("GLOBEX-01", order.CustomerReference));
    }

    [Fact]
    public async Task OpeningAnOutstandingOrder_GoesToTheEditableDetail_CarryingTheId()
    {
        var navigation = new RecordingNavigationService();
        var list = new OrderListNavigationViewModel(navigation, Orders());

        await list.OpenOrderCommand.ExecuteAsync(Orders()[0]);

        var request = Assert.Single(navigation.Requests);
        Assert.Equal(OrderRoutes.OrderDetail, request.Route);
        Assert.NotNull(request.Parameters);
        Assert.Equal(1, request.Parameters[OrderRoutes.OrderIdParameter]);
    }

    [Fact]
    public async Task OpeningACancelledOrder_GoesToTheAuditScreenInstead()
    {
        var navigation = new RecordingNavigationService();
        var list = new OrderListNavigationViewModel(navigation, Orders());

        await list.OpenOrderCommand.ExecuteAsync(Orders()[1]);

        var request = Assert.Single(navigation.Requests);
        Assert.Equal(OrderRoutes.OrderAudit, request.Route);
        Assert.NotEqual(OrderRoutes.OrderDetail, request.Route);
    }

    [Fact]
    public async Task OpeningNothing_NavigatesNowhere()
    {
        var navigation = new RecordingNavigationService();
        var list = new OrderListNavigationViewModel(navigation, Orders());

        await list.OpenOrderCommand.ExecuteAsync(null);

        Assert.Empty(navigation.Requests);
    }

    [Fact]
    public async Task GoingBack_AsksToGoBack_NotToARoute()
    {
        var navigation = new RecordingNavigationService();
        var detail = new OrderDetailNavigationViewModel(navigation, Orders());

        await detail.GoBackCommand.ExecuteAsync(null);

        var request = Assert.Single(navigation.Requests);
        Assert.Equal(NavigationRequest.BackRoute, request.Route);
        Assert.Null(request.Parameters);
    }

    [Fact]
    public void ApplyParameters_WithAnIntegerId_LoadsThatOrder()
    {
        var detail = new OrderDetailNavigationViewModel(new RecordingNavigationService(), Orders());

        detail.ApplyParameters(new Dictionary<string, object> { [OrderRoutes.OrderIdParameter] = 3 });

        Assert.NotNull(detail.Order);
        Assert.Equal("GLOBEX-01", detail.Order.CustomerReference);
        Assert.Equal(string.Empty, detail.Problem);
    }

    [Fact]
    public void ApplyParameters_WithTheIdAsAString_LoadsThatOrderToo()
    {
        // What a query-string navigation delivers. Accepting only int would compile, pass every
        // other test here, and fail the moment the demonstration used a query string.
        var detail = new OrderDetailNavigationViewModel(new RecordingNavigationService(), Orders());

        detail.ApplyParameters(new Dictionary<string, object> { [OrderRoutes.OrderIdParameter] = "3" });

        Assert.NotNull(detail.Order);
        Assert.Equal("GLOBEX-01", detail.Order.CustomerReference);
    }

    [Fact]
    public void ApplyParameters_WithTheKeyMissing_SaysSoAndDoesNotThrow()
    {
        var detail = new OrderDetailNavigationViewModel(new RecordingNavigationService(), Orders());

        detail.ApplyParameters(new Dictionary<string, object>());

        Assert.Null(detail.Order);
        Assert.Equal("No order was specified.", detail.Problem);
    }

    [Fact]
    public void ApplyParameters_WithAValueOfTheWrongType_SaysSoAndDoesNotThrow()
    {
        var detail = new OrderDetailNavigationViewModel(new RecordingNavigationService(), Orders());

        detail.ApplyParameters(new Dictionary<string, object> { [OrderRoutes.OrderIdParameter] = new object() });

        Assert.Null(detail.Order);
        Assert.Contains("not a number", detail.Problem);
    }

    [Fact]
    public void ApplyParameters_WithAnUnknownId_SaysSoRatherThanShowingNothing()
    {
        var detail = new OrderDetailNavigationViewModel(new RecordingNavigationService(), Orders());

        detail.ApplyParameters(new Dictionary<string, object> { [OrderRoutes.OrderIdParameter] = 99 });

        Assert.Null(detail.Order);
        Assert.Equal("Order 99 was not found.", detail.Problem);
    }
}
