using DependencyInjection.Core;

namespace DependencyInjection.Core.Tests;

/// <summary>
/// A clock a test controls.
/// </summary>
internal sealed class FixedClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
}

/// <summary>
/// Every test here builds its subject by hand, with no container anywhere. That is not a
/// simplification for the sake of testing — it is the property constructor injection delivers, and
/// the reason a container can resolve these types at all.
/// </summary>
public class AuditLoggingTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordingAnOrder_AddsAnEntryToTheLog()
    {
        var log = new InMemoryAuditLog();
        var entry = new OrderEntryViewModel(log, new FixedClock(Noon)) { Description = "ACME-01" };

        entry.RecordOrderCommand.Execute(null);

        var recorded = Assert.Single(log.Entries);
        Assert.Equal("ACME-01", recorded.Description);
    }

    [Fact]
    public void TheEntryCarriesTheInjectedClocksTime_Exactly()
    {
        // The reason the clock is a dependency. Without it this could only assert "roughly now".
        var log = new InMemoryAuditLog();
        var entry = new OrderEntryViewModel(log, new FixedClock(Noon)) { Description = "ACME-01" };

        entry.RecordOrderCommand.Execute(null);

        Assert.Equal(Noon, Assert.Single(log.Entries).RecordedAt);
    }

    [Fact]
    public void TwoViewModelsGivenTheSameLog_SeeEachOthersEntries()
    {
        // What this proves: neither view model creates a log of its own. It does not prove
        // anything about AddSingleton, which no test here uses — see the README for that evidence.
        var shared = new InMemoryAuditLog();
        var entry = new OrderEntryViewModel(shared, new FixedClock(Noon)) { Description = "ACME-01" };
        var trail = new AuditTrailViewModel(shared);

        entry.RecordOrderCommand.Execute(null);

        Assert.Equal("ACME-01", Assert.Single(trail.Entries).Description);
    }

    [Fact]
    public void TwoViewModelsGivenSeparateLogs_SeeNothingOfEachOther()
    {
        // The same code, wired differently. This is what makes the lifetime choice consequential.
        var entry = new OrderEntryViewModel(new InMemoryAuditLog(), new FixedClock(Noon)) { Description = "ACME-01" };
        var trail = new AuditTrailViewModel(new InMemoryAuditLog());

        entry.RecordOrderCommand.Execute(null);

        Assert.Empty(trail.Entries);
    }

    [Fact]
    public void TheLogReturnsEntriesNewestFirst()
    {
        var log = new InMemoryAuditLog();
        var clock = new FixedClock(Noon);
        var entry = new OrderEntryViewModel(log, clock);

        entry.Description = "first";
        entry.RecordOrderCommand.Execute(null);
        clock.UtcNow = Noon.AddMinutes(1);
        entry.Description = "second";
        entry.RecordOrderCommand.Execute(null);

        Assert.Collection(
            log.Entries,
            newest => Assert.Equal("second", newest.Description),
            oldest => Assert.Equal("first", oldest.Description));
    }

    [Fact]
    public void TheEntriesACallerReadsCannotChangeWhatTheLogHolds()
    {
        // The log is shared by two screens. A caller that could mutate what it reads could corrupt
        // what the other screen sees, so reads return a snapshot.
        var log = new InMemoryAuditLog();
        log.Record(new AuditEntry(Noon, "ACME-01"));

        var firstRead = log.Entries;
        log.Record(new AuditEntry(Noon.AddMinutes(1), "ACME-02"));

        Assert.Single(firstRead);
        Assert.Equal(2, log.Entries.Count);
    }

    [Fact]
    public void ABlankDescription_IsRefusedAndRecordsNothing()
    {
        var log = new InMemoryAuditLog();
        var entry = new OrderEntryViewModel(log, new FixedClock(Noon));

        Assert.False(entry.RecordOrderCommand.CanExecute(null));

        entry.Description = "   ";
        Assert.False(entry.RecordOrderCommand.CanExecute(null));

        // Executed directly, bypassing CanExecute as a mis-wired caller would.
        entry.RecordOrderCommand.Execute(null);
        Assert.Empty(log.Entries);
    }

    [Fact]
    public void RefreshingTheTrail_ShowsWhatWasRecordedAfterItWasBuilt()
    {
        var shared = new InMemoryAuditLog();
        var trail = new AuditTrailViewModel(shared);
        Assert.Empty(trail.Entries);

        var entry = new OrderEntryViewModel(shared, new FixedClock(Noon)) { Description = "ACME-01" };
        entry.RecordOrderCommand.Execute(null);
        trail.RefreshCommand.Execute(null);

        Assert.Single(trail.Entries);
    }
}
