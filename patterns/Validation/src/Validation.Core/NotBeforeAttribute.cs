using System.ComponentModel.DataAnnotations;

namespace Validation.Core;

/// <summary>
/// Requires a date to be on or after the date held by another property.
/// </summary>
/// <remarks>
/// <para>
/// A rule about two properties cannot be written as an attribute on one of them alone. This reads
/// the other from <see cref="ValidationContext.ObjectInstance"/>, which is the mechanism the MVVM
/// Toolkit documents for exactly this case.
/// </para>
/// <para>
/// Written as a reusable attribute rather than as a <c>[CustomValidation]</c> method because the
/// rule says nothing about requisitions and would suit any pair of dates. **It is applied once in
/// this repository**, so the reuse is available rather than demonstrated.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class NotBeforeAttribute : ValidationAttribute
{
    public NotBeforeAttribute(string otherPropertyName)
    {
        OtherPropertyName = otherPropertyName;
    }

    /// <summary>The property holding the date this one must not precede.</summary>
    public string OtherPropertyName { get; }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date)
        {
            // Not a date. Another attribute's problem, or no value at all; reporting a comparison
            // failure here would mask the real one.
            return ValidationResult.Success;
        }

        var other = validationContext.ObjectInstance
            .GetType()
            .GetProperty(OtherPropertyName)
            ?.GetValue(validationContext.ObjectInstance);

        if (other is not DateOnly otherDate)
        {
            return ValidationResult.Success;
        }

        return date < otherDate
            ? new ValidationResult($"{validationContext.DisplayName} must not be before {OtherPropertyName}.")
            : ValidationResult.Success;
    }
}
