using Navigation.Core;

namespace Navigation.Demo;

/// <summary>
/// The order list, and the screen that decides where opening an order goes.
/// </summary>
/// <remarks>
/// The page exposes the view model as a typed property and binds to itself, so the XAML can declare
/// the types it binds to. A binding through an <c>object</c>-typed <c>BindingContext</c> cannot be
/// resolved at compile time and falls back to reflection.
/// </remarks>
public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		List = new OrderListNavigationViewModel(
			DemoComposition.NavigationService,
			DemoComposition.Orders());
	}

	public OrderListNavigationViewModel List { get; }
}
