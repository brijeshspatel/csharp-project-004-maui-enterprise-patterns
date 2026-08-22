namespace Mvvm.Core;

/// <summary>
/// A line-of-business order, as loaded into the order-detail screen this pattern
/// demonstrates. Immutable: the view model owns the editable working state.
/// </summary>
public sealed record Order(int Id, string CustomerReference, decimal Total);
