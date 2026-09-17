namespace Authentication.Core.Tests;

/// <summary>
/// The token lifetime: acquire, cache, expire, refresh once, and discard.
/// </summary>
public class AccessTokenProviderTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Skew = TimeSpan.FromMinutes(1);

    private static (AccessTokenProvider Tokens, FakeIdentityProvider Provider, InMemoryTokenStore Store, TestClock Clock)
        Build(AuthenticationSession? stored)
    {
        var clock = new TestClock(Noon);
        var provider = new FakeIdentityProvider(clock);
        var store = new InMemoryTokenStore(stored);

        return (new AccessTokenProvider(provider, store, clock, new AuthenticationPolicy(Skew)), provider, store, clock);
    }

    [Fact]
    public async Task AUsableCachedToken_IsReturnedWithoutTheProviderBeingCalled()
    {
        var (tokens, provider, store, _) = Build(Fake.Session(Noon.AddMinutes(30)));

        var first = await tokens.GetAccessTokenAsync(CancellationToken.None);
        var second = await tokens.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(Fake.AccessTokenValue, first.Value);
        Assert.Equal(first, second);
        Assert.Equal(0, provider.RefreshCount);
        Assert.Equal(0, provider.SignInCount);

        // The store is consulted once, not on every acquisition.
        Assert.Equal(1, store.Loads);
    }

    [Fact]
    public async Task AStoredSession_IsLoadedRatherThanASignInBeingDemanded()
    {
        var (tokens, provider, store, _) = Build(Fake.Session(Noon.AddMinutes(30)));

        var token = await tokens.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(Fake.AccessTokenValue, token.Value);
        Assert.Equal(0, provider.SignInCount);
        Assert.True(tokens.IsSignedIn);
        Assert.Equal(1, store.Loads);
    }

    [Fact]
    public async Task AnExpiredToken_IsRefreshedAndTheNewSessionStored()
    {
        var (tokens, provider, store, _) = Build(Fake.Session(Noon.AddSeconds(-1)));

        var token = await tokens.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(1, provider.RefreshCount);
        Assert.Equal(Fake.RefreshTokenValue, provider.RefreshTokenLastSeen);
        Assert.NotEqual(Fake.AccessTokenValue, token.Value);
        Assert.Equal(token, store.Stored?.AccessToken);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public async Task ATokenInsideTheSkewWindow_IsRefreshedThoughItHasNotExpired()
    {
        var (tokens, provider, _, _) = Build(Fake.Session(Noon.AddSeconds(30)));

        await tokens.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(1, provider.RefreshCount);
    }

    [Fact]
    public async Task WithNoSessionAnywhere_AcquisitionDemandsASignIn()
    {
        var (tokens, provider, _, _) = Build(stored: null);

        var failure = await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => tokens.GetAccessTokenAsync(CancellationToken.None));

        Assert.Contains("no session", failure.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, provider.RefreshCount);
        Assert.False(tokens.IsSignedIn);
    }

    [Fact]
    public async Task ASignIn_EstablishesASessionAndStoresIt()
    {
        var (tokens, provider, store, _) = Build(stored: null);

        await tokens.SignInAsync(CancellationToken.None);
        var token = await tokens.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(1, provider.SignInCount);
        Assert.Equal(0, provider.RefreshCount);
        Assert.True(tokens.IsSignedIn);
        Assert.Equal(token, store.Stored?.AccessToken);
    }

    [Fact]
    public async Task ARefusedSignIn_LeavesNoSessionAndSurfacesTheProvidersFailure()
    {
        var (tokens, provider, store, _) = Build(stored: null);
        provider.RefusesSignIn = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tokens.SignInAsync(CancellationToken.None));

        Assert.False(tokens.IsSignedIn);
        Assert.Null(store.Stored);

        // And the gate is not left held: a later sign-in still works.
        provider.RefusesSignIn = false;
        await tokens.SignInAsync(CancellationToken.None);
        Assert.True(tokens.IsSignedIn);
    }

    [Fact]
    public async Task ARefusedRefresh_ClearsTheStoreAndDemandsASignIn()
    {
        var (tokens, provider, store, _) = Build(Fake.Session(Noon.AddSeconds(-1)));
        provider.RefusesRefresh = true;

        var failure = await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => tokens.GetAccessTokenAsync(CancellationToken.None));

        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Null(store.Stored);
        Assert.Equal(1, store.Clears);
        Assert.False(tokens.IsSignedIn);
    }

    [Fact]
    public async Task ARefusalReachedThroughAServerRejection_AlsoDiscardsTheTokenStillInMemory()
    {
        // THE CASE THAT MATTERS. On this path the cached token has NOT expired -- the clock says it
        // is fine and the server has refused it anyway. Clearing only the store would leave a token
        // in memory that passes every check this client can make, and the application would keep
        // presenting it until it was next launched, looking clean the whole time.
        var live = Fake.Session(Noon.AddMinutes(30));
        var (tokens, provider, store, _) = Build(live);

        var token = await tokens.GetAccessTokenAsync(CancellationToken.None);
        Assert.True(tokens.IsSignedIn);

        provider.RefusesRefresh = true;
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => tokens.RefreshAfterRejectionAsync(token, CancellationToken.None));

        Assert.Null(store.Stored);
        Assert.False(tokens.IsSignedIn);

        // The assertion the store-only fix would pass: the next acquisition must demand a sign-in
        // rather than hand out the token the server has already refused.
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => tokens.GetAccessTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task SignOut_ClearsTheStoreAndTheMemory()
    {
        var (tokens, _, store, _) = Build(Fake.Session(Noon.AddMinutes(30)));

        await tokens.GetAccessTokenAsync(CancellationToken.None);
        Assert.True(tokens.IsSignedIn);

        await tokens.SignOutAsync(CancellationToken.None);

        Assert.Null(store.Stored);
        Assert.Equal(1, store.Clears);
        Assert.False(tokens.IsSignedIn);

        // Not "the store is empty" -- that is the half that already worked. A sign-out that leaves
        // the in-memory copy behind looks like it worked and does not.
        await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => tokens.GetAccessTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentCallersOnAnExpiredToken_CauseExactlyOneRefresh()
    {
        var (tokens, provider, _, _) = Build(Fake.Session(Noon.AddSeconds(-1)));

        var held = new TaskCompletionSource();
        provider.HoldRefresh = held;

        // Each call runs synchronously as far as its first await, so by the time this loop ends the
        // first caller holds the gate inside the refresh and the other four are queued behind it.
        // Nothing here depends on timing.
        var callers = new List<Task<AccessToken>>();
        for (var i = 0; i < 5; i++)
        {
            callers.Add(tokens.GetAccessTokenAsync(CancellationToken.None));
        }

        Assert.Equal(1, provider.RefreshCount);

        held.SetResult();
        var results = await Task.WhenAll(callers);

        // THE ASSERTION THAT SEPARATES THE IMPLEMENTATIONS. Five callers each receiving a token
        // proves nothing -- an unguarded provider does that too, having refreshed five times and
        // burned four rotated refresh tokens on the way.
        Assert.Equal(1, provider.RefreshCount);
        Assert.All(results, token => Assert.Equal(results[0], token));
    }

    [Fact]
    public async Task TwoRequestsRefusedOnTheSameToken_CauseOneRefreshBetweenThem()
    {
        var (tokens, provider, _, _) = Build(Fake.Session(Noon.AddMinutes(30)));

        var rejected = await tokens.GetAccessTokenAsync(CancellationToken.None);

        var first = await tokens.RefreshAfterRejectionAsync(rejected, CancellationToken.None);
        var second = await tokens.RefreshAfterRejectionAsync(rejected, CancellationToken.None);

        // The second arrives, finds the current token is no longer the one it failed with, and
        // takes it. Keyed on the token that failed, not on "something failed".
        Assert.Equal(1, provider.RefreshCount);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task ARejectionAfterTheSessionHasEnded_DemandsASignIn()
    {
        var (tokens, _, _, _) = Build(Fake.Session(Noon.AddMinutes(30)));

        var token = await tokens.GetAccessTokenAsync(CancellationToken.None);
        await tokens.SignOutAsync(CancellationToken.None);

        var failure = await Assert.ThrowsAsync<AuthenticationRequiredException>(
            () => tokens.RefreshAfterRejectionAsync(token, CancellationToken.None));

        Assert.Contains("in flight", failure.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ACancelledRefresh_NeitherDiscardsTheSessionNorWedgesTheGate()
    {
        var (tokens, provider, store, _) = Build(Fake.Session(Noon.AddSeconds(-1)));
        provider.CancelsRefresh = true;

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => tokens.GetAccessTokenAsync(CancellationToken.None));

        // A cancellation is not a refusal. The session is untouched.
        Assert.NotNull(store.Stored);
        Assert.Equal(0, store.Clears);

        // And the gate was released, so a later call still gets through. A gate left held would
        // block every call for the life of the process while nothing looked wrong.
        provider.CancelsRefresh = false;
        var token = await tokens.GetAccessTokenAsync(CancellationToken.None);
        Assert.NotNull(token);
        Assert.Equal(2, provider.RefreshCount);
    }

    [Fact]
    public async Task ATokenThatExpiresWhileCached_IsRefreshedOnTheNextAcquisition()
    {
        var (tokens, provider, _, clock) = Build(Fake.Session(Noon.AddMinutes(5)));

        var first = await tokens.GetAccessTokenAsync(CancellationToken.None);
        Assert.Equal(0, provider.RefreshCount);

        clock.Advance(TimeSpan.FromMinutes(5));

        var second = await tokens.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(1, provider.RefreshCount);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task AStoreThatFailedToLoad_IsAskedAgainRatherThanAssumedEmptyForEver()
    {
        // _storeConsulted is set after the load, not before it, so a transient store failure does
        // not become a permanent conclusion that there is nothing stored.
        var clock = new TestClock(Noon);
        var provider = new FakeIdentityProvider(clock);
        var store = new ThrowingOnceTokenStore(Fake.Session(Noon.AddMinutes(30)));
        using var tokens = new AccessTokenProvider(provider, store, clock, new AuthenticationPolicy(Skew));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => tokens.GetAccessTokenAsync(CancellationToken.None));

        var token = await tokens.GetAccessTokenAsync(CancellationToken.None);

        Assert.Equal(Fake.AccessTokenValue, token.Value);
    }

    [Fact]
    public async Task TheProviderReleasesItsGateWhenDisposed()
    {
        var (tokens, _, _, _) = Build(Fake.Session(Noon.AddMinutes(30)));

        await tokens.GetAccessTokenAsync(CancellationToken.None);
        tokens.Dispose();

        // Disposing twice is the shape a container will produce, and must not throw.
        tokens.Dispose();
    }

    private sealed class ThrowingOnceTokenStore(AuthenticationSession session) : ITokenStore
    {
        private bool _thrown;

        public Task<AuthenticationSession?> LoadAsync(CancellationToken cancellationToken)
        {
            if (!_thrown)
            {
                _thrown = true;
                return Task.FromException<AuthenticationSession?>(new InvalidOperationException("The store was busy."));
            }

            return Task.FromResult<AuthenticationSession?>(session);
        }

        public Task SaveAsync(AuthenticationSession newSession, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
