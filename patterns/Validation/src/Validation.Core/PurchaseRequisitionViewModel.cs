using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Validation.Core;

/// <summary>
/// A purchase requisition form.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every rule is an attribute, and that is a constraint rather than a style.</b>
/// <c>ValidateAllProperties</c> runs the validation of public instance properties that carry at
/// least one validation attribute. A rule written as a hand-rolled check inside the submit command
/// would be invisible to it, so the submit path would accept a form it should reject.
/// </para>
/// <para>
/// <b>The form does not open red.</b> Nothing is validated in the constructor, so an untouched form
/// reports no errors. <see cref="SubmitCommand"/> validates everything before it decides, so a blank
/// form is not marked in advance and still cannot be submitted.
/// </para>
/// </remarks>
public sealed partial class PurchaseRequisitionViewModel : ObservableValidator
{
    private readonly IApprovalPolicy _approvalPolicy;

    public PurchaseRequisitionViewModel(IApprovalPolicy approvalPolicy, DateOnly today)
    {
        _approvalPolicy = approvalPolicy;
        _orderedOn = today;
        _requiredBy = today;
    }

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(AllowEmptyStrings = false, ErrorMessage = "A requester is required.")]
    private string _requestedBy = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 1000, ErrorMessage = "Quantity must be between 1 and 1000.")]
    [CustomValidation(typeof(PurchaseRequisitionViewModel), nameof(ValidateWithinApprovalLimit))]
    private int _quantity = 1;

    /// <remarks>
    /// The string form of <see cref="RangeAttribute"/> is used, not <c>[Range(0.01, 1000000)]</c>.
    /// The familiar constructor takes doubles, which would convert away the very precision
    /// <see cref="decimal"/> was chosen for. <c>ParseLimitsInInvariantCulture</c> is set because the
    /// limits are parsed with the current culture otherwise, and "0.01" is not one hundredth where
    /// the decimal separator is a comma.
    /// </remarks>
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(typeof(decimal), "0.01", "1000000", ParseLimitsInInvariantCulture = true,
        ErrorMessage = "Unit price must be above zero.")]
    private decimal _unitPrice = 1m;

    // No [NotifyDataErrorInfo] here, and it is not an oversight: OrderedOn carries no validation
    // attribute of its own — it is the date RequiredBy is measured against. The generator enforces
    // this, rejecting the pair with MVVMTK0026.
    [ObservableProperty]
    private DateOnly _orderedOn;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotBefore(nameof(OrderedOn))]
    private DateOnly _requiredBy;

    /// <summary>The requisition that was submitted, or <c>null</c> if none has been.</summary>
    [ObservableProperty]
    private SubmittedRequisition? _submittedRequisition;

    /// <summary>What the requisition commits, recomputed as the parts change.</summary>
    public decimal Total => Quantity * UnitPrice;

    /// <summary>
    /// Every current error, as one string.
    /// </summary>
    /// <remarks>
    /// Exists because <c>GetErrors()</c> is a method returning a sequence, and a view binds to
    /// properties. Recomputed whenever the error state changes.
    /// </remarks>
    public string ErrorSummary =>
        string.Join(" ", GetErrors().OfType<ValidationResult>().Select(result => result.ErrorMessage));

    /// <summary>
    /// The approval rule. It needs a service, so it cannot be a constant in an attribute.
    /// </summary>
    /// <remarks>
    /// Placed on <see cref="Quantity"/> because the total is what breaches the limit, and quantity
    /// is what a requester would reduce to fix it.
    /// </remarks>
    public static ValidationResult? ValidateWithinApprovalLimit(int quantity, ValidationContext context)
    {
        var instance = (PurchaseRequisitionViewModel)context.ObjectInstance;
        var limit = instance._approvalPolicy.LimitFor(instance.RequestedBy);
        var total = quantity * instance.UnitPrice;

        return total > limit
            ? new ValidationResult($"The total of {total:0.00} is above the approval limit of {limit:0.00}.")
            : ValidationResult.Success;
    }

    // Quantity's verdict depends on UnitPrice, and RequiredBy's depends on OrderedOn. Changing one
    // does not re-run the other's validation, so without these two calls the old verdict simply
    // stays — correct-looking and wrong. This is the generated equivalent of the hand-written
    // setter the MVVM Toolkit documentation puts the same call in.
    partial void OnUnitPriceChanged(decimal value)
    {
        ValidateProperty(Quantity, nameof(Quantity));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ErrorSummary));
    }

    partial void OnOrderedOnChanged(DateOnly value)
    {
        ValidateProperty(RequiredBy, nameof(RequiredBy));
        OnPropertyChanged(nameof(ErrorSummary));
    }

    partial void OnQuantityChanged(int value)
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ErrorSummary));
    }

    partial void OnRequestedByChanged(string value) => OnPropertyChanged(nameof(ErrorSummary));

    partial void OnRequiredByChanged(DateOnly value) => OnPropertyChanged(nameof(ErrorSummary));

    [RelayCommand]
    private void Submit()
    {
        // Validated here rather than gated through CanExecute. On a form nothing has touched,
        // HasErrors is false because nothing has been validated — which is "no known errors", not
        // "valid", and those are different states.
        ValidateAllProperties();
        OnPropertyChanged(nameof(ErrorSummary));

        if (HasErrors)
        {
            return;
        }

        SubmittedRequisition = new SubmittedRequisition(RequestedBy, Quantity, UnitPrice, OrderedOn, RequiredBy);
    }

    /// <summary>
    /// Returns the form to its pristine state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Clearing the fields alone would leave the errors behind, so the error state is cleared too.
    /// </para>
    /// <para>
    /// <b>Not <c>ClearAllErrors</c>.</b> Microsoft's documentation says <c>ObservableValidator</c>
    /// "exposes a <c>ClearAllErrors</c> method", and in CommunityToolkit.Mvvm 8.4.2 that method is
    /// <c>private</c> — calling it from a derived class fails with <c>CS0103</c>. The member that
    /// exists for this is <c>ClearErrors</c>, whose property name is optional and clears everything
    /// when omitted. Established by reflecting over the shipped assembly after the build refused
    /// the documented call.
    /// </para>
    /// </remarks>
    [RelayCommand]
    private void Reset()
    {
        RequestedBy = string.Empty;
        Quantity = 1;
        UnitPrice = 1m;
        RequiredBy = OrderedOn;
        SubmittedRequisition = null;

        ClearErrors();
        OnPropertyChanged(nameof(ErrorSummary));
    }
}
