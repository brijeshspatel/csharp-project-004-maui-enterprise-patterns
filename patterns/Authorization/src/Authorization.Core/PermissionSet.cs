namespace Authorization.Core;

/// <summary>The permission names this build knows how to ask about.</summary>
/// <remarks>
/// Constants rather than an enumeration, so a typo is a compile error instead of a silent denial,
/// while the set itself stays open — the server may grant a name this build has never heard of.
/// </remarks>
public static class Permissions
{
    /// <summary>May approve a purchase requisition raised by somebody else.</summary>
    public const string ApproveRequisition = "requisition.approve";

    /// <summary>May see purchase requisitions.</summary>
    public const string ViewRequisition = "requisition.view";
}

/// <summary>
/// What the server said this subject may do.
/// </summary>
/// <remarks>
/// <para>
/// <b>The server states both halves.</b> The subject is not derived here from a token — the client
/// cannot validate one, and parsing it would be the client deciding its own identity. It is told
/// who it is acting for in the same response that says what that subject may do.
/// </para>
/// <para>
/// <b>Immutable.</b> Nothing mutates a set; a new one replaces the old wholesale. That is what lets
/// <see cref="UserCapabilities"/> be thread-safe without a lock.
/// </para>
/// </remarks>
public sealed class PermissionSet
{
    private readonly HashSet<string> _granted;

    /// <summary>A set that knows nobody and grants nothing.</summary>
    /// <remarks>
    /// <see cref="SubjectId"/> is <see langword="null"/> rather than empty, so it cannot
    /// accidentally equal a resource's owner. An empty string compares equal to an empty string,
    /// and "the unknown user owns the record nobody owns" is a defect nobody reads twice.
    /// </remarks>
    public static PermissionSet None { get; } = new(null, []);

    public PermissionSet(string? subjectId, IEnumerable<string> granted)
    {
        SubjectId = subjectId;

        // Ordinal, and therefore case-sensitive. Two systems whose permission names differ only in
        // case are two systems, and matching them loosely is how one system's permission silently
        // becomes another's.
        _granted = new HashSet<string>(granted, StringComparer.Ordinal);
    }

    /// <summary>Who these permissions belong to, or <see langword="null"/> if nobody is known.</summary>
    public string? SubjectId { get; }

    /// <summary>Whether this set grants the named permission.</summary>
    /// <remarks>
    /// <b>An unknown name is denied.</b> The set answers only about what it was told, so a
    /// permission this build invents, or one the server has stopped granting, is a refusal rather
    /// than a guess.
    /// </remarks>
    public bool Allows(string permission) => _granted.Contains(permission);
}
