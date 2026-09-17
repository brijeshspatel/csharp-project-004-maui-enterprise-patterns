namespace Authentication.Core;

/// <summary>
/// Everything the application holds on behalf of a signed-in user: the credential it presents, and
/// the credential it uses to obtain the next one.
/// </summary>
/// <remarks>
/// <para>
/// The two are discarded together and are never held apart. A refresh token without an access token
/// is a session waiting to resume; an access token without a refresh token is a session that will
/// end silently the moment it expires.
/// </para>
/// <para>
/// It redacts itself for the same reason <see cref="AccessToken"/> does, and the refresh token is
/// the more sensitive of the two: an access token expires by itself, and a refresh token is how a
/// thief keeps getting new ones.
/// </para>
/// </remarks>
public sealed record AuthenticationSession(AccessToken AccessToken, string RefreshToken)
{
    /// <summary>The shape and the access token's expiry. Neither secret.</summary>
    public override string ToString() => $"AuthenticationSession(refresh token redacted, {AccessToken})";
}
