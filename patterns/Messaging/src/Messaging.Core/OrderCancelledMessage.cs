using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Messaging.Core;

/// <summary>
/// What a cancellation says: which order, and why.
/// </summary>
/// <remarks>
/// The reason travels with the message because a recipient that must explain the change to a user
/// would otherwise have to ask the publisher for it, which is the coupling this pattern removes.
/// </remarks>
public sealed record OrderCancellation(int OrderId, string Reason);

/// <summary>
/// Announces that an order was cancelled. Sent by the screen that cancelled it, to recipients it
/// does not know about.
/// </summary>
/// <remarks>
/// Microsoft's migration guidance names two shapes for a message payload: a
/// <see cref="ValueChangedMessage{T}"/>, or a custom message with properties. A cancellation
/// carries more than an identifier, so the payload is a record and this type carries it.
/// </remarks>
public sealed class OrderCancelledMessage(OrderCancellation cancellation)
    : ValueChangedMessage<OrderCancellation>(cancellation);
