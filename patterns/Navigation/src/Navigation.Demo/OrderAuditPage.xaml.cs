using Navigation.Core;

namespace Navigation.Demo;

/// <summary>
/// The read-only screen a cancelled order opens on.
/// </summary>
/// <remarks>
/// It shares <see cref="OrderDetailNavigationViewModel"/> with the editable screen, because what
/// differs is what the screen offers rather than what it knows. The rule that sends a cancelled
/// order here lives in <c>OrderListNavigationViewModel</c>, which is the only place it is decided.
/// </remarks>
public partial class OrderAuditPage : ContentPage, IQueryAttributable
{
	private readonly OrderDetailNavigationViewModel _viewModel;

	public OrderAuditPage()
	{
		InitializeComponent();
		_viewModel = new OrderDetailNavigationViewModel(
			DemoComposition.NavigationService,
			DemoComposition.Orders());
		BindingContext = _viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		_viewModel.ApplyParameters(new Dictionary<string, object>(query));
	}
}
