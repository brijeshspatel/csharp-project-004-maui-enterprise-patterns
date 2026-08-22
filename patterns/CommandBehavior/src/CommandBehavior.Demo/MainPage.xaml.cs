using CommandBehavior.Core;

namespace CommandBehavior.Demo;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		BindingContext = new OrderListFilterViewModel(
		[
			new FilterableOrder(1, "ACME-01", 100m),
			new FilterableOrder(2, "ACME-02", 200m),
			new FilterableOrder(3, "GLOBEX-01", 300m),
		]);
	}
}
