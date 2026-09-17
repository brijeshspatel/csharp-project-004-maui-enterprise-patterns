using CommunityToolkit.Mvvm.Messaging;
using Messaging.Core;

namespace Messaging.Core.Tests;

/// <summary>
/// Every test builds its own <see cref="WeakReferenceMessenger"/>. None uses
/// <c>WeakReferenceMessenger.Default</c>: that instance is process-wide, xUnit runs test classes in
/// parallel, and registrations would leak between tests in a way that fails only sometimes.
/// </summary>
/// <remarks>
/// Every participant is also given its <em>own</em> order instances, as three screens fed by three
/// separate queries would have. Sharing one mutable instance between publisher and recipient would
/// make the list update through the shared reference, so the test would pass with no message sent
/// at all — and the pattern would be pointless, because the state would already have propagated.
/// </remarks>
public class OrderCancellationMessagingTests
{
    private static List<OrderSummary> Orders() =>
    [
        new(1, "ACME-01", 100m),
        new(2, "ACME-02", 250m),
        new(3, "GLOBEX-01", 400m),
    ];

    private static OrderSummary Subject(int id) => Orders().Single(order => order.Id == id);

    [Fact]
    public void CancellingAnOrder_UpdatesTheList_WithNoReferenceBetweenThem()
    {
        var messenger = new WeakReferenceMessenger();

        // Built independently, sharing only the messenger. Neither is passed to the other, and the
        // list's rows are not the object the publisher mutates.
        using var list = new OrderListViewModel(messenger, Orders());
        var detail = new OrderDetailViewModel(messenger, Subject(1)) { CancellationReason = "Duplicate order" };

        detail.CancelOrderCommand.Execute(null);

        var cancelled = Assert.Single(list.Orders, order => order.Status == OrderStatus.Cancelled);
        Assert.Equal("ACME-01", cancelled.CustomerReference);
        Assert.All(
            list.Orders.Where(order => order.Id != 1),
            order => Assert.Equal(OrderStatus.Outstanding, order.Status));
    }

    [Fact]
    public void OneSend_ReachesBothRecipients_WhichKnowNothingOfEachOther()
    {
        var messenger = new WeakReferenceMessenger();

        using var list = new OrderListViewModel(messenger, Orders());
        using var summary = new OutstandingWorkViewModel(messenger, Orders());
        var detail = new OrderDetailViewModel(messenger, Subject(2)) { CancellationReason = "Customer withdrew" };

        detail.CancelOrderCommand.Execute(null);

        Assert.Equal(OrderStatus.Cancelled, list.Orders.Single(order => order.Id == 2).Status);
        Assert.Equal(2, summary.OutstandingCount);
        Assert.Equal(500m, summary.CommittedValue);
        Assert.Equal("Customer withdrew", summary.LastCancellationReason);
    }

    [Fact]
    public void ADisposedRecipient_StopsReceiving_AndTheOtherContinues()
    {
        var messenger = new WeakReferenceMessenger();

        using var list = new OrderListViewModel(messenger, Orders());
        var summary = new OutstandingWorkViewModel(messenger, Orders());

        summary.Dispose();

        var detail = new OrderDetailViewModel(messenger, Subject(3)) { CancellationReason = "Out of stock" };
        detail.CancelOrderCommand.Execute(null);

        // The disposed recipient did not move.
        Assert.Equal(3, summary.OutstandingCount);
        Assert.Equal(750m, summary.CommittedValue);
        Assert.Equal(string.Empty, summary.LastCancellationReason);

        // The one still registered did.
        Assert.Equal(OrderStatus.Cancelled, list.Orders.Single(order => order.Id == 3).Status);
    }

    [Fact]
    public void CancellingTwice_SendsOnce_SoTheSummaryIsDecrementedOnce()
    {
        var messenger = new WeakReferenceMessenger();

        using var summary = new OutstandingWorkViewModel(messenger, Orders());
        var detail = new OrderDetailViewModel(messenger, Subject(1)) { CancellationReason = "Duplicate order" };

        detail.CancelOrderCommand.Execute(null);

        Assert.False(detail.CancelOrderCommand.CanExecute(null));

        // Executed directly, bypassing CanExecute exactly as a mis-wired caller would. The summary
        // decrements on every message it receives and defends itself against nothing, so a second
        // send would show here as a count of 1 and a committed value of 550.
        detail.CancelOrderCommand.Execute(null);

        Assert.Equal(2, summary.OutstandingCount);
        Assert.Equal(650m, summary.CommittedValue);
    }

    [Fact]
    public void RecipientsOnADifferentMessenger_ReceiveNothing()
    {
        var publisherMessenger = new WeakReferenceMessenger();
        var recipientMessenger = new WeakReferenceMessenger();

        using var summary = new OutstandingWorkViewModel(recipientMessenger, Orders());
        var detail = new OrderDetailViewModel(publisherMessenger, Subject(1)) { CancellationReason = "Duplicate order" };

        detail.CancelOrderCommand.Execute(null);

        Assert.Equal(3, summary.OutstandingCount);
        Assert.Equal(750m, summary.CommittedValue);
        Assert.Equal(string.Empty, summary.LastCancellationReason);
    }

    [Fact]
    public void SendingWithNoRecipientsRegistered_DoesNotThrow()
    {
        var messenger = new WeakReferenceMessenger();
        var subject = Subject(1);
        var detail = new OrderDetailViewModel(messenger, subject) { CancellationReason = "Nobody is listening" };

        detail.CancelOrderCommand.Execute(null);

        Assert.Equal(OrderStatus.Cancelled, subject.Status);
    }

    [Fact]
    public void TheCommandIsRefused_UntilAReasonIsGiven()
    {
        var messenger = new WeakReferenceMessenger();
        var detail = new OrderDetailViewModel(messenger, Subject(1));

        Assert.False(detail.CancelOrderCommand.CanExecute(null));

        detail.CancellationReason = "   ";
        Assert.False(detail.CancelOrderCommand.CanExecute(null));

        detail.CancellationReason = "Duplicate order";
        Assert.True(detail.CancelOrderCommand.CanExecute(null));
    }
}
