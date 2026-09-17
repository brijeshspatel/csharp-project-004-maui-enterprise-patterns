namespace AppSettings.Core;

/// <summary>
/// The application's settings, typed, with their defaults and their current rules.
/// </summary>
/// <remarks>
/// <para>
/// <b>A stored setting is untrusted input from a version of the application that no longer
/// exists.</b> It was written against rules that may since have changed, by code that may since
/// have been deleted. Nothing about it is corrupt, and nothing about it is guaranteed to be
/// acceptable now.
/// </para>
/// <para>
/// That is why <see cref="PageSize"/> is checked when it is read rather than only when it is
/// written. A value written by an earlier version cannot be reached by validation added later.
/// </para>
/// </remarks>
public sealed class ApplicationSettings
{
    /// <summary>The smallest page size this version allows.</summary>
    public const int MinimumPageSize = 10;

    /// <summary>
    /// The largest page size this version allows.
    /// </summary>
    /// <remarks>
    /// An earlier version allowed 500. Devices that ran it still hold that value.
    /// </remarks>
    public const int MaximumPageSize = 100;

    public const int DefaultPageSize = 25;
    public const bool DefaultNotificationsEnabled = true;
    public const string DefaultWarehouse = "MAIN";

    private readonly ISettingsStore _store;

    public ApplicationSettings(ISettingsStore store)
    {
        _store = store;
    }

    public bool NotificationsEnabled
    {
        get => _store.GetBool(SettingKeys.NotificationsEnabled, DefaultNotificationsEnabled);
        set => _store.SetBool(SettingKeys.NotificationsEnabled, value);
    }

    public string Warehouse
    {
        get => _store.GetString(SettingKeys.DefaultWarehouse, DefaultWarehouse);
        set => _store.SetString(SettingKeys.DefaultWarehouse, value);
    }

    /// <summary>
    /// Whether the last read of <see cref="PageSize"/> had to correct the stored value.
    /// </summary>
    /// <remarks>
    /// Exposed so a settings screen can tell the user their stored choice is no longer available.
    /// A value silently changed underneath someone is how a support call begins.
    /// </remarks>
    public bool PageSizeWasClamped { get; private set; }

    /// <summary>
    /// How many rows a list screen shows.
    /// </summary>
    /// <remarks>
    /// Clamped on read, not merely on write. Throwing was rejected: a settings screen that cannot
    /// open because an earlier version wrote a then-legal value is worse than the value being
    /// corrected and the correction being reported.
    /// </remarks>
    public int PageSize
    {
        get
        {
            var stored = _store.GetInt(SettingKeys.PageSize, DefaultPageSize);
            var clamped = Math.Clamp(stored, MinimumPageSize, MaximumPageSize);
            PageSizeWasClamped = clamped != stored;
            return clamped;
        }
        set => _store.SetInt(SettingKeys.PageSize, Math.Clamp(value, MinimumPageSize, MaximumPageSize));
    }

    /// <summary>
    /// When the application last synchronised, in UTC.
    /// </summary>
    /// <remarks>
    /// Normalised to UTC on the way in and returned as UTC. .NET MAUI stores a
    /// <see cref="DateTime"/> through <see cref="DateTime.ToBinary"/>, which Microsoft documents as
    /// making adjustments to values that are not already UTC. Storing UTC removes the question.
    /// </remarks>
    public DateTime LastSynchronised
    {
        get
        {
            var stored = _store.GetDateTime(SettingKeys.LastSynchronised, DateTime.UnixEpoch);
            return DateTime.SpecifyKind(stored, DateTimeKind.Utc);
        }
        set => _store.SetDateTime(SettingKeys.LastSynchronised, value.ToUniversalTime());
    }

    /// <summary>
    /// Whether anyone has chosen a page size, as opposed to never having been asked.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ISettingsStore.ContainsKey"/> because the value cannot answer this. A user
    /// who deliberately chose the default leaves a store whose <c>Get</c> is indistinguishable from
    /// an untouched one.
    /// </remarks>
    public bool HasBeenConfigured => _store.ContainsKey(SettingKeys.PageSize);

    /// <summary>
    /// Returns one setting to its default by removing it, rather than by writing the default over
    /// it.
    /// </summary>
    /// <remarks>
    /// The difference is visible to <see cref="HasBeenConfigured"/>: writing the default leaves a
    /// key behind and claims the user has chosen.
    /// </remarks>
    public void ResetPageSize() => _store.Remove(SettingKeys.PageSize);

    /// <summary>Returns every setting to its default.</summary>
    public void ResetAll() => _store.Clear();
}
