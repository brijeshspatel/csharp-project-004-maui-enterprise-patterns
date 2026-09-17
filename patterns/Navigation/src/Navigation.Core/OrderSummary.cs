namespace Navigation.Core;

/// <summary>
/// The state an order is in, as this pattern needs it.
/// </summary>
public enum OrderState
{
    /// <summary>Live work, and still editable.</summary>
    Outstanding,

    /// <summary>Cancelled. A financial record, and no longer editable.</summary>
    Cancelled,
}

/// <summary>
/// An order as a list screen shows it.
/// </summary>
/// <remarks>
/// Deliberately its own type rather than a reference to another pattern's model. The pattern
/// projects in this repository do not reference one another, so each stands alone and can be read
/// without the others.
/// </remarks>
public sealed record OrderSummary(int Id, string CustomerReference, decimal Total, OrderState State);
