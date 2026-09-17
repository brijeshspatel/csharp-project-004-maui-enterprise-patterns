namespace Authentication.Core;

/// <summary>
/// Holds the session, hands out access tokens, and refreshes exactly once however many callers
/// arrive at the same moment.
/// </summary>
/// <remarks>
/// <para>
/// <b>This type is thread-safe, and the Circuit Breaker entry's is deliberately not.</b> That
/// difference is not an inconsistency. A breaker in this repository is driven from a screen, on one
/// UI thread. A token is fetched inside an HTTP message handler, which runs on whatever thread the
/// HTTP stack hands it, and several screens issuing requests at once is the normal case rather than
/// the exotic one.
/// </para>
/// <para>
/// <b>The gate is a <see cref="SemaphoreSlim"/> rather than a lock</b>, because a lock cannot be
/// held across an <c>await</c> and every operation here awaits.
/// </para>
/// </remarks>
public sealed class AccessTokenProvider(
    IIdentityProvider identityProvider,
    ITokenStore tokenStore,
    IClock clock,
    AuthenticationPolicy policy) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// The current session, written inside the gate and read outside it by the fast path.
    /// </summary>
    /// <remarks>
    /// <b><c>volatile</c> is about visibility, not staleness.</b> Two different concerns are easily
    /// confused here. A <i>stale</i> read is harmless: the caller takes the gate, re-checks, and
    /// finds whatever the last writer installed. An <i>invisible</i> write is not harmless, and
    /// ECMA-335 does not promise that an ordinary write becomes visible to a thread that never
    /// takes the gate — which is precisely what the fast path is. This costs nothing measurable and
    /// removes a dependency on a memory model stronger than the one that is actually specified.
    /// </remarks>
    private volatile AuthenticationSession? _session;

    /// <summary>Whether the store has been read. Touched only inside the gate.</summary>
    private bool _storeConsulted;

    /// <summary>Whether a session is currently held. Cheap, and safe to read from anywhere.</summary>
    public bool IsSignedIn => _session is not null;

    /// <summary>
    /// A token that can be attached to a request, refreshing first if the held one is spent.
    /// </summary>
    /// <exception cref="AuthenticationRequiredException">
    /// There is no session, or the provider refused to extend the one there was.
    /// </exception>
    public async Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        // The fast path, and the reason the gate is not taken on every call. An unguarded read is
        // safe here because the worst outcome is a redundant trip through the gate below.
        var cached = _session;
        if (cached is not null && cached.AccessToken.IsUsableAt(clock.UtcNow, policy.RefreshSkew))
        {
            return cached.AccessToken;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await CurrentSessionAsync(cancellationToken).ConfigureAwait(false);

            // THE RE-CHECK, and it is the whole of the single-flight guarantee. A gate that only
            // serialises still performs every refresh, one after another politely. The second
            // caller through finds the token the first one installed and takes it.
            if (session is not null && session.AccessToken.IsUsableAt(clock.UtcNow, policy.RefreshSkew))
            {
                return session.AccessToken;
            }

            if (session is null)
            {
                throw new AuthenticationRequiredException("There is no session to use.", null);
            }

            var refreshed = await RefreshUnderGateAsync(session, cancellationToken).ConfigureAwait(false);
            return refreshed.AccessToken;
        }
        finally
        {
            // Released on every path, including a cancellation and a refusal. A gate left held
            // would block every later call for the life of the process while nothing looked wrong.
            _gate.Release();
        }
    }

    /// <summary>
    /// Obtain a token after the server has refused one, refreshing only if nobody else already has.
    /// </summary>
    /// <param name="rejected">The token the server refused.</param>
    /// <remarks>
    /// <b>Keyed on the token that failed</b>, which is the second half of the single-flight rule.
    /// Two requests that both fail on the same token must cause one refresh: the second arrives
    /// here, finds the current token is no longer the one it failed with, and takes the new one
    /// without asking the provider for anything.
    /// </remarks>
    public async Task<AccessToken> RefreshAfterRejectionAsync(AccessToken rejected, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await CurrentSessionAsync(cancellationToken).ConfigureAwait(false);

            if (session is null)
            {
                throw new AuthenticationRequiredException(
                    "The session ended while a request was in flight.", null);
            }

            if (session.AccessToken != rejected)
            {
                return session.AccessToken;
            }

            var refreshed = await RefreshUnderGateAsync(session, cancellationToken).ConfigureAwait(false);
            return refreshed.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Establish a session interactively, replacing any that is held.</summary>
    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await identityProvider.SignInAsync(cancellationToken).ConfigureAwait(false);
            await KeepSessionAsync(session, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>End the session, here and in storage.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DiscardSessionAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>The held session, consulting the store the first time it is asked for.</summary>
    private async Task<AuthenticationSession?> CurrentSessionAsync(CancellationToken cancellationToken)
    {
        if (!_storeConsulted)
        {
            _session = await tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);

            // Set after the load rather than before it, so that a store which failed transiently
            // is asked again rather than the application concluding for ever that there is nothing
            // stored.
            _storeConsulted = true;
        }

        return _session;
    }

    /// <summary>Exchange the refresh token. The caller holds the gate.</summary>
    private async Task<AuthenticationSession> RefreshUnderGateAsync(
        AuthenticationSession session,
        CancellationToken cancellationToken)
    {
        AuthenticationSession refreshed;
        try
        {
            refreshed = await identityProvider
                .RefreshAsync(session.RefreshToken, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // The provider refused. Everything derived from that refresh token is now worthless,
            // INCLUDING the access token still held in memory -- which on the rejection path has
            // not expired and passes every check this client can make, while the server has
            // already refused it. Both halves go, together, which is what DiscardSessionAsync is
            // for.
            //
            // CancellationToken.None deliberately: the caller asking to stop must not leave a
            // refused session half-discarded.
            await DiscardSessionAsync(CancellationToken.None).ConfigureAwait(false);

            throw new AuthenticationRequiredException("The session could not be refreshed.", failure);
        }

        await KeepSessionAsync(refreshed, cancellationToken).ConfigureAwait(false);
        return refreshed;
    }

    /// <summary>Hold a session and store it. The caller holds the gate.</summary>
    private async Task KeepSessionAsync(AuthenticationSession session, CancellationToken cancellationToken)
    {
        _session = session;
        _storeConsulted = true;
        await tokenStore.SaveAsync(session, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The only way a session ends. Memory and storage, together, always.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three different paths end a session — a sign-out, a refusal reached through expiry, and a
    /// refusal reached through the server rejecting a token. Each of them calls this. <b>None of
    /// them clears one half and remembers to clear the other</b>, because that is the defect this
    /// exists to make impossible: clearing storage while keeping the in-memory copy produces an
    /// application that keeps presenting a dead credential until it is next launched, and looks
    /// clean the whole time because the store is empty.
    /// </para>
    /// <para>
    /// Memory is cleared first, so that a store which throws on the way out still leaves nothing
    /// usable behind.
    /// </para>
    /// </remarks>
    private async Task DiscardSessionAsync(CancellationToken cancellationToken)
    {
        _session = null;
        _storeConsulted = true;
        await tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
    }
}
