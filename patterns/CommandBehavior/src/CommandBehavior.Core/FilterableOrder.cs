namespace CommandBehavior.Core;

/// <summary>
/// An order row on a list screen, filterable by customer reference.
/// </summary>
public sealed record FilterableOrder(int Id, string CustomerReference, decimal Total);
