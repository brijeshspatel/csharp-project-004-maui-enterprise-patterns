using System.Globalization;

namespace Authentication.Core;

/// <summary>
/// A bearer credential, and the moment it stops being accepted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The value is opaque.</b> This client never parses it, never reads claims from it and never
/// validates its signature. The expiry comes from what the provider said when it issued the token,
/// not from a field extracted out of it. Reading claims is a different catalogue entry, from a
/// source that entry will have to justify.
/// </para>
/// <para>
/// <b>It redacts itself.</b> A positional record's generated <c>ToString()</c> prints every
/// property, so a type carrying a credential has to override it. The disclosure this prevents is
/// not <c>Console.WriteLine(token.Value)</c> — nobody writes that. It is <c>$"token: {token}"</c>,
/// written by someone who was being careful, in a log line or an exception message or a crash
/// report.
/// </para>
/// </remarks>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt)
{
    /// <summary>
    /// Whether this token can still be used, allowing a margin before it expires.
    /// </summary>
    /// <remarks>
    /// The boundary is <b>exclusive</b>: a token that expires exactly at the margin is treated as
    /// finished. "Before expiry" without saying whether the boundary counts is how off-by-one gets
    /// in, so it is stated here and tested one tick either side.
    /// </remarks>
    public bool IsUsableAt(DateTimeOffset now, TimeSpan refreshSkew) => now + refreshSkew < ExpiresAt;

    /// <summary>The shape and the expiry. Never the value.</summary>
    /// <remarks>
    /// The timestamp uses <see cref="CultureInfo.InvariantCulture"/> because this string is read by
    /// a developer or a log parser. The contrast with the Circuit Breaker entry is deliberate:
    /// that one formats with <c>CultureInfo.CurrentCulture</c> because a user reads it. Neither is
    /// an oversight.
    /// </remarks>
    public override string ToString() =>
        $"AccessToken(value redacted, expires {ExpiresAt.ToString("O", CultureInfo.InvariantCulture)})";
}
