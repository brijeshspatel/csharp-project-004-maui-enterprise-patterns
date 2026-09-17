using Navigation.Core;

namespace Navigation.Demo;

/// <summary>
/// The editable order detail screen.
/// </summary>
/// <remarks>
/// This class is the whole seam. It implements <see cref="IQueryAttributable"/>, which is a
/// <c>Microsoft.Maui.Controls</c> type that <c>Navigation.Core</c> cannot reference, and forwards
/// to the view model's own interface. Everything above that forwarding line is testable without a
/// platform.
/// </remarks>
public partial class OrderDetailPage : ContentPage, IQueryAttributable
{
	private readonly OrderDetailNavigationViewModel _viewModel;

	public OrderDetailPage()
	{
		InitializeComponent();
		_viewModel = new OrderDetailNavigationViewModel(
			DemoComposition.NavigationService,
			DemoComposition.Orders());
		BindingContext = _viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		// Copied, not cast. The interface promises only IDictionary, and Shell may retain or clear
		// its own dictionary afterwards — so the view model must not hold a reference to it.
		_viewModel.ApplyParameters(new Dictionary<string, object>(query));
	}
}
