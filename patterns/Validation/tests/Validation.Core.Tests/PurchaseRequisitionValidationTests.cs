using System.ComponentModel.DataAnnotations;
using Validation.Core;

namespace Validation.Core.Tests;

/// <summary>
/// A fixed approval limit, so a test states the limit it is testing against rather than depending
/// on a rule defined elsewhere.
/// </summary>
internal sealed class FixedApprovalPolicy(decimal limit) : IApprovalPolicy
{
    public decimal LimitFor(string requestedBy) => limit;
}

public class PurchaseRequisitionValidationTests
{
    private static readonly DateOnly Today = new(2026, 9, 17);

    private static PurchaseRequisitionViewModel Form(decimal limit = 10_000m) =>
        new(new FixedApprovalPolicy(limit), Today);

    private static PurchaseRequisitionViewModel ValidForm(decimal limit = 10_000m)
    {
        var form = Form(limit);
        form.RequestedBy = "A. Patel";
        form.Quantity = 10;
        form.UnitPrice = 25m;
        form.RequiredBy = Today.AddDays(14);
        return form;
    }

    private static IEnumerable<string?> ErrorsFor(PurchaseRequisitionViewModel form, string property) =>
        form.GetErrors(property).OfType<ValidationResult>().Select(result => result.ErrorMessage);

    [Fact]
    public void AnUntouchedForm_ReportsNoErrors()
    {
        // Validation deliberately does not run in the constructor: a form should not open red.
        Assert.False(Form().HasErrors);
    }

    [Fact]
    public void ACompleteValidRequisition_Submits()
    {
        var form = ValidForm();

        form.SubmitCommand.Execute(null);

        Assert.NotNull(form.SubmittedRequisition);
        Assert.Equal("A. Patel", form.SubmittedRequisition.RequestedBy);
        Assert.Equal(250m, form.SubmittedRequisition.Total);
    }

    [Fact]
    public void TheSubmittedRequisition_CarriesEveryFieldFromTheForm()
    {
        var form = ValidForm();

        form.SubmitCommand.Execute(null);

        var submitted = form.SubmittedRequisition;
        Assert.NotNull(submitted);
        Assert.Equal("A. Patel", submitted.RequestedBy);
        Assert.Equal(10, submitted.Quantity);
        Assert.Equal(25m, submitted.UnitPrice);
        Assert.Equal(Today, submitted.OrderedOn);
        Assert.Equal(Today.AddDays(14), submitted.RequiredBy);
    }

    [Fact]
    public void Total_TracksQuantityAndUnitPrice()
    {
        var form = Form();

        form.Quantity = 7;
        form.UnitPrice = 3m;

        Assert.Equal(21m, form.Total);
    }

    [Fact]
    public void ABlankForm_DoesNotSubmit_AndReportsTheMissingRequester()
    {
        var form = Form();

        form.SubmitCommand.Execute(null);

        Assert.Null(form.SubmittedRequisition);
        Assert.Contains("A requester is required.", ErrorsFor(form, nameof(form.RequestedBy)));
    }

    [Fact]
    public void AQuantityOutsideItsRange_ReportsAnError()
    {
        var form = ValidForm();

        form.Quantity = 5000;

        Assert.Contains("Quantity must be between 1 and 1000.", ErrorsFor(form, nameof(form.Quantity)));
    }

    [Fact]
    public void ARequiredDateBeforeTheOrderDate_ReportsTheCustomAttributesMessage()
    {
        var form = ValidForm();

        form.RequiredBy = Today.AddDays(-1);

        Assert.Contains(
            ErrorsFor(form, nameof(form.RequiredBy)),
            message => message is not null && message.Contains("must not be before OrderedOn"));
    }

    [Fact]
    public void MovingTheOrderDatePastTheRequiredDate_PutsTheRequiredDateIntoError()
    {
        // The staleness guard. RequiredBy was valid when it was set, and nothing touches it here.
        // Without a re-validation when OrderedOn changes, its verdict would simply stay.
        var form = ValidForm();
        Assert.Empty(ErrorsFor(form, nameof(form.RequiredBy)));

        form.OrderedOn = Today.AddDays(30);

        Assert.NotEmpty(ErrorsFor(form, nameof(form.RequiredBy)));
    }

    [Fact]
    public void RaisingTheUnitPricePastTheApprovalLimit_PutsQuantityIntoError()
    {
        // The second staleness guard, and the injected policy. Quantity is untouched.
        var form = ValidForm(limit: 1_000m);
        Assert.Empty(ErrorsFor(form, nameof(form.Quantity)));

        form.UnitPrice = 500m;

        Assert.Contains(
            ErrorsFor(form, nameof(form.Quantity)),
            message => message is not null && message.Contains("above the approval limit"));
    }

    [Fact]
    public void LoweringTheUnitPriceAgain_ClearsThatError()
    {
        var form = ValidForm(limit: 1_000m);
        form.UnitPrice = 500m;
        Assert.NotEmpty(ErrorsFor(form, nameof(form.Quantity)));

        form.UnitPrice = 10m;

        Assert.Empty(ErrorsFor(form, nameof(form.Quantity)));
    }

    [Fact]
    public void Reset_ClearsTheFieldsAndTheErrors()
    {
        var form = Form();
        form.SubmitCommand.Execute(null);
        Assert.True(form.HasErrors);

        form.ResetCommand.Execute(null);

        Assert.False(form.HasErrors);
        Assert.Equal(string.Empty, form.RequestedBy);
        Assert.Equal(string.Empty, form.ErrorSummary);
    }

    [Fact]
    public void ErrorSummary_ListsEveryCurrentError_NotOnlyTheFirst()
    {
        var form = Form(limit: 1m);
        form.Quantity = 5000;
        form.UnitPrice = 999m;

        form.SubmitCommand.Execute(null);

        Assert.Contains("A requester is required.", form.ErrorSummary);
        Assert.Contains("Quantity must be between 1 and 1000.", form.ErrorSummary);
        Assert.Contains("above the approval limit", form.ErrorSummary);
    }

    [Fact]
    public void TheMonetaryLimitsParseIndependentlyOfCulture()
    {
        // ParseLimitsInInvariantCulture is set on the Range attribute. Without it the limits are
        // parsed with the current culture, and "0.01" is not one hundredth where the decimal
        // separator is a comma.
        var original = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            var form = ValidForm();
            form.UnitPrice = 0.005m;

            Assert.NotEmpty(ErrorsFor(form, nameof(form.UnitPrice)));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }
}
