using System.Net;
using RemoteData.Core;

namespace RemoteData.Core.Tests;

/// <summary>
/// Nothing here reaches a network. Every test drives the real <see cref="HttpClient"/> through a
/// stub handler.
/// </summary>
public class HttpOrderCatalogueTests
{
    private const string TwoOrders =
        """[{"id":1,"customerReference":"ACME-01","total":100},{"id":2,"customerReference":"ACME-02","total":250}]""";

    private static HttpClient Client(StubHttpMessageHandler handler, TimeSpan? timeout = null) =>
        new(handler)
        {
            BaseAddress = new Uri("https://orders.invalid/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };

    private static IReadOnlyList<OrderDto> Cached() => [new OrderDto(9, "STALE-01", 999m)];

    [Fact]
    public async Task AServiceThatAnswers_ReturnsTheOrders_MarkedLive()
    {
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.OK, TwoOrders)),
            new InMemoryOrderCache());

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(DataSource.Live, result.Source);
        Assert.Equal(2, result.Orders!.Count);
        Assert.Equal("ACME-01", result.Orders[0].CustomerReference);
    }

    [Fact]
    public async Task AServiceThatAnswers_UpdatesTheCache()
    {
        var cache = new InMemoryOrderCache();
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.OK, TwoOrders)),
            cache);

        await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.Equal(2, cache.Read()!.Count);
    }

    [Fact]
    public async Task ATimeout_WithSomethingCached_ReturnsItMarkedStale()
    {
        // We do not know the answer, so the last answer is better than nothing — provided the
        // screen is told it is stale.
        var cache = new InMemoryOrderCache();
        cache.Write(Cached());
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Hanging(), TimeSpan.FromMilliseconds(150)),
            cache);

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(DataSource.Cache, result.Source);
        Assert.Equal("STALE-01", result.Orders!.Single().CustomerReference);
    }

    [Fact]
    public async Task ATimeout_WithNothingCached_Fails()
    {
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Hanging(), TimeSpan.FromMilliseconds(150)),
            new InMemoryOrderCache());

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("did not answer in time", result.Problem);
    }

    [Fact]
    public async Task ACancelledCall_Throws_AndDoesNotReadTheCache()
    {
        // The load-bearing test. A timeout and a cancellation arrive as the same exception type,
        // and mean opposite things: one is "we do not know", the other is "the user left". A
        // cancelled caller must get nothing — returning it cached data would update a screen that
        // is already gone.
        var cache = new InMemoryOrderCache();
        cache.Write(Cached());
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Hanging(), TimeSpan.FromSeconds(30)),
            cache);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalogue.GetOrdersAsync(cts.Token));
    }

    [Fact]
    public async Task AServerError_Fails_NamingTheStatus_AndDoesNotThrow()
    {
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.InternalServerError, "{}")),
            new InMemoryOrderCache());

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("500", result.Problem);
    }

    [Fact]
    public async Task AServerError_DoesNotFallBackToTheCache_EvenWhenOneExists()
    {
        // The server answered. It said no, and that is information. Showing month-old orders
        // instead would hide a real state from the user.
        var cache = new InMemoryOrderCache();
        cache.Write(Cached());
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.InternalServerError, "{}")),
            cache);

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Orders);
        Assert.Null(result.Source);
    }

    [Fact]
    public async Task ABodyOfLiteralNull_Fails_RatherThanShowingAnEmptyList()
    {
        // A valid JSON document that deserialises to nothing. It is not a parse failure, so it
        // reaches the success path, and treating it as "no orders" would show an empty list as a
        // current answer.
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.OK, "null")),
            new InMemoryOrderCache());

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("no orders", result.Problem);
    }

    [Fact]
    public async Task AnUnreachableService_Fails_AndSaysOnlyOneAttemptWasMade()
    {
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Unreachable("No such host is known.")),
            new InMemoryOrderCache());

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("could not be reached", result.Problem);
        Assert.Contains("One attempt was made", result.Problem);
    }

    [Fact]
    public async Task AnUnreachableService_DoesNotFallBackToTheCache()
    {
        // Deliberately the same rule as a server error. The cache is for "we do not know because
        // nothing came back in time", not for every failure — a connection that was refused is a
        // fact the user should see.
        var cache = new InMemoryOrderCache();
        cache.Write(Cached());
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Unreachable("No such host is known.")),
            cache);

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Null(result.Orders);
    }

    [Fact]
    public async Task ABodyThatIsNotOrders_Fails()
    {
        var catalogue = new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.OK, "not json at all")),
            new InMemoryOrderCache());

        var result = await catalogue.GetOrdersAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("could not be read", result.Problem);
    }

    [Fact]
    public async Task TheCallersCancellation_ReachesTheRequestWhileItIsInFlight()
    {
        // Asserts identity, not presence: passing CancellationToken.None would also deliver "a
        // token" to the handler.
        //
        // The assertion has to be made while the request is in flight. HttpClient does not hand the
        // handler the caller's token — it hands it a token linked from the caller's and the
        // client's own timeout, and that link is torn down when the request ends. Cancelling the
        // caller's source afterwards therefore shows nothing on the recorded token.
        var handler = StubHttpMessageHandler.Hanging();
        var catalogue = new HttpOrderCatalogue(
            Client(handler, TimeSpan.FromSeconds(30)),
            new InMemoryOrderCache());
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => catalogue.GetOrdersAsync(cts.Token));

        // The client's timeout was thirty seconds, so only the caller's cancellation can have
        // reached the handler.
        Assert.True(handler.ReceivedToken.IsCancellationRequested);
    }

    [Fact]
    public async Task TheScreen_SaysWhenItIsShowingStaleData()
    {
        var cache = new InMemoryOrderCache();
        cache.Write(Cached());
        var screen = new OrderListViewModel(new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Hanging(), TimeSpan.FromMilliseconds(150)),
            cache));

        await screen.RefreshCommand.ExecuteAsync(null);

        Assert.True(screen.IsShowingStaleData);
        Assert.Contains("last orders received", screen.Status);
        Assert.Single(screen.Orders);
    }

    [Fact]
    public async Task TheScreen_ReportsAFailureAndShowsNothingStale()
    {
        var screen = new OrderListViewModel(new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.InternalServerError, "{}")),
            new InMemoryOrderCache()));

        await screen.RefreshCommand.ExecuteAsync(null);

        Assert.False(screen.IsShowingStaleData);
        Assert.Contains("500", screen.Status);
        Assert.Empty(screen.Orders);
    }

    [Fact]
    public async Task TheScreen_ShowsLiveOrdersAndSaysSo()
    {
        var screen = new OrderListViewModel(new HttpOrderCatalogue(
            Client(StubHttpMessageHandler.Returning(HttpStatusCode.OK, TwoOrders)),
            new InMemoryOrderCache()));

        await screen.RefreshCommand.ExecuteAsync(null);

        Assert.False(screen.IsShowingStaleData);
        Assert.Contains("current as of this refresh", screen.Status);
        Assert.Equal(2, screen.Orders.Count);
    }
}
