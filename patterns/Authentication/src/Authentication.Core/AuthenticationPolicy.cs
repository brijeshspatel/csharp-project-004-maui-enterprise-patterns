namespace Authentication.Core;

/// <summary>
/// How long before a token's stated expiry the client stops trusting it.
/// </summary>
/// <param name="RefreshSkew">
/// The margin. A token is used only while <c>now + RefreshSkew</c> is still before its expiry.
/// </param>
/// <remarks>
/// <para>
/// The margin exists because the client's clock and the server's clock are not the same clock, and
/// because a request takes time to arrive. A token with four hundred milliseconds left passes a
/// naive check and is expired by the time the server reads it.
/// </para>
/// <para>
/// <b>This is an optimisation, not a correctness guarantee.</b> It avoids a round trip that would
/// certainly fail. The guarantee is what happens when the server refuses a token anyway, which it
/// can do at any moment and for reasons no clock predicts — a revocation, a password change, a
/// policy change.
/// </para>
/// </remarks>
public sealed record AuthenticationPolicy(TimeSpan RefreshSkew)
{
    /// <summary>A minute of margin, which is the usual order for a mobile client.</summary>
    public static AuthenticationPolicy Default { get; } = new(TimeSpan.FromMinutes(1));
}

/// <summary>Reads the current time, so that no test waits for one.</summary>
public interface IClock
{
    /// <summary>The current moment, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// The application cannot obtain a token without the user signing in again.
/// </summary>
/// <remarks>
/// This is not a failure to be retried. It is the end of a session: either there was never one, or
/// the provider has refused to extend the one there was. The only thing a caller can usefully do is
/// send the user to sign in.
/// </remarks>
public sealed class AuthenticationRequiredException(string reason, Exception? innerException)
    : Exception($"Interactive sign-in is required. {reason}", innerException)
{
    /// <summary>Why the session could not be established or continued.</summary>
    public string Reason { get; } = reason;
}
