using System.Globalization;
using System.Net;

namespace Authentication.Core.Tests;

/// <summary>
/// Values that announce they are not credentials.
/// </summary>
/// <remarks>
/// Deliberately unmistakable. A fixture that looked like a real token would be the hazard this
/// entry exists to warn about: somebody copies a plausible literal out of a reference
/// implementation and puts it somewhere real.
/// </remarks>
internal static class Fake
{
    public const string AccessTokenValue = "not-a-real-token-fixture-value-only";
    public const string RefreshTokenValue = "not-a-real-refresh-token-fixture-value-only";

    /// <summary>The reserved TLD from RFC 2606. It cannot resolve, and nothing here sends to it.</summary>
    public static readonly Uri Endpoint = new("https://service.invalid/orders");

    public static AuthenticationSession Session(DateTimeOffset expiresAt) =>
        new(new AccessToken(AccessTokenValue, expiresAt), RefreshTokenValue);
}

/// <summary>A clock a test moves, so that no test waits.</summary>
internal sealed class TestClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = now;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>
/// Stands in for the identity provider, and counts what it was asked to do.
/// </summary>
/// <remarks>
/// It reaches nothing. Every session it issues is fabricated here, and every value in one says so.
/// </remarks>
internal sealed class FakeIdentityProvider(IClock clock) : IIdentityProvider
{
    private int _issued;

    public int SignInCount { get; private set; }

    public int RefreshCount { get; private set; }

    public string? RefreshTokenLastSeen { get; private set; }

    public bool RefusesSignIn { get; set; }

    public bool RefusesRefresh { get; set; }

    public bool CancelsRefresh { get; set; }

    /// <summary>Holds a refresh open, so a test can prove what other callers do meanwhile.</summary>
    public TaskCompletionSource? HoldRefresh { get; set; }

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(10);

    public Task<AuthenticationSession> SignInAsync(CancellationToken cancellationToken)
    {
        SignInCount++;

        return RefusesSignIn
            ? Task.FromException<AuthenticationSession>(new InvalidOperationException("Sign-in refused."))
            : Task.FromResult(Issue());
    }

    public async Task<AuthenticationSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        RefreshCount++;
        RefreshTokenLastSeen = refreshToken;

        // Completes synchronously when nothing is holding it, which is what makes the concurrency
        // test deterministic rather than timing-dependent.
        await (HoldRefresh?.Task ?? Task.CompletedTask).ConfigureAwait(false);

        if (CancelsRefresh)
        {
            throw new OperationCanceledException("The refresh was cancelled.");
        }

        if (RefusesRefresh)
        {
            throw new InvalidOperationException("Refresh refused.");
        }

        return Issue();
    }

    private AuthenticationSession Issue()
    {
        var number = (++_issued).ToString(CultureInfo.InvariantCulture);

        return new AuthenticationSession(
            new AccessToken($"{Fake.AccessTokenValue}-{number}", clock.UtcNow + Lifetime),
            $"{Fake.RefreshTokenValue}-{number}");
    }
}

/// <summary>A token store that lives in a field, and counts what it was asked to do.</summary>
internal sealed class InMemoryTokenStore(AuthenticationSession? initial) : ITokenStore
{
    public AuthenticationSession? Stored { get; private set; } = initial;

    public int Loads { get; private set; }

    public int Saves { get; private set; }

    public int Clears { get; private set; }

    public Task<AuthenticationSession?> LoadAsync(CancellationToken cancellationToken)
    {
        Loads++;
        return Task.FromResult(Stored);
    }

    public Task SaveAsync(AuthenticationSession session, CancellationToken cancellationToken)
    {
        Saves++;
        Stored = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        Clears++;
        Stored = null;
        return Task.CompletedTask;
    }
}

/// <summary>
/// The end of the pipeline. It answers from a script and records what arrived.
/// </summary>
/// <remarks>
/// <b>No request leaves this object.</b> The real <see cref="HttpClient"/> and the real
/// <see cref="AuthenticatingHandler"/> are under test; only the socket is replaced.
/// </remarks>
internal sealed class ScriptedHandler(IReadOnlyList<HttpStatusCode> statuses) : HttpMessageHandler
{
    /// <summary>Per-request context, the way a pipeline passes something down to the socket.</summary>
    public static readonly HttpRequestOptionsKey<string> TenantHint = new("tenant-hint");

    public List<string?> BearerTokens { get; } = [];

    public List<string> Bodies { get; } = [];

    public List<string?> CustomHeaders { get; } = [];

    public List<string?> TenantHints { get; } = [];

    public int Sends { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var index = Sends++;

        BearerTokens.Add(request.Headers.Authorization?.Parameter);
        CustomHeaders.Add(request.Headers.TryGetValues("X-Correlation", out var values) ? values.First() : null);
        TenantHints.Add(request.Options.TryGetValue(ScriptedHandler.TenantHint, out var hint) ? hint : null);
        Bodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));

        var status = index < statuses.Count ? statuses[index] : HttpStatusCode.OK;
        return new HttpResponseMessage(status);
    }
}
