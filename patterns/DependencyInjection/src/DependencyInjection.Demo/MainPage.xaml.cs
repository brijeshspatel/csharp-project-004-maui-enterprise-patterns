using DependencyInjection.Core;

namespace DependencyInjection.Demo;

/// <summary>
/// Both screens on one page, sharing one audit log.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no <c>new</c> in this constructor.</b> Every other demonstration in this repository
/// builds its view models here; this one receives them. .NET MAUI resolves a registered page during
/// Shell navigation and injects its constructor arguments, so the page states what it needs and the
/// composition root decides what that means.
/// </para>
/// <para>
/// The two view models are separate instances, because both are registered transient. The
/// <c>IAuditLog</c> they receive is one instance, because it is registered singleton. That
/// difference is the pattern.
/// </para>
/// </remarks>
public partial class MainPage : ContentPage
{
	public MainPage(OrderEntryViewModel entry, AuditTrailViewModel trail)
	{
		InitializeComponent();
		Entry = entry;
		Trail = trail;
	}

	public OrderEntryViewModel Entry { get; }

	public AuditTrailViewModel Trail { get; }
}
