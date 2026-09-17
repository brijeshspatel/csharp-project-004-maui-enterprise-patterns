namespace Authorization.Core.Tests;

/// <summary>
/// A permission is not a decision: the resource decides too, and the order of the refusals is
/// user-visible.
/// </summary>
public class RequisitionAuthorizationTests
{
    private const decimal Limit = 5_000m;

    private static async Task<RequisitionAuthorization> BuildAsync(params string[] permissions)
    {
        var capabilities = new UserCapabilities(new FakePermissionSource(Fixture.Granting(permissions)));
        await capabilities.RefreshAsync(CancellationToken.None);

        return new RequisitionAuthorization(capabilities, Limit);
    }

    [Fact]
    public async Task WithoutThePermission_ApprovalIsRefused()
    {
        var authorization = await BuildAsync(Permissions.ViewRequisition);

        var decision = authorization.CanApprove(Fixture.Requisition(Fixture.SomebodyElse));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.NotPermitted, decision.Reason);
        Assert.NotEqual(string.Empty, decision.Explanation);
    }

    [Fact]
    public async Task ARequisitionTheSubjectRaised_IsRefusedEvenWithThePermission()
    {
        // The most common real authorisation defect, in its most common form. The permission bit
        // says yes; the resource says no.
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(Fixture.Requisition(Fixture.ThisUser));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.OwnRequisition, decision.Reason);
    }

    [Fact]
    public async Task AnAmountAboveTheLimitIsRefused()
    {
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(
            Fixture.Requisition(Fixture.SomebodyElse, amount: Limit + 0.01m));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.AboveApprovalLimit, decision.Reason);
    }

    [Fact]
    public async Task AnAmountExactlyAtTheLimitIsAllowed()
    {
        // The boundary is inclusive, and it is stated rather than discovered.
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(Fixture.Requisition(Fixture.SomebodyElse, amount: Limit));

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task ARequisitionThatIsNotSubmittedIsRefused()
    {
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(
            Fixture.Requisition(Fixture.SomebodyElse, state: RequisitionState.Approved));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.WrongState, decision.Reason);
        Assert.Contains("already approved", decision.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AStateThisBuildHasNeverHeardOfIsRefused()
    {
        // A server can add a state, and a build that has not been updated will receive it. The same
        // default-deny rule that applies to an unknown permission applies here.
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(
            Fixture.Requisition(Fixture.SomebodyElse, state: (RequisitionState)99));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.WrongState, decision.Reason);
        Assert.Contains("does not recognise", decision.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EverythingSatisfied_IsAllowedAndCarriesNoReason()
    {
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(Fixture.Requisition(Fixture.SomebodyElse));

        Assert.True(decision.IsAllowed);
        Assert.Equal(DenialReason.None, decision.Reason);
        Assert.Equal(string.Empty, decision.Explanation);
    }

    [Fact]
    public async Task FailingThreeConditionsAtOnce_TheSubjectIsToldAboutThePermission()
    {
        // THE ORDER TEST. Without the permission, on a requisition this subject raised, for an
        // amount above the limit. Telling them about the limit would be wrong, would invite them to
        // ask for a limit that changes nothing, and would disclose the limit to somebody with no
        // approval role at all.
        var authorization = await BuildAsync(Permissions.ViewRequisition);

        var decision = authorization.CanApprove(
            Fixture.Requisition(Fixture.ThisUser, amount: Limit + 1_000m, state: RequisitionState.Draft));

        Assert.Equal(DenialReason.NotPermitted, decision.Reason);
    }

    [Fact]
    public async Task WithThePermission_OwnRequisitionIsReportedBeforeTheLimit()
    {
        // The second rung of the same ladder: the resource relationship is a more fundamental
        // refusal than the amount, and saying "above your limit" would imply a higher limit helps.
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(
            Fixture.Requisition(Fixture.ThisUser, amount: Limit + 1_000m));

        Assert.Equal(DenialReason.OwnRequisition, decision.Reason);
    }

    [Fact]
    public async Task TheRaiserMayCancel_WithNoPermissionAtAll()
    {
        // Authorisation is a relationship between a subject and a resource, not a bit. No permission
        // table would ever say "may withdraw the thing they themselves raised".
        var authorization = await BuildAsync();

        var decision = authorization.CanCancel(Fixture.Requisition(Fixture.ThisUser));

        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task SomebodyElsesRequisitionCannotBeCancelled_EvenWithEveryPermission()
    {
        var authorization = await BuildAsync(Permissions.ApproveRequisition, Permissions.ViewRequisition);

        var decision = authorization.CanCancel(Fixture.Requisition(Fixture.SomebodyElse));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.NotRaiser, decision.Reason);
    }

    [Fact]
    public async Task TheRaiserCannotCancelOneThatIsAlreadyApproved()
    {
        var authorization = await BuildAsync();

        var decision = authorization.CanCancel(
            Fixture.Requisition(Fixture.ThisUser, state: RequisitionState.Approved));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.WrongState, decision.Reason);
    }

    [Fact]
    public async Task ADraftCannotBeApproved_AndTheReasonSaysWhatItIs()
    {
        var authorization = await BuildAsync(Permissions.ApproveRequisition);

        var decision = authorization.CanApprove(
            Fixture.Requisition(Fixture.SomebodyElse, state: RequisitionState.Draft));

        Assert.Equal(DenialReason.WrongState, decision.Reason);
        Assert.Contains("still a draft", decision.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACancelledRequisitionCannotBeCancelledAgain()
    {
        var authorization = await BuildAsync();

        var decision = authorization.CanCancel(
            Fixture.Requisition(Fixture.ThisUser, state: RequisitionState.Cancelled));

        Assert.Equal(DenialReason.WrongState, decision.Reason);
        Assert.Contains("cancelled", decision.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNobodyKnown_NothingIsTheSubjectsOwn()
    {
        // PermissionSet.None's subject is null rather than empty, so it matches no owner. An empty
        // string equals an empty string, and "the unknown user owns the record nobody owns" is a
        // defect nobody reads twice.
        var capabilities = new UserCapabilities(new FakePermissionSource(PermissionSet.None));
        var authorization = new RequisitionAuthorization(capabilities, Limit);

        var decision = authorization.CanCancel(Fixture.Requisition(string.Empty));

        Assert.False(decision.IsAllowed);
        Assert.Equal(DenialReason.NotRaiser, decision.Reason);
    }
}
