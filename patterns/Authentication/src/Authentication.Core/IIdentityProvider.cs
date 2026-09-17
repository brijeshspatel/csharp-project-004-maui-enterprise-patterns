namespace Authentication.Core;

/// <summary>
/// Whatever establishes who the user is and issues the tokens that say so.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the seam, and it is the whole of the provider as far as this pattern is concerned.</b>
/// A real implementation talks to an identity provider over a browser-based flow; the Microsoft
/// Authentication Library is what does that in a production .NET MAUI application, and it is a
/// package this repository does not carry. Nothing behind this interface is implemented here:
/// <c>Authentication.Core</c> performs no I/O at all, the tests implement it in memory, and the
/// demonstration fabricates a session locally without reaching a network.
/// </para>
/// <para>
/// <b>A refusal is signalled by throwing.</b> A provider that declines to extend a session — a
/// revoked or expired or already-used refresh token — throws, and
/// <see cref="AccessTokenProvider"/> turns that into <see cref="AuthenticationRequiredException"/>
/// after discarding the session.
/// </para>
/// </remarks>
public interface IIdentityProvider
{
    /// <summary>Establish a new session interactively.</summary>
    Task<AuthenticationSession> SignInAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Exchange a refresh token for a new session, without the user being involved.
    /// </summary>
    /// <remarks>
    /// Where refresh tokens are rotated on use, the token passed here is consumed by the call and
    /// the returned session carries its replacement. Presenting a rotated token a second time is
    /// treated by some providers as evidence of theft, and the response is to revoke the whole
    /// family. That is why <see cref="AccessTokenProvider"/> permits exactly one refresh at a time.
    /// </remarks>
    Task<AuthenticationSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

/// <summary>
/// Where a session survives between launches.
/// </summary>
/// <remarks>
/// <para>
/// <b>A token is not a setting.</b> The Application Settings Management entry fenced secrets out by
/// name and pointed here, and this is the other side of that fence: settings belong in
/// <c>Preferences</c>, and a credential belongs in <c>SecureStorage</c>, which is backed by the
/// platform's own key store. <c>Authentication.Core</c> can reference neither — both are MAUI types
/// and this project targets plain <c>net10.0</c> — so the abstraction is declared here and adapted
/// in <c>Authentication.Demo</c>.
/// </para>
/// <para>
/// An implementation is not required to be able to store anything. A store that cannot keep a
/// session returns <see langword="null"/> from <see cref="LoadAsync"/>, and the user signs in
/// again; that is a worse experience, not a broken one.
/// </para>
/// </remarks>
public interface ITokenStore
{
    /// <summary>The stored session, or <see langword="null"/> if there is none to be had.</summary>
    Task<AuthenticationSession?> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Replace whatever is stored with this session.</summary>
    Task SaveAsync(AuthenticationSession session, CancellationToken cancellationToken);

    /// <summary>Remove the stored session.</summary>
    Task ClearAsync(CancellationToken cancellationToken);
}
