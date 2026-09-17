using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Authorization.Core;

/// <summary>
/// One requisition, and what this user may do with it.
/// </summary>
/// <remarks>
/// <b>It disables and explains rather than hiding.</b> Both are defensible: an explanation helps a
/// user who could reasonably obtain the capability, and an explanation also discloses that the
/// capability exists. For an internal line-of-business screen, where the organisation chart is not
/// a secret, explaining is the better default — and the README says what would change that.
/// </remarks>
public sealed partial class RequisitionViewModel(
    UserCapabilities capabilities,
    RequisitionAuthorization authorization) : ObservableObject
{
    [ObservableProperty]
    private PurchaseRequisition? _requisition;

    [ObservableProperty]
    private string _heading = "No requisition.";

    [ObservableProperty]
    private bool _canApprove;

    [ObservableProperty]
    private string _approveExplanation = string.Empty;

    [ObservableProperty]
    private bool _canCancel;

    [ObservableProperty]
    private string _cancelExplanation = string.Empty;

    [ObservableProperty]
    private bool _permissionsAreStale;

    [ObservableProperty]
    private string _status = "Permissions have not been checked yet.";

    /// <summary>Ask the server what this user may do, and re-decide what the screen offers.</summary>
    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var outcome = await capabilities.RefreshAsync(cancellationToken).ConfigureAwait(true);

        Status = outcome switch
        {
            RefreshOutcome.Loaded => "Permissions are current.",
            RefreshOutcome.Failed =>
                "Your permissions could not be checked, so what is shown may be out of date.",

            // Superseded. The application is not refreshed again automatically: a server that is
            // actively revoking could supersede every attempt, and a silent loop is worse than a
            // sentence asking for one more press.
            _ => "Your permissions changed while they were being checked. Refresh again.",
        };

        PermissionsAreStale = capabilities.IsStale;
        Decide();
    }

    partial void OnRequisitionChanged(PurchaseRequisition? value) => Decide();

    private void Decide()
    {
        if (Requisition is null)
        {
            Heading = "No requisition.";
            CanApprove = false;
            ApproveExplanation = string.Empty;
            CanCancel = false;
            CancelExplanation = string.Empty;
            return;
        }

        // Named flatly rather than bound through Requisition.Reference: a binding that reaches
        // through an object-typed property cannot be resolved at compile time, which is what
        // MAUIG2045 stopped the Navigation entry's build for.
        Heading = $"Requisition {Requisition.Reference} — {Requisition.StateInWords}";

        var approve = authorization.CanApprove(Requisition);
        CanApprove = approve.IsAllowed;
        ApproveExplanation = approve.Explanation;

        var cancel = authorization.CanCancel(Requisition);
        CanCancel = cancel.IsAllowed;
        CancelExplanation = cancel.Explanation;
    }
}
