using Authentication.Core;

namespace Authentication.Demo;

/// <summary>
/// Drives a session by hand: sign in, use the token, make the provider refuse, sign out.
/// </summary>
public partial class MainPage : ContentPage
{
	private readonly StandInIdentityProvider _provider;

	public MainPage(SessionViewModel session, IIdentityProvider provider)
	{
		InitializeComponent();
		Session = session;

		// The composition root registers the stand-in, and this screen is the only thing that knows
		// it is one. Nothing in Authentication.Core can tell the difference.
		_provider = (StandInIdentityProvider)provider;
	}

	public SessionViewModel Session { get; }

	/// <summary>Whether the stand-in provider refuses the next refresh.</summary>
	public bool ProviderRefusesRefresh
	{
		get => _provider.RefusesRefresh;
		set
		{
			_provider.RefusesRefresh = value;
			OnPropertyChanged();
		}
	}
}
