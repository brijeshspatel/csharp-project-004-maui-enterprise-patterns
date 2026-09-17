using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace Messaging.Core;

/// <summary>
/// A summary of work still committed: how many orders are outstanding, and what they are worth.
/// The second recipient, and the reason this pattern is not simply an event.
/// </summary>
/// <remarks>
/// It keeps its own record of order values, captured when it is built, rather than reading the
/// list recipient's collection. Two recipients that share mutable state would have to agree on the
/// order they run in, and the messenger promises no such order.
/// </remarks>
public sealed partial class OutstandingWorkViewModel : ObservableObject, IRecipient<OrderCancelledMessage>, IDisposable
{
    private readonly IMessenger _messenger;
    private readonly Dictionary<int, decimal> _valueByOrderId;

    public OutstandingWorkViewModel(IMessenger messenger, IEnumerable<OrderSummary> orders)
    {
        _messenger = messenger;

        var outstanding = orders.Where(order => order.Status == OrderStatus.Outstanding).ToList();
        _valueByOrderId = outstanding.ToDictionary(order => order.Id, order => order.Total);
        _outstandingCount = outstanding.Count;
        _committedValue = outstanding.Sum(order => order.Total);

        // Last statement, for the reason given on OrderListViewModel's constructor.
        _messenger.RegisterAll(this);
    }

    [ObservableProperty]
    private int _outstandingCount;

    [ObservableProperty]
    private decimal _committedValue;

    [ObservableProperty]
    private string _lastCancellationReason = string.Empty;

    /// <summary>
    /// Applies a cancellation.
    /// </summary>
    /// <remarks>
    /// This deliberately does not defend itself against being told twice about the same order: it
    /// would decrement twice. Not guarding here is what makes the publisher's guard load-bearing
    /// rather than decorative, and it is what allows a test to tell the two apart.
    /// </remarks>
    public void Receive(OrderCancelledMessage message)
    {
        if (_valueByOrderId.TryGetValue(message.Value.OrderId, out var value))
        {
            OutstandingCount--;
            CommittedValue -= value;
        }

        LastCancellationReason = message.Value.Reason;
    }

    /// <inheritdoc cref="OrderListViewModel.Dispose" />
    public void Dispose() => _messenger.UnregisterAll(this);
}
