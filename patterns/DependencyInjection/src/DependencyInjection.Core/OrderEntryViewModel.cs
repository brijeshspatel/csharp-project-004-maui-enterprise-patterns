using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DependencyInjection.Core;

/// <summary>
/// The order entry screen. It records an order, and it builds nothing it depends on.
/// </summary>
/// <remarks>
/// Both dependencies arrive through the constructor. That is what lets a container resolve this
/// type — and, more usefully, what lets a test construct it with fakes and no container at all.
/// </remarks>
public sealed partial class OrderEntryViewModel : ObservableObject
{
    private readonly IAuditLog _auditLog;
    private readonly IClock _clock;

    public OrderEntryViewModel(IAuditLog auditLog, IClock clock)
    {
        _auditLog = auditLog;
        _clock = clock;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecordOrderCommand))]
    private string _description = string.Empty;

    [RelayCommand(CanExecute = nameof(CanRecord))]
    private void RecordOrder()
    {
        // Re-checked rather than trusted: CanExecute governs the button, not a caller that invokes
        // the command directly. An audit log with a blank entry is worse than one without it.
        if (!CanRecord())
        {
            return;
        }

        _auditLog.Record(new AuditEntry(_clock.UtcNow, Description));
        Description = string.Empty;
    }

    private bool CanRecord() => !string.IsNullOrWhiteSpace(Description);
}

/// <summary>
/// The audit trail screen. It reads the log it was given, and never creates one.
/// </summary>
/// <remarks>
/// A screen that built its own log would have built a private one, and would show nothing the other
/// screen recorded. That is the defect this pattern prevents, and a test proves it by making the
/// two share.
/// </remarks>
public sealed partial class AuditTrailViewModel : ObservableObject
{
    private readonly IAuditLog _auditLog;

    public AuditTrailViewModel(IAuditLog auditLog)
    {
        _auditLog = auditLog;
    }

    /// <summary>Every recorded entry, newest first.</summary>
    public IReadOnlyList<AuditEntry> Entries => _auditLog.Entries;

    /// <summary>Re-reads the log.</summary>
    /// <remarks>
    /// Needed because the log is shared and can change without this screen doing anything. A
    /// dependency with a longer lifetime than its consumer is exactly the case where a view must
    /// be told to look again.
    /// </remarks>
    [RelayCommand]
    private void Refresh() => OnPropertyChanged(nameof(Entries));
}
