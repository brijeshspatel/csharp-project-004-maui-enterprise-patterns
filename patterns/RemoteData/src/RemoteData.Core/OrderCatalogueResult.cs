namespace RemoteData.Core;

/// <summary>
/// An order as the service returns it.
/// </summary>
public sealed record OrderDto(int Id, string CustomerReference, decimal Total);

/// <summary>
/// Where an answer came from.
/// </summary>
public enum DataSource
{
    /// <summary>The service answered.</summary>
    Live,

    /// <summary>The service did not answer, and this is the last answer it gave.</summary>
    Cache,
}

/// <summary>
/// What a read of the order catalogue produced.
/// </summary>
/// <remarks>
/// <para>
/// A result rather than an exception, because "the service did not answer" is an outcome a screen
/// must handle rather than an error it can only report. <b>Cancellation is the exception to that</b>
/// — a cancelled call has no result, and throws.
/// </para>
/// <para>
/// Deliberately not generic. A <c>RemoteResult&lt;T&gt;</c> would need static factory methods on a
/// generic type, which <c>CA1000</c> reports, and this pattern has exactly one result shape.
/// Generalising it when a second endpoint appears is a smaller change than carrying the generality
/// before anything needs it.
/// </para>
/// <para>
/// Built only through the factory methods below, so a value without a source, or a value beside a
/// problem, cannot be constructed by accident.
/// </para>
/// </remarks>
public sealed record OrderCatalogueResult
{
    private OrderCatalogueResult(IReadOnlyList<OrderDto>? orders, DataSource? source, string? problem)
    {
        Orders = orders;
        Source = source;
        Problem = problem;
    }

    public IReadOnlyList<OrderDto>? Orders { get; }

    /// <summary>Where <see cref="Orders"/> came from. Never null when <see cref="Succeeded"/>.</summary>
    public DataSource? Source { get; }

    /// <summary>What went wrong, or null.</summary>
    public string? Problem { get; }

    public bool Succeeded => Problem is null;

    /// <summary>The service answered.</summary>
    public static OrderCatalogueResult Live(IReadOnlyList<OrderDto> orders) =>
        new(orders, DataSource.Live, null);

    /// <summary>
    /// The service did not answer, and this is what it last said.
    /// </summary>
    /// <remarks>
    /// Only for the case where the answer is unknown. A service that answered with an error has
    /// told us something, and showing stale data instead would hide it.
    /// </remarks>
    public static OrderCatalogueResult FromCache(IReadOnlyList<OrderDto> orders) =>
        new(orders, DataSource.Cache, null);

    /// <summary>The read failed and there is nothing to show.</summary>
    public static OrderCatalogueResult Failed(string problem) => new(null, null, problem);
}

/// <summary>
/// The last answer the service gave.
/// </summary>
public interface IOrderCache
{
    IReadOnlyList<OrderDto>? Read();

    void Write(IReadOnlyList<OrderDto> orders);
}

/// <summary>
/// A cache held in memory, which is enough to show the pattern and is lost when the process ends.
/// </summary>
/// <remarks>
/// A production cache would outlive the process, and where it is stored is a decision of its own —
/// a file, a database, or the settings store. That choice does not change the rules in
/// <see cref="HttpOrderCatalogue"/> about when the cache may be used.
/// </remarks>
public sealed class InMemoryOrderCache : IOrderCache
{
    private IReadOnlyList<OrderDto>? _orders;

    public IReadOnlyList<OrderDto>? Read() => _orders;

    public void Write(IReadOnlyList<OrderDto> orders) => _orders = orders;
}
