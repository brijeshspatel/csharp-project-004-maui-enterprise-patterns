using AppSettings.Core;

namespace AppSettings.Core.Tests;

public class ApplicationSettingsTests
{
    private static (ApplicationSettings Settings, InMemorySettingsStore Store) NewSettings()
    {
        var store = new InMemorySettingsStore();
        return (new ApplicationSettings(store), store);
    }

    [Fact]
    public void AnEmptyStore_GivesEverySettingItsDocumentedDefault()
    {
        var (settings, _) = NewSettings();

        Assert.Equal(ApplicationSettings.DefaultNotificationsEnabled, settings.NotificationsEnabled);
        Assert.Equal(ApplicationSettings.DefaultPageSize, settings.PageSize);
        Assert.Equal(ApplicationSettings.DefaultWarehouse, settings.Warehouse);
        Assert.Equal(DateTime.UnixEpoch, settings.LastSynchronised);
    }

    [Fact]
    public void EachSetting_ReadsBackWhatItWrote()
    {
        // What this proves is that ApplicationSettings reads back what it wrote, through whatever
        // store it was given. It is not evidence that .NET MAUI's Preferences holds these types —
        // that store runs on no platform available here.
        var (settings, _) = NewSettings();

        settings.NotificationsEnabled = false;
        settings.PageSize = 50;
        settings.Warehouse = "NORTH";

        Assert.False(settings.NotificationsEnabled);
        Assert.Equal(50, settings.PageSize);
        Assert.Equal("NORTH", settings.Warehouse);
    }

    [Fact]
    public void APageSizeStoredAboveTheCurrentMaximum_IsCorrectedOnRead_AndTheCorrectionIsReported()
    {
        // The version-drift case. An earlier version allowed 500, and devices that ran it still
        // hold that value. Validation added now cannot reach back into what was already written.
        var (settings, store) = NewSettings();
        store.SetInt(SettingKeys.PageSize, 500);

        Assert.Equal(ApplicationSettings.MaximumPageSize, settings.PageSize);
        Assert.True(settings.PageSizeWasClamped);
    }

    [Fact]
    public void APageSizeStoredBelowTheCurrentMinimum_IsCorrectedOnRead()
    {
        var (settings, store) = NewSettings();
        store.SetInt(SettingKeys.PageSize, 1);

        Assert.Equal(ApplicationSettings.MinimumPageSize, settings.PageSize);
        Assert.True(settings.PageSizeWasClamped);
    }

    [Fact]
    public void APageSizeWithinRange_IsNotReportedAsCorrected()
    {
        // Without this, the clamp test above would also pass against an implementation that
        // reported every read as corrected.
        var (settings, store) = NewSettings();
        store.SetInt(SettingKeys.PageSize, 50);

        Assert.Equal(50, settings.PageSize);
        Assert.False(settings.PageSizeWasClamped);
    }

    [Fact]
    public void ALocalTime_IsStoredAndReturnedAsTheSameInstantInUtc()
    {
        var (settings, _) = NewSettings();
        var local = new DateTime(2026, 9, 17, 21, 30, 0, DateTimeKind.Local);

        settings.LastSynchronised = local;

        var read = settings.LastSynchronised;
        Assert.Equal(DateTimeKind.Utc, read.Kind);
        Assert.Equal(local.ToUniversalTime(), read);
    }

    [Fact]
    public void WritingASettingToItsOwnDefaultValue_IsStillAChoice_AndOnlyContainsKeyCanTellTheDifference()
    {
        var (settings, store) = NewSettings();
        Assert.False(settings.HasBeenConfigured);

        settings.PageSize = ApplicationSettings.DefaultPageSize;

        // The value alone cannot distinguish this state from an untouched store...
        Assert.Equal(ApplicationSettings.DefaultPageSize, settings.PageSize);
        Assert.Equal(ApplicationSettings.DefaultPageSize, store.GetInt(SettingKeys.PageSize, ApplicationSettings.DefaultPageSize));

        // ...and ContainsKey can, which is the whole reason it exists.
        Assert.True(settings.HasBeenConfigured);
    }

    [Fact]
    public void ResettingOneSetting_RemovesIt_RatherThanWritingTheDefaultOverIt()
    {
        var (settings, _) = NewSettings();
        settings.PageSize = 50;
        Assert.True(settings.HasBeenConfigured);

        settings.ResetPageSize();

        Assert.Equal(ApplicationSettings.DefaultPageSize, settings.PageSize);
        Assert.False(settings.HasBeenConfigured);
    }

    [Fact]
    public void ResettingEverything_ReturnsEverySettingToItsDefault()
    {
        var (settings, _) = NewSettings();
        settings.NotificationsEnabled = false;
        settings.PageSize = 50;
        settings.Warehouse = "NORTH";

        settings.ResetAll();

        Assert.Equal(ApplicationSettings.DefaultNotificationsEnabled, settings.NotificationsEnabled);
        Assert.Equal(ApplicationSettings.DefaultPageSize, settings.PageSize);
        Assert.Equal(ApplicationSettings.DefaultWarehouse, settings.Warehouse);
        Assert.False(settings.HasBeenConfigured);
    }

    [Fact]
    public void WritingOneSetting_LeavesTheOthersUntouched()
    {
        var (settings, _) = NewSettings();

        settings.Warehouse = "NORTH";

        Assert.Equal(ApplicationSettings.DefaultPageSize, settings.PageSize);
        Assert.Equal(ApplicationSettings.DefaultNotificationsEnabled, settings.NotificationsEnabled);
    }

    [Fact]
    public void TheSettingsScreen_ReportsACorrectionOnceAndClearsItAfterSaving()
    {
        var (settings, store) = NewSettings();
        store.SetInt(SettingKeys.PageSize, 500);

        var screen = new SettingsViewModel(settings);
        Assert.Contains("outside the allowed range", screen.Notice);
        Assert.Equal(ApplicationSettings.MaximumPageSize, screen.PageSize);

        screen.SaveCommand.Execute(null);

        Assert.Equal(string.Empty, screen.Notice);
    }

    [Fact]
    public void TheSettingsScreen_ResetsEverything()
    {
        var (settings, _) = NewSettings();
        settings.Warehouse = "NORTH";
        var screen = new SettingsViewModel(settings);
        Assert.Equal("NORTH", screen.Warehouse);

        screen.ResetAllCommand.Execute(null);

        Assert.Equal(ApplicationSettings.DefaultWarehouse, screen.Warehouse);
    }
}
