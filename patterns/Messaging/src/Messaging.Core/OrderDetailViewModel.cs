using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Messaging.Core;

/// <summary>
/// The order detail screen, and the only publisher in this pattern. It cancels an order and
/// announces that it did so. It holds no reference to anything that reacts.
/// </summary>
public sealed partial class OrderDetailViewModel : ObservableObject
{
    private readonly IMessenger _messenger;

    /// <summary>
    /// Takes the messenger as a dependency rather than reading a shared static instance, so the
    /// screen can be exercised against a messenger created for one test and nothing leaks between
    /// tests.
    /// </summary>
    public OrderDetailViewModel(IMessenger messenger, OrderSummary order)
    {
        _messenger = messenger;
        Order = order;
    }

    public OrderSummary Order { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelOrderCommand))]
    private string _cancellationReason = string.Empty;

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelOrder()
    {
        // Re-checked rather than trusted. CanExecute governs the button; it does not govern a
        // caller that invokes the command directly. A duplicate send fans out to every recipient,
        // and each would then have to defend itself. Guarding once, here, is cheaper and correct.
        if (Order.Status != OrderStatus.Outstanding)
        {
            return;
        }

        Order.Status = OrderStatus.Cancelled;

        // Status lives on OrderSummary, not on this view model, so no generated notification
        // reaches the command. Raising it explicitly is what disables the button.
        CancelOrderCommand.NotifyCanExecuteChanged();

        _messenger.Send(new OrderCancelledMessage(new OrderCancellation(Order.Id, CancellationReason)));
    }

    private bool CanCancel() =>
        Order.Status == OrderStatus.Outstanding && !string.IsNullOrWhiteSpace(CancellationReason);
}
