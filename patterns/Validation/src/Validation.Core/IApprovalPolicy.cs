namespace Validation.Core;

/// <summary>
/// How much a requester may commit without further approval.
/// </summary>
/// <remarks>
/// This is why the approval rule is a <c>[CustomValidation]</c> method and not an attribute
/// constant: the limit depends on who is asking, which the view model cannot know and a service
/// can.
/// </remarks>
public interface IApprovalPolicy
{
    /// <summary>
    /// The limit for <paramref name="requestedBy"/>.
    /// </summary>
    /// <remarks>
    /// Must return a limit for any input, including an empty one. The total is validated while the
    /// requester field may still be blank, and a validation rule that throws takes the form down
    /// instead of reporting a problem.
    /// </remarks>
    decimal LimitFor(string requestedBy);
}

/// <summary>
/// A requisition that passed validation and was submitted.
/// </summary>
public sealed record SubmittedRequisition(
    string RequestedBy,
    int Quantity,
    decimal UnitPrice,
    DateOnly OrderedOn,
    DateOnly RequiredBy)
{
    /// <summary>What the requisition commits.</summary>
    public decimal Total => Quantity * UnitPrice;
}
