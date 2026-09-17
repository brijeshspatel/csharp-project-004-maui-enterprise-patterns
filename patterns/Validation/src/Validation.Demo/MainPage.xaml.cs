using Validation.Core;

namespace Validation.Demo;

/// <summary>
/// The requisition form.
/// </summary>
/// <remarks>
/// <para>
/// The page exposes the view model as a typed property and binds to itself, so the XAML can declare
/// the types it binds to. A binding through an <c>object</c>-typed <c>BindingContext</c> cannot be
/// resolved at compile time and falls back to reflection, which this repository treats as an error.
/// </para>
/// <para>
/// It also adapts between <see cref="DateOnly"/> and <see cref="DateTime"/>. <c>DatePicker.Date</c>
/// is a <see cref="DateTime"/>, while a requisition date has no time of day and the view model says
/// so. <b>The adapter lives here rather than on the view model</b>, because it exists to satisfy a
/// control, and putting it in the core project would let a view concern set the shape of the
/// domain.
/// </para>
/// </remarks>
public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		Form = new PurchaseRequisitionViewModel(
			new TieredApprovalPolicy(),
			DateOnly.FromDateTime(DateTime.Today));
	}

	public PurchaseRequisitionViewModel Form { get; }

	public DateTime OrderedOnDate
	{
		get => Form.OrderedOn.ToDateTime(TimeOnly.MinValue);
		set
		{
			Form.OrderedOn = DateOnly.FromDateTime(value);
			OnPropertyChanged();
		}
	}

	public DateTime RequiredByDate
	{
		get => Form.RequiredBy.ToDateTime(TimeOnly.MinValue);
		set
		{
			Form.RequiredBy = DateOnly.FromDateTime(value);
			OnPropertyChanged();
		}
	}
}

/// <summary>
/// A stand-in approval policy for the demonstration.
/// </summary>
/// <remarks>
/// Deliberately trivial. What matters to the pattern is that the limit comes from a service the view
/// model was given, not from a constant an attribute could have held.
/// </remarks>
internal sealed class TieredApprovalPolicy : IApprovalPolicy
{
	public decimal LimitFor(string requestedBy) =>
		string.IsNullOrWhiteSpace(requestedBy) ? 0m : 2_500m;
}
