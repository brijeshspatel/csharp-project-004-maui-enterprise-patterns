using System.Net;

namespace Authorization.Core;

/// <summary>
/// Notices when the server refuses an operation, and stops the application claiming to know what it
/// may do.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>401</c> and <c>403</c> are not the same news.</b> A <c>401</c> says <i>who you are</i> is
/// not accepted, and belongs to the Authentication entry, which refreshes the token and resends
/// once. A <c>403</c> says who you are is accepted and <i>what you asked for</i> is not — so the
/// thing that is wrong is this application's idea of its own permissions.
/// </para>
/// <para>
/// <b>A <c>403</c> is never retried.</b> The same request with the same identity will be refused
/// again, and asking twice is a loop with a slower answer. It is one of the statuses the Retry entry
/// already names as not retryable.
/// </para>
/// <para>
/// <b>And a <c>403</c> is a signal, not routine traffic.</b> In a client that shapes its own
/// interface, a user should not be able to press a button that produces one. When one arrives it is
/// exactly one of three things — a stale permission set, a screen that offered something it should
/// not have, or a request sent with no check in front of it. <b>Two of those three are defects in
/// the client</b>, which is why this handler counts them rather than correcting the display and
/// saying nothing.
/// </para>
/// </remarks>
public sealed class ForbiddenResponseHandler(UserCapabilities capabilities) : DelegatingHandler
{
    private int _forbiddenResponses;

    /// <summary>How many refusals have been seen. In a correct client this stays at zero.</summary>
    public int ForbiddenResponses => Volatile.Read(ref _forbiddenResponses);

    /// <summary>The path of the most recent refusal, so a developer can find the screen.</summary>
    public string? LastForbiddenPath { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A 401 is deliberately left alone. It is about identity, not permission, and invalidating
        // the permission set because a token expired would throw away something that is not wrong.
        if (response.StatusCode != HttpStatusCode.Forbidden)
        {
            return response;
        }

        Interlocked.Increment(ref _forbiddenResponses);
        LastForbiddenPath = request.RequestUri?.AbsolutePath;

        capabilities.Invalidate();

        // Returned exactly as it arrived. A refusal is a legitimate HTTP outcome and the caller is
        // entitled to see it; swallowing it here would leave the screen waiting for a result that
        // never comes.
        return response;
    }
}
