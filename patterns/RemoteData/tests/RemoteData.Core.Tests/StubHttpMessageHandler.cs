using System.Net;

namespace RemoteData.Core.Tests;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that answers however a test needs.
/// </summary>
/// <remarks>
/// This is the framework's own seam, so the code under test is the real <see cref="HttpClient"/> —
/// its timeout, its status handling and its cancellation, rather than a stand-in for them.
/// </remarks>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

    private StubHttpMessageHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _respond = respond;
    }

    /// <summary>
    /// The token the handler was actually given.
    /// </summary>
    /// <remarks>
    /// Recorded so a test can assert it is the caller's own token, rather than merely that some
    /// token arrived — which would be true of <see cref="CancellationToken.None"/> too.
    /// </remarks>
    public CancellationToken ReceivedToken { get; private set; }

    public static StubHttpMessageHandler Returning(HttpStatusCode status, string body) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        }));

    /// <summary>Fails the way an unreachable host does.</summary>
    public static StubHttpMessageHandler Unreachable(string message) =>
        new(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException(message)));

    /// <summary>Never answers, so the client's timeout or the caller's token decides.</summary>
    public static StubHttpMessageHandler Hanging() =>
        new(async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ReceivedToken = cancellationToken;
        return _respond(cancellationToken);
    }
}
