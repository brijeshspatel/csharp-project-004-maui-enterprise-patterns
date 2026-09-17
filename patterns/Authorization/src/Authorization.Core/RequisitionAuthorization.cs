using System.Globalization;

namespace Authorization.Core;

/// <summary>Where a purchase requisition has got to.</summary>
public enum RequisitionState
{
    /// <summary>Being written, and not yet anybody else's business.</summary>
    Draft,

    /// <summary>Waiting for somebody to approve it.</summary>
    Submitted,

    /// <summary>Approved.</summary>
    Approved,

    /// <summary>Withdrawn by the person who raised it.</summary>
    Cancelled,
}

/// <summary>The resource an authorisation decision is about.</summary>
public sealed record PurchaseRequisition(
    string Reference,
    string RaisedBy,
    decimal Amount,
    RequisitionState State)
{
    /// <summary>The state in words, including a state this build has never heard of.</summary>
    /// <remarks>
    /// <para>
    /// The last arm is not defensive padding. A server can add a state, and a build that has not
    /// been updated will receive it. <b>An unknown state is not a state this client can act in</b> —
    /// the same default-deny rule <see cref="PermissionSet.Allows"/> applies to an unknown
    /// permission.
    /// </para>
    /// <para>
    /// This belongs to the requisition rather than to the authorisation rules: how a record
    /// describes its own state is a fact about the record, and a screen needs it whether or not
    /// anything was refused.
    /// </para>
    /// </remarks>
    public string StateInWords => State switch
    {
        RequisitionState.Draft => "still a draft",
        RequisitionState.Submitted => "submitted",
        RequisitionState.Approved => "already approved",
        RequisitionState.Cancelled => "cancelled",
        _ => "in a state this application does not recognise",
    };
}

/// <summary>
/// Decides what a screen should offer for one requisition.
/// </summary>
/// <remarks>
/// <para>
/// <b>A permission is not a decision.</b> "May approve requisitions" is a permission. "May approve
/// <i>this</i> requisition" needs the requisition: who raised it, what it is worth, and where it has
/// got to. <b>Checking the permission and stopping there is the most common real authorisation
/// defect</b>, and self-approval is its most common form.
/// </para>
/// <para>
/// And the server still decides. Everything here is about what the interface offers.
/// </para>
/// </remarks>
public sealed class RequisitionAuthorization(UserCapabilities capabilities, decimal approvalLimit)
{
    /// <summary>Whether the screen should offer to approve this requisition, and why not.</summary>
    /// <remarks>
    /// <b>The order of these four is user-visible and is not incidental.</b> The permission is the
    /// coarsest fact and every other reason is only meaningful to somebody who has it. Telling a
    /// user with no approval role that a requisition is "above your limit" is wrong, invites them
    /// to ask for a limit that would change nothing, and discloses the limit to somebody with no
    /// role at all.
    /// </remarks>
    public AuthorizationDecision CanApprove(PurchaseRequisition requisition)
    {
        if (!capabilities.Allows(Permissions.ApproveRequisition))
        {
            return AuthorizationDecision.Denied(
                DenialReason.NotPermitted,
                "You do not have permission to approve requisitions.");
        }

        // SubjectId is null when nobody is known, and null equals no requisition's owner. That is
        // why it is null rather than empty.
        if (string.Equals(requisition.RaisedBy, capabilities.SubjectId, StringComparison.Ordinal))
        {
            return AuthorizationDecision.Denied(
                DenialReason.OwnRequisition,
                "You cannot approve a requisition you raised yourself.");
        }

        if (requisition.Amount > approvalLimit)
        {
            // CurrentCulture, because a person reads this. The Circuit Breaker entry made the same
            // choice for its status line and the Authentication entry made the opposite one for a
            // diagnostic string. All three are deliberate.
            return AuthorizationDecision.Denied(
                DenialReason.AboveApprovalLimit,
                $"This requisition is above your approval limit of {approvalLimit.ToString("C", CultureInfo.CurrentCulture)}.");
        }

        if (requisition.State != RequisitionState.Submitted)
        {
            return AuthorizationDecision.Denied(
                DenialReason.WrongState,
                $"Only a submitted requisition can be approved. This one is {requisition.StateInWords}.");
        }

        return AuthorizationDecision.Allowed;
    }

    /// <summary>Whether the screen should offer to cancel this requisition, and why not.</summary>
    /// <remarks>
    /// <b>No permission is involved.</b> The person who raised a requisition may withdraw it, and no
    /// permission table would ever say so. Authorisation is a relationship between a subject and a
    /// resource, not a bit.
    /// </remarks>
    public AuthorizationDecision CanCancel(PurchaseRequisition requisition)
    {
        if (!string.Equals(requisition.RaisedBy, capabilities.SubjectId, StringComparison.Ordinal))
        {
            return AuthorizationDecision.Denied(
                DenialReason.NotRaiser,
                "Only the person who raised a requisition can cancel it.");
        }

        if (requisition.State != RequisitionState.Submitted)
        {
            return AuthorizationDecision.Denied(
                DenialReason.WrongState,
                $"A requisition can only be cancelled while it is submitted. This one is {requisition.StateInWords}.");
        }

        return AuthorizationDecision.Allowed;
    }
}
