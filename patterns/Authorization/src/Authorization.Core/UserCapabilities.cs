namespace Authorization.Core;

/// <summary>Where the permission set comes from.</summary>
/// <remarks>
/// <para>
/// <b>The server states the permissions</b>, over the authenticated channel, as ordinary data. It is
/// the only honest source, because it is the same party that enforces them.
/// </para>
/// <para>
/// Three sources were rejected: the access token's claims, because a client cannot validate a token
/// and parsing one needs a package; a table compiled into the client, because that is the client
/// deciding its own permissions; and trying the operation to see whether it works, because that is
/// a side effect rather than a question.
/// </para>
/// <para>
/// <c>Authorization.Core</c> performs no I/O. The tests implement this in memory and the
/// demonstration answers locally. <b>Nothing here reaches a network.</b>
/// </para>
/// </remarks>
public interface IPermissionSource
{
    /// <summary>Ask the server what the current subject may do.</summary>
    Task<PermissionSet> LoadAsync(CancellationToken cancellationToken);
}

/// <summary>What happened when the permissions were last asked for.</summary>
public enum RefreshOutcome
{
    /// <summary>A new set arrived and is now held.</summary>
    Loaded,

    /// <summary>The source could not answer. Whatever was held is still held, and is now stale.</summary>
    Failed,

    /// <summary>
    /// The answer arrived after something discarded the set it belonged to, and was dropped.
    /// </summary>
    Superseded,
}

/// <summary>
/// The permission set this application is currently working from.
/// </summary>
/// <remarks>
/// <para>
/// <b>These checks shape the user interface. They do not enforce anything.</b> The binary is on the
/// user's device; the server is where authorisation actually happens. Everything below is about not
/// offering what the user cannot have, and explaining why.
/// </para>
/// <para>
/// <b>Thread-safe without a lock</b>, and the reason is worth reading rather than copying. The held
/// set is <b>immutable and replaced wholesale</b>, so there is no read-modify-write to protect — a
/// <c>volatile</c> reference is enough. The neighbouring Authentication entry needed a semaphore
/// because it had to decide whether to refresh and then refresh; this one only ever publishes a new
/// value.
/// </para>
/// </remarks>
public sealed class UserCapabilities(IPermissionSource source)
{
    private volatile PermissionSet _held = PermissionSet.None;
    private volatile bool _isStale;
    private int _generation;

    /// <summary>Who the server said this application is acting for, if anybody.</summary>
    public string? SubjectId => _held.SubjectId;

    /// <summary>Whether the last attempt to refresh failed, so what is held may be out of date.</summary>
    public bool IsStale => _isStale;

    /// <summary>Whether the held set grants the named permission.</summary>
    /// <remarks>
    /// <b>Synchronous, deliberately.</b> An asynchronous check would put a network call behind every
    /// button's enabled state — and behind every one of them at once, while the network is down.
    /// </remarks>
    public bool Allows(string permission) => _held.Allows(permission);

    /// <summary>Ask the server again.</summary>
    /// <remarks>
    /// <para>
    /// <b>A failure keeps what is held.</b> "Fail closed" is the right rule where a check is the
    /// thing that stops an action; it is the wrong rule here, because the server enforces. Denying
    /// everything because a refresh failed buys no security at all and costs the user every
    /// affordance in the application. It is marked stale instead.
    /// </para>
    /// <para>
    /// <b>Never having loaded is different</b>, and is denied: offering a capability nobody has
    /// described is guessing. <b>Being contradicted is different again</b> — see
    /// <see cref="Invalidate"/>. Absence of evidence and contrary evidence must not produce the
    /// same answer.
    /// </para>
    /// </remarks>
    public async Task<RefreshOutcome> RefreshAsync(CancellationToken cancellationToken)
    {
        var generation = Volatile.Read(ref _generation);

        PermissionSet loaded;
        try
        {
            loaded = await source.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            _isStale = true;
            return RefreshOutcome.Failed;
        }

        // THE GENERATION CHECK. A 403 may have arrived while this call was in flight, and what came
        // back is the permission set as it was BEFORE the server contradicted it. Installing it
        // would put the application back to believing exactly what the server has just refused, and
        // nothing would correct it. A late answer to a question that is no longer being asked is
        // dropped rather than applied.
        if (Volatile.Read(ref _generation) != generation)
        {
            return RefreshOutcome.Superseded;
        }

        _held = loaded;
        _isStale = false;
        return RefreshOutcome.Loaded;
    }

    /// <summary>
    /// Throw the permission set away, because the server has said it is wrong.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The whole set goes, not the permission that seems to be involved.</b> A <c>403</c> carries
    /// a status, not a permission name, so invalidating selectively would need a map from route to
    /// permission — and that map is the client deciding its own permissions, which is the rejected
    /// design in another form. It would be written once and be wrong, silently, the first time the
    /// server changed a route.
    /// </para>
    /// <para>
    /// <b>This is not staleness.</b> Stale means "possibly out of date"; this means "known wrong",
    /// and the application denies everything until the server describes the subject again.
    /// </para>
    /// </remarks>
    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _held = PermissionSet.None;
    }
}
