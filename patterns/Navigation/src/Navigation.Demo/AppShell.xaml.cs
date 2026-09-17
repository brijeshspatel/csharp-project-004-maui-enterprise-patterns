using Navigation.Core;

namespace Navigation.Demo;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Registered here, and named from the same constants the view models navigate with, so a
		// route cannot be registered under one spelling and requested under another.
		Routing.RegisterRoute(OrderRoutes.OrderDetail, typeof(OrderDetailPage));
		Routing.RegisterRoute(OrderRoutes.OrderAudit, typeof(OrderAuditPage));
	}
}
