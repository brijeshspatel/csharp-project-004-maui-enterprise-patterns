using CommunityToolkit.Mvvm.Messaging;
using Messaging.Core;

namespace Messaging.Demo;

/// <summary>
/// Hosts the three view models on one screen so a single cancellation can be seen reaching two
/// regions that share no reference.
/// </summary>
/// <remarks>
/// <para>
/// The page is the composition root of this demonstration. It creates one messenger and hands it
/// to all three view models. No view model reads <c>WeakReferenceMessenger.Default</c>: the host
/// decides which messenger is in use, which is what lets a test supply its own.
/// </para>
/// <para>
/// In an application with navigation this wiring would be a container registration, and the three
/// view models would be created on different screens at different times. A single page is used
/// here because the property worth seeing is two regions changing at once.
/// </para>
/// <para>
/// Each view model is given its <em>own</em> order instances, as three screens fed by three
/// separate queries would have. Were they to share one instance, the list would update through the
/// shared reference and the message would be doing no work.
/// </para>
/// </remarks>
public partial class MainPage : ContentPage
{
	private readonly IMessenger _messenger = new WeakReferenceMessenger();

	public MainPage()
	{
		InitializeComponent();

		List = new OrderListViewModel(_messenger, SampleOrders());
		OutstandingWork = new OutstandingWorkViewModel(_messenger, SampleOrders());
		Detail = new OrderDetailViewModel(_messenger, SampleOrders().First());

		BindingContext = this;
	}

	public OrderListViewModel List { get; }

	public OutstandingWorkViewModel OutstandingWork { get; }

	public OrderDetailViewModel Detail { get; }

	private static List<OrderSummary> SampleOrders() =>
	[
		new(1, "ACME-01", 100m),
		new(2, "ACME-02", 250m),
		new(3, "GLOBEX-01", 400m),
	];
}
