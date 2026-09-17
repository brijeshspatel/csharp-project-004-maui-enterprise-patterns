namespace Authorization.Core.Tests;

/// <summary>
/// The permission set the application works from, and three different kinds of not knowing.
/// </summary>
public class UserCapabilitiesTests
{
    [Fact]
    public void AGrantedPermissionIsAllowed_AndAnUnknownOneIsNot()
    {
        var granted = Fixture.Granting(Permissions.ApproveRequisition);

        Assert.True(granted.Allows(Permissions.ApproveRequisition));
        Assert.False(granted.Allows("requisition.delete"));
    }

    [Fact]
    public void TheEmptySetGrantsNothing_AndKnowsNobody()
    {
        Assert.False(PermissionSet.None.Allows(Permissions.ApproveRequisition));

        // Null rather than empty, so it cannot accidentally equal a record's owner.
        Assert.Null(PermissionSet.None.SubjectId);
    }

    [Fact]
    public void APermissionDifferingOnlyInCaseIsDenied()
    {
        // Ordinal. Two systems whose names differ only in case are two systems, and matching them
        // loosely is how one system's permission silently becomes another's.
        var granted = Fixture.Granting("Requisition.Approve");

        Assert.False(granted.Allows("requisition.approve"));
    }

    [Fact]
    public void BeforeAnythingHasBeenLoaded_NothingIsAllowed()
    {
        // The one case that IS fail-closed: offering a capability nobody has described is guessing.
        var capabilities = new UserCapabilities(new FakePermissionSource(PermissionSet.None));

        Assert.False(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.Null(capabilities.SubjectId);
        Assert.False(capabilities.IsStale);
    }

    [Fact]
    public async Task AfterASuccessfulRefresh_TheGrantedPermissionsAreAllowed()
    {
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition));
        var capabilities = new UserCapabilities(source);

        var outcome = await capabilities.RefreshAsync(CancellationToken.None);

        Assert.Equal(RefreshOutcome.Loaded, outcome);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.Equal(Fixture.ThisUser, capabilities.SubjectId);
        Assert.False(capabilities.IsStale);
    }

    [Fact]
    public async Task AFailedRefreshKeepsWhatIsHeld_AndReportsItStale()
    {
        // THE DECISION THIS ENTRY TURNS ON. These checks are not a security control -- the server
        // enforces -- so denying everything because a refresh failed buys no security at all and
        // costs the user every affordance in the application.
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition));
        var capabilities = new UserCapabilities(source);

        await capabilities.RefreshAsync(CancellationToken.None);
        source.IsUnreachable = true;

        var outcome = await capabilities.RefreshAsync(CancellationToken.None);

        Assert.Equal(RefreshOutcome.Failed, outcome);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.True(capabilities.IsStale);
    }

    [Fact]
    public async Task AFailedFirstLoadLeavesEverythingDenied()
    {
        // Absence of evidence. Nothing was ever held, so nothing is offered.
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition))
        {
            IsUnreachable = true,
        };
        var capabilities = new UserCapabilities(source);

        var outcome = await capabilities.RefreshAsync(CancellationToken.None);

        Assert.Equal(RefreshOutcome.Failed, outcome);
        Assert.False(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.True(capabilities.IsStale);
    }

    [Fact]
    public async Task ASuccessfulRefreshAfterAFailure_ClearsTheStaleMark()
    {
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition))
        {
            IsUnreachable = true,
        };
        var capabilities = new UserCapabilities(source);

        await capabilities.RefreshAsync(CancellationToken.None);
        Assert.True(capabilities.IsStale);

        source.IsUnreachable = false;
        await capabilities.RefreshAsync(CancellationToken.None);

        Assert.False(capabilities.IsStale);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));
    }

    [Fact]
    public async Task InvalidateDiscardsEverything_AndIsNotReportedAsStaleness()
    {
        // Contrary evidence, not absence of it. Stale means "possibly out of date"; this means
        // "known wrong", and the two must not look the same to a screen.
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition));
        var capabilities = new UserCapabilities(source);

        await capabilities.RefreshAsync(CancellationToken.None);
        capabilities.Invalidate();

        Assert.False(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.Null(capabilities.SubjectId);
        Assert.False(capabilities.IsStale);
    }

    [Fact]
    public async Task ARefreshThatWasInFlightWhenTheSetWasDiscarded_DoesNotReinstateIt()
    {
        // THE RACE THE SPECIFICATION REVIEW CAUGHT. A refresh is in flight; a 403 arrives on another
        // request and discards the set, correctly; then the refresh completes carrying the
        // permissions AS THEY WERE BEFORE the revocation. Installing them would put the application
        // back to believing exactly what the server has just refused, and nothing would correct it.
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition));
        var capabilities = new UserCapabilities(source);

        // Interleaved inside the load itself, so the ordering is by construction rather than by
        // timing. No threads are raced.
        source.WhileLoading = capabilities.Invalidate;

        var outcome = await capabilities.RefreshAsync(CancellationToken.None);

        Assert.Equal(RefreshOutcome.Superseded, outcome);
        Assert.False(capabilities.Allows(Permissions.ApproveRequisition));
        Assert.Null(capabilities.SubjectId);
    }

    [Fact]
    public async Task ARefreshAfterTheDiscardInstallsNormally()
    {
        // The generation check drops a stale answer. It must not wedge the mechanism: the next
        // refresh, which starts after the discard, is current and is installed.
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition));
        var capabilities = new UserCapabilities(source);

        source.WhileLoading = capabilities.Invalidate;
        await capabilities.RefreshAsync(CancellationToken.None);

        source.WhileLoading = null;
        var outcome = await capabilities.RefreshAsync(CancellationToken.None);

        Assert.Equal(RefreshOutcome.Loaded, outcome);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));
    }

    [Fact]
    public async Task ACancelledRefreshIsNotTreatedAsAFailedOne()
    {
        // A cancellation is the caller changing its mind, not the server being unreachable, and it
        // must not mark a perfectly current set as stale.
        var source = new FakePermissionSource(Fixture.Granting(Permissions.ApproveRequisition));
        var capabilities = new UserCapabilities(source);
        await capabilities.RefreshAsync(CancellationToken.None);

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        source.WhileLoading = () => cancelled.Token.ThrowIfCancellationRequested();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => capabilities.RefreshAsync(cancelled.Token));

        Assert.False(capabilities.IsStale);
        Assert.True(capabilities.Allows(Permissions.ApproveRequisition));
    }
}
