using CommunityToolkit.Mvvm.ComponentModel;

namespace Messaging.Core;

/// <summary>
/// The state an order is in. Two values, because the demonstration needs exactly two: an order
/// that still represents committed work, and one that no longer does.
/// </summary>
public enum OrderStatus
{
    /// <summary>The order is live and its value is still committed work.</summary>
    Outstanding,

    /// <summary>The order has been cancelled and its value is released.</summary>
    Cancelled,
}

/// <summary>
/// An order as it appears on a list screen.
/// </summary>
/// <remarks>
/// This is an <see cref="ObservableObject"/> rather than a record because <see cref="Status"/>
/// changes in place and a bound list must see the change. Identity and value do not change, so
/// they are get-only.
/// </remarks>
public sealed partial class OrderSummary : ObservableObject
{
    public OrderSummary(int id, string customerReference, decimal total)
    {
        Id = id;
        CustomerReference = customerReference;
        Total = total;
    }

    public int Id { get; }

    public string CustomerReference { get; }

    public decimal Total { get; }

    [ObservableProperty]
    private OrderStatus _status = OrderStatus.Outstanding;
}
