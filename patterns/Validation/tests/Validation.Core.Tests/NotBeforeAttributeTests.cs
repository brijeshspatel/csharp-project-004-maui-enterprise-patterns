using System.ComponentModel.DataAnnotations;
using Validation.Core;

namespace Validation.Core.Tests;

/// <summary>
/// The attribute is a reusable type in its own right, so it is tested on its own rather than only
/// through the one view model that happens to use it.
/// </summary>
public class NotBeforeAttributeTests
{
    private sealed class DateHolder
    {
        public DateOnly Start { get; init; }

        public string NotADate { get; init; } = "not a date";
    }

    private static ValidationResult? Check(object? value, string otherProperty, DateHolder holder) =>
        new NotBeforeAttribute(otherProperty)
            .GetValidationResult(value, new ValidationContext(holder) { DisplayName = "Finish" });

    [Fact]
    public void ADateOnOrAfterTheOtherDate_IsValid()
    {
        var holder = new DateHolder { Start = new DateOnly(2026, 1, 10) };

        Assert.Equal(ValidationResult.Success, Check(new DateOnly(2026, 1, 10), nameof(DateHolder.Start), holder));
        Assert.Equal(ValidationResult.Success, Check(new DateOnly(2026, 1, 11), nameof(DateHolder.Start), holder));
    }

    [Fact]
    public void ADateBeforeTheOtherDate_ReportsWhichPropertyItMustNotPrecede()
    {
        var holder = new DateHolder { Start = new DateOnly(2026, 1, 10) };

        var result = Check(new DateOnly(2026, 1, 9), nameof(DateHolder.Start), holder);

        Assert.NotNull(result);
        Assert.Equal("Finish must not be before Start.", result.ErrorMessage);
    }

    [Fact]
    public void AValueThatIsNotADate_IsLeftToWhicheverAttributeOwnsThatProblem()
    {
        // Reporting a comparison failure for a value that was never a date would mask the real
        // error, which belongs to another attribute.
        var holder = new DateHolder { Start = new DateOnly(2026, 1, 10) };

        Assert.Equal(ValidationResult.Success, Check("nonsense", nameof(DateHolder.Start), holder));
        Assert.Equal(ValidationResult.Success, Check(null, nameof(DateHolder.Start), holder));
    }

    [Fact]
    public void AReferenceToAPropertyThatIsNotADate_IsIgnoredRatherThanThrowing()
    {
        // A misnamed or wrongly typed reference is a programming error, and failing validation
        // would report it as the user's mistake on a form they cannot fix.
        var holder = new DateHolder { Start = new DateOnly(2026, 1, 10) };

        Assert.Equal(ValidationResult.Success, Check(new DateOnly(2026, 1, 1), nameof(DateHolder.NotADate), holder));
        Assert.Equal(ValidationResult.Success, Check(new DateOnly(2026, 1, 1), "NoSuchProperty", holder));
    }
}
