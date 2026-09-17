using AppSettings.Core;

namespace AppSettings.Demo;

/// <summary>
/// The settings screen.
/// </summary>
public partial class MainPage : ContentPage
{
	public MainPage(SettingsViewModel settings)
	{
		InitializeComponent();
		Settings = settings;
	}

	public SettingsViewModel Settings { get; }
}
