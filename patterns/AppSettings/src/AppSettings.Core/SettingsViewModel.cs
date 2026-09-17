using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AppSettings.Core;

/// <summary>
/// The settings screen.
/// </summary>
/// <remarks>
/// It reads the settings once into editable properties rather than binding the store directly, so a
/// user can change several and then save, and so a correction made on read is reported once rather
/// than on every binding evaluation.
/// </remarks>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ApplicationSettings _settings;

    public SettingsViewModel(ApplicationSettings settings)
    {
        _settings = settings;
        Load();
    }

    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private int _pageSize;

    [ObservableProperty]
    private string _warehouse = ApplicationSettings.DefaultWarehouse;

    [ObservableProperty]
    private DateTime _lastSynchronised;

    /// <summary>
    /// Set when the stored page size was outside what this version allows.
    /// </summary>
    [ObservableProperty]
    private string _notice = string.Empty;

    [RelayCommand]
    private void Load()
    {
        var storedPageSize = _settings.PageSize;

        NotificationsEnabled = _settings.NotificationsEnabled;
        PageSize = storedPageSize;
        Warehouse = _settings.Warehouse;
        LastSynchronised = _settings.LastSynchronised;

        Notice = _settings.PageSizeWasClamped
            ? $"A stored page size was outside the allowed range and has been set to {storedPageSize}."
            : string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.NotificationsEnabled = NotificationsEnabled;
        _settings.PageSize = PageSize;
        _settings.Warehouse = Warehouse;
        _settings.LastSynchronised = LastSynchronised;
        Load();
    }

    [RelayCommand]
    private void ResetAll()
    {
        _settings.ResetAll();
        Load();
    }
}
