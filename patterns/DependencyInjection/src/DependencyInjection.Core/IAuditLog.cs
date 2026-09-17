namespace DependencyInjection.Core;

/// <summary>
/// The time, as a dependency.
/// </summary>
/// <remarks>
/// Injected rather than read from <see cref="DateTimeOffset"/> directly, so a test can fix the time
/// and assert an exact stamp instead of asserting that one is roughly now.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// One recorded action.
/// </summary>
public sealed record AuditEntry(DateTimeOffset RecordedAt, string Description);

/// <summary>
/// A record of what the application did, shared by every screen that writes to or reads it.
/// </summary>
public interface IAuditLog
{
    /// <summary>Records an action.</summary>
    void Record(AuditEntry entry);

    /// <summary>
    /// Every entry, newest first.
    /// </summary>
    /// <remarks>
    /// A snapshot, not the live collection. This log is a singleton shared by two screens, so a
    /// caller that could mutate what it reads could corrupt what the other screen sees. The
    /// lifetime is what makes that a real risk rather than a theoretical one.
    /// </remarks>
    IReadOnlyList<AuditEntry> Entries { get; }
}

/// <summary>
/// An audit log held in memory.
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly List<AuditEntry> _entries = [];

    public void Record(AuditEntry entry) => _entries.Insert(0, entry);

    public IReadOnlyList<AuditEntry> Entries => _entries.ToArray();
}
