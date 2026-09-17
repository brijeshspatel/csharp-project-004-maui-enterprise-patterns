using ContainerServices.Core;

namespace ContainerServices.Demo;

/// <summary>
/// Shows where each containerized service would be reached from, on this platform.
/// </summary>
public partial class MainPage : ContentPage
{
	public MainPage(ServiceCatalogueViewModel catalogue)
	{
		InitializeComponent();
		Catalogue = catalogue;
	}

	public ServiceCatalogueViewModel Catalogue { get; }
}
