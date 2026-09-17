using CircuitBreaker.Core;

namespace CircuitBreaker.Demo;

/// <summary>
/// Calls a service through the breaker, and lets the reader make the service healthy again.
/// </summary>
public partial class MainPage : ContentPage
{
	private readonly FailingService _service;

	public MainPage(ServiceHealthViewModel health, FailingService service)
	{
		InitializeComponent();
		Health = health;
		_service = service;
	}

	public ServiceHealthViewModel Health { get; }

	/// <summary>Whether the stand-in service answers or fails.</summary>
	public bool ServiceIsHealthy
	{
		get => _service.IsHealthy;
		set
		{
			_service.IsHealthy = value;
			OnPropertyChanged();
		}
	}
}
