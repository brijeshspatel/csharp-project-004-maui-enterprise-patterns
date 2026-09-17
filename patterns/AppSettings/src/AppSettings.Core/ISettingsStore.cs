namespace AppSettings.Core;

/// <summary>
/// A key and value store for application settings.
/// </summary>
/// <remarks>
/// <para>
/// Declared here rather than using .NET MAUI's own <c>IPreferences</c>, which this project could
/// reference and must not: <c>Microsoft.Maui.Essentials</c> ships a plain <c>net10.0</c> library, so
/// the reference compiles, and calling it throws
/// <c>NotImplementedInReferenceAssemblyException</c> at run time.
/// </para>
/// <para>
/// The members mirror the types .NET MAUI documents as storable. There is deliberately no generic
/// <c>Get&lt;T&gt;</c>: the platform supports exactly seven types, no constraint can express that,
/// and a generic method would compile for types the store cannot hold.
/// </para>
/// </remarks>
public interface ISettingsStore
{
    bool GetBool(string key, bool defaultValue);

    void SetBool(string key, bool value);

    int GetInt(string key, int defaultValue);

    void SetInt(string key, int value);

    string GetString(string key, string defaultValue);

    void SetString(string key, string value);

    DateTime GetDateTime(string key, DateTime defaultValue);

    void SetDateTime(string key, DateTime value);

    /// <summary>
    /// Whether the key is present.
    /// </summary>
    /// <remarks>
    /// Needed because a default cannot report absence: a key may exist holding a value equal to the
    /// default, and a caller reading only the value cannot tell that from an unset key.
    /// </remarks>
    bool ContainsKey(string key);

    void Remove(string key);

    void Clear();
}

/// <summary>
/// The setting keys, declared once.
/// </summary>
public static class SettingKeys
{
    public const string NotificationsEnabled = "notifications_enabled";
    public const string PageSize = "page_size";
    public const string DefaultWarehouse = "default_warehouse";
    public const string LastSynchronised = "last_synchronised";
}
