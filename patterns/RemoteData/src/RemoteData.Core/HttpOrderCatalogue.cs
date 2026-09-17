using System.Net.Http.Json;
using System.Text.Json;

namespace RemoteData.Core;

/// <summary>
/// Reads orders from a service.
/// </summary>
public interface IOrderCatalogue
{
    Task<OrderCatalogueResult> GetOrdersAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Reads orders over HTTP, once.
/// </summary>
/// <remarks>
/// <para>
/// <b>One attempt.</b> No retry, no backoff, no circuit breaker: those are separate catalogue
/// entries, and an entry that quietly did all three would teach none of them.
/// </para>
/// <para>
/// Takes an <see cref="HttpClient"/> rather than an interface over one. The client is the thing
/// whose behaviour matters here, and the framework already provides a seam for testing it — a
/// stub <see cref="HttpMessageHandler"/>. Wrapping the client would test the wrapper.
/// </para>
/// </remarks>
public sealed class HttpOrderCatalogue : IOrderCatalogue
{
    private readonly HttpClient _client;
    private readonly IOrderCache _cache;

    public HttpOrderCatalogue(HttpClient client, IOrderCache cache)
    {
        _client = client;
        _cache = cache;
    }

    public async Task<OrderCatalogueResult> GetOrdersAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetAsync("orders", cancellationToken);

            // GetAsync does not throw on a non-success status. A 500 arrives here as an ordinary
            // response, and code that forgets to ask would carry on and deserialise the error page.
            if (!response.IsSuccessStatusCode)
            {
                // The server answered. It said no, and that is information — showing stale data
                // instead would hide it.
                return OrderCatalogueResult.Failed(
                    $"The service answered {(int)response.StatusCode}. One attempt was made.");
            }

            var orders = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderDto>>(cancellationToken);
            if (orders is null)
            {
                return OrderCatalogueResult.Failed("The service returned no orders.");
            }

            _cache.Write(orders);
            return OrderCatalogueResult.Live(orders);
        }
        // ORDER MATTERS. This filtered catch must come first. A client timeout and a caller's
        // cancellation both arrive as TaskCanceledException, so an unfiltered catch placed above
        // this one would swallow every cancellation into the timeout path — the caller would be
        // handed a value it never asked for, for a screen it has already left. The compiler does
        // not object, and only one test notices.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller asked to stop. It gets nothing, not even the cache.
            throw;
        }
        catch (OperationCanceledException)
        {
            // The client's own timeout fired. We do not know the answer, which is the one case
            // where the last known answer is better than nothing.
            var cached = _cache.Read();
            return cached is not null
                ? OrderCatalogueResult.FromCache(cached)
                : OrderCatalogueResult.Failed(
                    "The service did not answer in time, and nothing has been read before. One attempt was made.");
        }
        catch (HttpRequestException ex)
        {
            return OrderCatalogueResult.Failed(
                $"The service could not be reached: {ex.Message} One attempt was made.");
        }
        catch (JsonException)
        {
            // The service answered with something that is not orders. Stale data would hide a real
            // problem with the service or the contract.
            return OrderCatalogueResult.Failed("The service returned a response that could not be read.");
        }
    }
}
