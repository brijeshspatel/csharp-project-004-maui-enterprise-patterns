using AppSettings.Core;

namespace AppSettings.Demo;

/// <summary>
/// The platform half of the settings seam, and the only class in this pattern that knows
/// <c>Preferences</c> exists.
/// </summary>
/// <remarks>
/// <para>
/// .NET MAUI's own <c>IPreferences</c> is nearly this interface already, and
/// <c>AppSettings.Core</c> still declares its own. That is not duplication for its own sake:
/// <c>Microsoft.Maui.Essentials</c> ships a plain <c>net10.0</c> library, so a reference from a
/// platform-agnostic project compiles and then throws
/// <c>NotImplementedInReferenceAssemblyException</c> when called.
/// </para>
/// <para>
/// The adapter is about fifteen lines, and it is the whole cost of keeping the settings logic
/// testable.
/// </para>
/// </remarks>
internal sealed class PreferencesSettingsStore : ISettingsStore
{
	private readonly IPreferences _preferences;

	public PreferencesSettingsStore(IPreferences preferences)
	{
		_preferences = preferences;
	}

	public bool GetBool(string key, bool defaultValue) => _preferences.Get(key, defaultValue);

	public void SetBool(string key, bool value) => _preferences.Set(key, value);

	public int GetInt(string key, int defaultValue) => _preferences.Get(key, defaultValue);

	public void SetInt(string key, int value) => _preferences.Set(key, value);

	public string GetString(string key, string defaultValue) => _preferences.Get(key, defaultValue);

	public void SetString(string key, string value) => _preferences.Set(key, value);

	public DateTime GetDateTime(string key, DateTime defaultValue) => _preferences.Get(key, defaultValue);

	public void SetDateTime(string key, DateTime value) => _preferences.Set(key, value);

	public bool ContainsKey(string key) => _preferences.ContainsKey(key);

	public void Remove(string key) => _preferences.Remove(key);

	public void Clear() => _preferences.Clear();
}
