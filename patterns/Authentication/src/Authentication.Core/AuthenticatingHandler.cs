using System.Net;
using System.Net.Http.Headers;

namespace Authentication.Core;

/// <summary>
/// Attaches the access token to every outgoing request, and recovers once when the server refuses
/// it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two mechanisms, and only one of them is a correctness guarantee.</b> Refreshing before expiry
/// (<see cref="AuthenticationPolicy.RefreshSkew"/>) is an optimisation: it avoids a round trip that
/// would certainly fail. Refreshing after a <c>401</c> is the guarantee, because the server is the
/// authority on whether a token is acceptable and it can refuse for reasons no clock predicts.
/// </para>
/// <para>
/// <b>A request already in flight when the token expires cannot be recalled.</b> That case is not
/// prevented, it is handled — by the second mechanism, here. An implementation that checks expiry
/// and nothing else is wrong, and looks correct in every test where the clocks agree.
/// </para>
/// <para>
/// This type is in <c>Authentication.Core</c> because <c>System.Net.Http</c> is in the shared
/// framework. No package and no MAUI type is involved.
/// </para>
/// </remarks>
public sealed class AuthenticatingHandler(AccessTokenProvider tokens) : DelegatingHandler
{
    private const string BearerScheme = "Bearer";

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Before the first send, because the first send consumes the content stream and a stream
        // cannot be read twice. ByteArrayContent can be read as often as the resend needs.
        await BufferContentAsync(request, cancellationToken).ConfigureAwait(false);

        var token = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, token.Value);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Keyed on the token that failed, so two requests refused on the same token cause one
        // refresh between them rather than one each.
        var refreshed = await tokens.RefreshAfterRejectionAsync(token, cancellationToken).ConfigureAwait(false);

        response.Dispose();

        var replay = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
        replay.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, refreshed.Value);

        // EXACTLY ONE RESEND. A 401 on this response is returned as it stands. The token it carried
        // was minted seconds earlier, so a second refusal is the server declining the identity
        // rather than the credential, and asking again would be a loop that ends when something
        // else gives out.
        return await base.SendAsync(replay, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Replace a request's content with a copy that can be read more than once.</summary>
    private static async Task BufferContentAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return;
        }

        var original = request.Content;
        var body = await original.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        request.Content = CopyOf(body, original.Headers);
    }

    /// <summary>
    /// A second request carrying the same call.
    /// </summary>
    /// <remarks>
    /// <b>The clone is not optional.</b> An <see cref="HttpRequestMessage"/> cannot be sent twice —
    /// the second send throws <see cref="InvalidOperationException"/>. Method, URI, version,
    /// headers, options and content are all carried across; a resend that silently loses the
    /// request body is a defect that only appears on a <c>POST</c>.
    /// </remarks>
    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        if (request.Content is not null)
        {
            // Safe to read a second time only because BufferContentAsync replaced the content
            // before the first send.
            var body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            clone.Content = CopyOf(body, request.Content.Headers);
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in (IDictionary<string, object?>)request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return clone;
    }

    private static ByteArrayContent CopyOf(byte[] body, HttpContentHeaders headers)
    {
        var content = new ByteArrayContent(body);
        content.Headers.Clear();

        foreach (var header in headers)
        {
            content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return content;
    }
}
