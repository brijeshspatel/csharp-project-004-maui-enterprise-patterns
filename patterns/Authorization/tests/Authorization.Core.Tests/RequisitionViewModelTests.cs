namespace Authorization.Core.Tests;

/// <summary>
/// What the screen offers, and what it says when it will not.
/// </summary>
public class RequisitionViewModelTests
{
    private static (RequisitionViewModel Screen, FakePermissionSource Source, UserCapabilities Capabilities)
        Build(params string[] permissions)
    {
        var source = new FakePermissionSource(Fixture.Granting(permissions));
        var capabilities = new UserCapabilities(source);

        return (
            new RequisitionViewModel(capabilities, new RequisitionAuthorization(capabilities, 5_000m)),
            source,
            capabilities);
    }

    [Fact]
    public async Task WhenApprovalIsAllowed_TheScreenOffersItWithNothingToExplain()
    {
        var (screen, _, _) = Build(Permissions.ApproveRequisition);

        await screen.RefreshCommand.ExecuteAsync(null);
        screen.Requisition = Fixture.Requisition(Fixture.SomebodyElse);

        Assert.True(screen.CanApprove);
        Assert.Equal(string.Empty, screen.ApproveExplanation);
        Assert.Equal("Permissions are current.", screen.Status);

        // The screen names the record and its state, so a user is never deciding about "this one".
        Assert.Contains("REQ-0001", screen.Heading, StringComparison.Ordinal);
        Assert.Contains("submitted", screen.Heading, StringComparison.Ordinal);
        Assert.False(screen.PermissionsAreStale);
    }

    [Fact]
    public async Task WhenApprovalIsRefused_TheScreenSaysWhyRatherThanGoingGrey()
    {
        // A control that is grey for no stated reason is indistinguishable from a broken one.
        var (screen, _, _) = Build(Permissions.ViewRequisition);

        await screen.RefreshCommand.ExecuteAsync(null);
        screen.Requisition = Fixture.Requisition(Fixture.SomebodyElse);

        Assert.False(screen.CanApprove);
        Assert.NotEqual(string.Empty, screen.ApproveExplanation);
    }

    [Fact]
    public async Task TheRaiserIsOfferedCancellationButNotApproval()
    {
        var (screen, _, _) = Build(Permissions.ApproveRequisition);

        await screen.RefreshCommand.ExecuteAsync(null);
        screen.Requisition = Fixture.Requisition(Fixture.ThisUser);

        Assert.False(screen.CanApprove);
        Assert.True(screen.CanCancel);
        Assert.Equal(string.Empty, screen.CancelExplanation);
    }

    [Fact]
    public async Task WhenTheServerCannotBeReached_TheScreenSaysSoAndKeepsWorking()
    {
        var (screen, source, _) = Build(Permissions.ApproveRequisition);

        await screen.RefreshCommand.ExecuteAsync(null);
        screen.Requisition = Fixture.Requisition(Fixture.SomebodyElse);
        Assert.True(screen.CanApprove);

        source.IsUnreachable = true;
        await screen.RefreshCommand.ExecuteAsync(null);

        // Still offered, because the server enforces and denying here would buy nothing.
        Assert.True(screen.CanApprove);
        Assert.True(screen.PermissionsAreStale);
        Assert.Contains("out of date", screen.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenTheSetIsDiscardedMidRefresh_TheScreenAsksForAnotherPress()
    {
        // Not retried automatically: a server that is actively revoking could supersede every
        // attempt, and a silent loop is worse than a sentence asking for one more press.
        var (screen, source, capabilities) = Build(Permissions.ApproveRequisition);
        source.WhileLoading = capabilities.Invalidate;

        await screen.RefreshCommand.ExecuteAsync(null);
        screen.Requisition = Fixture.Requisition(Fixture.SomebodyElse);

        Assert.False(screen.CanApprove);
        Assert.Contains("Refresh again", screen.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoRequisitionOnScreen_NothingIsOffered()
    {
        var (screen, _, _) = Build(Permissions.ApproveRequisition);

        await screen.RefreshCommand.ExecuteAsync(null);
        screen.Requisition = Fixture.Requisition(Fixture.SomebodyElse);
        Assert.True(screen.CanApprove);

        screen.Requisition = null;

        Assert.False(screen.CanApprove);
        Assert.False(screen.CanCancel);
        Assert.Equal(string.Empty, screen.ApproveExplanation);
        Assert.Equal(string.Empty, screen.CancelExplanation);
        Assert.Equal("No requisition.", screen.Heading);
    }
}
