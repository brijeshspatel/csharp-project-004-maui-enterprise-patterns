using Mvvm.Core;

namespace Mvvm.Demo;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		BindingContext = new OrderDetailViewModel(new Order(1, "ACME-01", 100m));
	}
}
