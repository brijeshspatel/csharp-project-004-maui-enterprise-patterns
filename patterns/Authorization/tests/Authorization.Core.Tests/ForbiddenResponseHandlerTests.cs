using System.Net;

namespace Authorization.Core.Tests;

/// <summary>
/// What the client does when the server contradicts it.
/// </summary>
/// <remarks>
/// The real <see cref="HttpClient"/> and the real <see cref="ForbiddenResponseHandler"/> are under
/// test. Only the socket is replaced, and the address used is in the reserved <c>.invalid</c>
/// top-level domain, which cannot resolve.
/// </remarks>
public class ForbiddenResponseHandlerTests
{
    private static async Task<(HttpClient Client, ScriptedHandler Inner, UserCapabilities Capabilities, ForbiddenResponseHandler Handler)>
        BuildAsync(params HttpStatusCode[] statuses)
    {
        var capabilities = new UserCapabilities(
            new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition)));
        await capabilities.RefreshAsync(CancellationToken.None);

        var inner = new ScriptedHandler(statuses);
        var handler = new ForbiddenResponseHandler(capabilities) { InnerHandler = inner };

        return (new HttpClient(handler), inner, capabilities, handler);
    }

    [Fact]
    public async Task A403DiscardsThePermissionSet()
    {
        var (client, _, capabilities, _) = await BuildAsync(HttpStatusCode.Forbidden);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));

        await client.GetAsync(Fixture.Endpoint);

        Assert.False(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.Null(capabilities.SubjectId);

        // Discarded, not marked stale. The two mean different things and a screen must be able to
        // tell them apart.
        Assert.False(capabilities.IsStale);
    }

    [Fact]
    public async Task A403IsReturnedUnchangedAndSentExactlyOnce()
    {
        var (client, inner, _, _) = await BuildAsync(HttpStatusCode.Forbidden);

        var response = await client.GetAsync(Fixture.Endpoint);

        // Never retried. The same request with the same identity will be refused again, and asking
        // twice is a loop with a slower answer.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, inner.Sends);
    }

    [Fact]
    public async Task A403IsRecordedRatherThanCorrectedSilently()
    {
        // In a client that shapes its own interface, a user should not be able to press a button
        // that produces a 403. When one arrives it is a stale permission set, a screen that offered
        // what it should not have, or a request with no check in front of it -- and two of those
        // three are defects here.
        var (client, _, _, handler) = await BuildAsync(HttpStatusCode.Forbidden);
        Assert.Equal(0, handler.ForbiddenResponses);

        await client.GetAsync(Fixture.Endpoint);

        Assert.Equal(1, handler.ForbiddenResponses);
        Assert.Equal("/requisitions/4471", handler.LastForbiddenPath);
    }

    [Fact]
    public async Task ASuccessfulResponseChangesNothing()
    {
        var (client, _, capabilities, handler) = await BuildAsync(HttpStatusCode.OK);

        var response = await client.GetAsync(Fixture.Endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.Equal(0, handler.ForbiddenResponses);
        Assert.Null(handler.LastForbiddenPath);
    }

    [Fact]
    public async Task A401DoesNotDiscardThePermissionSet()
    {
        // A 401 is about identity, and it is the Authentication entry's business. Throwing away the
        // permission set because a token expired would discard something that is not wrong.
        var (client, _, capabilities, handler) = await BuildAsync(HttpStatusCode.Unauthorized);

        var response = await client.GetAsync(Fixture.Endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.Equal(0, handler.ForbiddenResponses);
    }
}
