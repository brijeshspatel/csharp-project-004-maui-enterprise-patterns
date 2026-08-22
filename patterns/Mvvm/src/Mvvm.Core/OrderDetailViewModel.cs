using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Mvvm.Core;

/// <summary>
/// View model for an order-detail screen. Base class is <see cref="ObservableValidator"/>,
/// not <see cref="ObservableObject"/>: <c>NotifyDataErrorInfo</c> and <c>HasErrors</c> are
/// <see cref="ObservableValidator"/> members, verified by building this exact shape during
/// the specification review for this pattern (CommunityToolkit.Mvvm 8.4.2).
/// </summary>
public sealed partial class OrderDetailViewModel : ObservableValidator
{
    public OrderDetailViewModel(Order order)
    {
        _customerReference = order.CustomerReference;
        ValidateAllProperties();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyPropertyChangedFor(nameof(FirstError))]
    [NotifyDataErrorInfo]
    [Required]
    private string _customerReference;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private bool CanSave() => !HasErrors;

    /// <summary>
    /// The first validation error, or an empty string when there is none. Exists so the
    /// view can bind directly to a string without a value converter — <c>GetErrors()</c>
    /// itself is a method, not a bindable property.
    /// </summary>
    public string FirstError =>
        GetErrors(nameof(CustomerReference)).OfType<ValidationResult>().FirstOrDefault()?.ErrorMessage
        ?? string.Empty;
}
