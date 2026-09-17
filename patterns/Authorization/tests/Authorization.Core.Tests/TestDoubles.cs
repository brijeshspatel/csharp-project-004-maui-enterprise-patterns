using System.Net;

namespace Authorization.Core.Tests;

/// <summary>Fixture values. Nothing here is a credential, and nothing here reaches anything.</summary>
internal static class Fixture
{
    public const string ThisUser = "fixture-subject-this-user";
    public const string SomebodyElse = "fixture-subject-somebody-else";

    /// <summary>The reserved TLD from RFC 2606. It cannot resolve, and nothing here sends to it.</summary>
    public static readonly Uri Endpoint = new("https://service.invalid/requisitions/4471");

    public static PermissionSet Granting(params string[] permissions) => new(ThisUser, permissions);

    public static PurchaseRequisition Requisition(
        string raisedBy,
        decimal amount = 1_000m,
        RequisitionState state = RequisitionState.Submitted) =>
        new("REQ-0001", raisedBy, amount, state);
}

/// <summary>
/// Stands in for the server's answer, and counts what it was asked.
/// </summary>
internal sealed class FakePermissionSource(PermissionSet answer) : IPermissionSource
{
    public int Loads { get; private set; }

    public PermissionSet Answer { get; set; } = answer;

    public bool IsUnreachable { get; set; }

    /// <summary>Holds a load open, so a test can prove what happens to a late answer.</summary>
    public TaskCompletionSource? HoldLoad { get; set; }

    /// <summary>Runs while the load is held, so a test can interleave without racing threads.</summary>
    public Action? WhileLoading { get; set; }

    public async Task<PermissionSet> LoadAsync(CancellationToken cancellationToken)
    {
        Loads++;
        WhileLoading?.Invoke();

        // Completes synchronously when nothing is holding it, which keeps every test deterministic.
        await (HoldLoad?.Task ?? Task.CompletedTask).ConfigureAwait(false);

        if (IsUnreachable)
        {
            throw new InvalidOperationException("The fake permission source is unreachable.");
        }

        return Answer;
    }
}

/// <summary>
/// The end of the pipeline. It answers from a script and records what arrived.
/// </summary>
/// <remarks>
/// <b>No request leaves this object.</b> The real <see cref="HttpClient"/> and the real
/// <see cref="ForbiddenResponseHandler"/> are under test; only the socket is replaced.
/// </remarks>
internal sealed class ScriptedHandler(IReadOnlyList<HttpStatusCode> statuses) : HttpMessageHandler
{
    public int Sends { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var index = Sends++;
        var status = index < statuses.Count ? statuses[index] : HttpStatusCode.OK;

        return Task.FromResult(new HttpResponseMessage(status));
    }
}
