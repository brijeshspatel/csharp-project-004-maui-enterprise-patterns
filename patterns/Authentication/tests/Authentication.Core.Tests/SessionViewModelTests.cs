namespace Authentication.Core.Tests;

/// <summary>
/// The screen: what it says, and what it never says.
/// </summary>
public class SessionViewModelTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    private static (SessionViewModel Screen, FakeIdentityProvider Provider, InMemoryTokenStore Store)
        Build(AuthenticationSession? stored)
    {
        var clock = new TestClock(Noon);
        var provider = new FakeIdentityProvider(clock);
        var store = new InMemoryTokenStore(stored);
        var tokens = new AccessTokenProvider(provider, store, clock, AuthenticationPolicy.Default);

        return (new SessionViewModel(tokens), provider, store);
    }

    [Fact]
    public async Task ASuccessfulSignIn_ReportsIt()
    {
        var (screen, provider, store) = Build(stored: null);

        await screen.SignInCommand.ExecuteAsync(null);

        Assert.True(screen.IsSignedIn);
        Assert.Equal("Signed in.", screen.Status);
        Assert.Equal(1, provider.SignInCount);
        Assert.NotNull(store.Stored);
    }

    [Fact]
    public async Task ARefusedSignIn_ReportsTheFailureAndStaysSignedOut()
    {
        var (screen, provider, _) = Build(stored: null);
        provider.RefusesSignIn = true;

        await screen.SignInCommand.ExecuteAsync(null);

        Assert.False(screen.IsSignedIn);
        Assert.Contains("Sign-in failed", screen.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UsingTheToken_ReportsTheRedactedFormAndNeverTheValue()
    {
        var (screen, _, _) = Build(Fake.Session(Noon.AddMinutes(30)));

        await screen.UseTokenCommand.ExecuteAsync(null);

        Assert.True(screen.IsSignedIn);
        Assert.Contains("redacted", screen.Status, StringComparison.Ordinal);

        // The assertion that matters. A screen is a log with a user attached.
        Assert.DoesNotContain(Fake.AccessTokenValue, screen.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UsingTheTokenWithNoSession_AsksTheUserToSignIn()
    {
        var (screen, _, _) = Build(stored: null);

        await screen.UseTokenCommand.ExecuteAsync(null);

        Assert.False(screen.IsSignedIn);
        Assert.Contains("Sign-in required", screen.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SigningOut_ReportsItAndTheNextUseAsksForASignIn()
    {
        var (screen, _, store) = Build(Fake.Session(Noon.AddMinutes(30)));

        await screen.UseTokenCommand.ExecuteAsync(null);
        Assert.True(screen.IsSignedIn);

        await screen.SignOutCommand.ExecuteAsync(null);

        Assert.False(screen.IsSignedIn);
        Assert.Equal("Signed out.", screen.Status);
        Assert.Null(store.Stored);

        await screen.UseTokenCommand.ExecuteAsync(null);
        Assert.Contains("Sign-in required", screen.Status, StringComparison.Ordinal);
    }
}
