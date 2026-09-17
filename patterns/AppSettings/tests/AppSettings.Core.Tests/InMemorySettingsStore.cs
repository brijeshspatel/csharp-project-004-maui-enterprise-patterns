using AppSettings.Core;

namespace AppSettings.Core.Tests;

/// <summary>
/// A settings store held in a dictionary.
/// </summary>
/// <remarks>
/// This stands in for the platform store, which cannot run here. Anything it proves is a claim
/// about <see cref="ApplicationSettings"/>, not about .NET MAUI's <c>Preferences</c>.
/// </remarks>
internal sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly Dictionary<string, object> _values = [];

    public bool GetBool(string key, bool defaultValue) => Get(key, defaultValue);

    public void SetBool(string key, bool value) => _values[key] = value;

    public int GetInt(string key, int defaultValue) => Get(key, defaultValue);

    public void SetInt(string key, int value) => _values[key] = value;

    public string GetString(string key, string defaultValue) => Get(key, defaultValue);

    public void SetString(string key, string value) => _values[key] = value;

    public DateTime GetDateTime(string key, DateTime defaultValue) => Get(key, defaultValue);

    public void SetDateTime(string key, DateTime value) => _values[key] = value;

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public void Remove(string key) => _values.Remove(key);

    public void Clear() => _values.Clear();

    private T Get<T>(string key, T defaultValue) =>
        _values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;
}
