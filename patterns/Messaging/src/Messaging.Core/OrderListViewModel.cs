using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Messaging.Core;

/// <summary>
/// The order list. One of two recipients, and it knows about neither the publisher nor the other
/// recipient.
/// </summary>
public sealed partial class OrderListViewModel : ObservableObject, IRecipient<OrderCancelledMessage>, IDisposable
{
    private readonly IMessenger _messenger;

    public OrderListViewModel(IMessenger messenger, IEnumerable<OrderSummary> orders)
    {
        _messenger = messenger;
        Orders = new ObservableCollection<OrderSummary>(orders);

        // Deliberately the last statement. From the moment this returns, the messenger can reach
        // this instance and call Receive, and a send from another thread would then arrive on a
        // half-built object. Every field must already be assigned.
        _messenger.RegisterAll(this);
    }

    public ObservableCollection<OrderSummary> Orders { get; }

    public void Receive(OrderCancelledMessage message)
    {
        var cancelled = Orders.FirstOrDefault(order => order.Id == message.Value.OrderId);
        if (cancelled is not null)
        {
            cancelled.Status = OrderStatus.Cancelled;
        }
    }

    /// <summary>
    /// Unregisters this recipient.
    /// </summary>
    /// <remarks>
    /// Not required by <c>WeakReferenceMessenger</c>, which holds recipients weakly. It is
    /// documented good practice, and it is what keeps this implementation correct if the messenger
    /// is ever swapped for <c>StrongReferenceMessenger</c>, which holds recipients strongly and
    /// does leak without it.
    /// </remarks>
    public void Dispose() => _messenger.UnregisterAll(this);
}
