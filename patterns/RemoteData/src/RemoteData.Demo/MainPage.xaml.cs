using RemoteData.Core;

namespace RemoteData.Demo;

/// <summary>
/// The order list.
/// </summary>
public partial class MainPage : ContentPage
{
	public MainPage(OrderListViewModel orders)
	{
		InitializeComponent();
		Orders = orders;
	}

	public OrderListViewModel Orders { get; }
}
