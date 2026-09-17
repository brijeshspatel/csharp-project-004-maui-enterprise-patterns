using System.Net;
using System.Text;

namespace Authentication.Core.Tests;

/// <summary>
/// Attaching the token, and recovering once when the server refuses it.
/// </summary>
/// <remarks>
/// The real <see cref="HttpClient"/> and the real <see cref="AuthenticatingHandler"/> are under
/// test. Only the socket is replaced, by a handler that answers from a script. <b>No request leaves
/// this process</b>, and the address used is in the reserved <c>.invalid</c> top-level domain,
/// which cannot resolve.
/// </remarks>
public class AuthenticatingHandlerTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private static (HttpClient Client, ScriptedHandler Inner, FakeIdentityProvider Provider, AccessTokenProvider Tokens)
        Build(params HttpStatusCode[] statuses)
    {
        var clock = new TestClock(Noon);
        var provider = new FakeIdentityProvider(clock);
        var store = new InMemoryTokenStore(Fake.Session(Noon.AddMinutes(30)));
        var tokens = new AccessTokenProvider(provider, store, clock, AuthenticationPolicy.Default);

        var inner = new ScriptedHandler(statuses);
        var handler = new AuthenticatingHandler(tokens) { InnerHandler = inner };

        return (new HttpClient(handler), inner, provider, tokens);
    }

    [Fact]
    public async Task EveryRequestCarriesTheBearerToken()
    {
        var (client, inner, _, _) = Build(HttpStatusCode.OK);

        var response = await client.GetAsync(Fake.Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.Sends);
        Assert.Equal(Fake.AccessTokenValue, Assert.Single(inner.BearerTokens));
    }

    [Fact]
    public async Task ANonUnauthorizedFailure_PassesThroughWithNoRefresh()
    {
        var (client, inner, provider, _) = Build(HttpStatusCode.InternalServerError);

        var response = await client.GetAsync(Fake.Endpoint);

        // A 500 says the service is unwell. It says nothing about the credential, and treating it
        // as an authentication failure would burn a refresh token on every outage.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, inner.Sends);
        Assert.Equal(0, provider.RefreshCount);
    }

    [Fact]
    public async Task A401_CausesExactlyOneRefreshAndExactlyOneResend()
    {
        var (client, inner, provider, _) = Build(HttpStatusCode.Unauthorized, HttpStatusCode.OK);

        var response = await client.GetAsync(Fake.Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Sends);
        Assert.Equal(1, provider.RefreshCount);

        // The second attempt carried a different token from the first. A resend with the same
        // credential is not a recovery, it is the same request twice.
        Assert.Equal(Fake.AccessTokenValue, inner.BearerTokens[0]);
        Assert.NotEqual(inner.BearerTokens[0], inner.BearerTokens[1]);
    }

    [Fact]
    public async Task A401OnTheResend_IsReturnedAsItStands()
    {
        var (client, inner, provider, _) = Build(HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized);

        var response = await client.GetAsync(Fake.Endpoint);

        // No loop. The second token was minted seconds earlier, so a second refusal is the server
        // declining the identity rather than the credential.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, inner.Sends);
        Assert.Equal(1, provider.RefreshCount);
    }

    [Fact]
    public async Task AResentRequestKeepsItsBody_ItsHeadersAndItsOptions()
    {
        // THE TRAP. An HttpRequestMessage cannot be sent twice, so the resend needs a clone -- and
        // a clone built carelessly loses the body, which is a defect nobody sees until a POST.
        var (client, inner, _, _) = Build(HttpStatusCode.Unauthorized, HttpStatusCode.Created);

        using var request = new HttpRequestMessage(HttpMethod.Post, Fake.Endpoint)
        {
            Content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("""{"quantity":3}"""))),
        };
        request.Headers.Add("X-Correlation", "correlation-fixture-value");
        request.Options.Set(ScriptedHandler.TenantHint, "tenant-fixture-value");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, inner.Sends);
        Assert.Equal("""{"quantity":3}""", inner.Bodies[0]);
        Assert.Equal("""{"quantity":3}""", inner.Bodies[1]);
        Assert.Equal("correlation-fixture-value", inner.CustomHeaders[0]);
        Assert.Equal("correlation-fixture-value", inner.CustomHeaders[1]);

        // Request options are how a pipeline passes per-request context down to the socket.
        // A clone that drops them silently changes what the resent call means.
        Assert.Equal("tenant-fixture-value", inner.TenantHints[0]);
        Assert.Equal("tenant-fixture-value", inner.TenantHints[1]);
    }

    [Fact]
    public async Task WhenTheRefreshIsRefused_TheCallerIsToldToSignInAgain()
    {
        var (client, inner, provider, _) = Build(HttpStatusCode.Unauthorized);
        provider.RefusesRefresh = true;

        // What a caller of HttpClient.SendAsync actually observes when a DelegatingHandler throws
        // is measured here rather than assumed, because the README documents the catch that works.
        var failure = await Assert.ThrowsAnyAsync<Exception>(() => client.GetAsync(Fake.Endpoint));

        var required = InChain<AuthenticationRequiredException>(failure);

        Assert.Equal(1, inner.Sends);
        Assert.NotNull(required);

        // The measurement: whether the caller receives it directly or wrapped by the pipeline.
        Assert.Same(failure, required);
    }

    /// <summary>The first exception of a given type in the chain, or null.</summary>
    private static T? InChain<T>(Exception failure)
        where T : Exception
    {
        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            if (current is T match)
            {
                return match;
            }
        }

        return null;
    }
}
