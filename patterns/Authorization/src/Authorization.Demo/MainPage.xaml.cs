using Authorization.Core;

namespace Authorization.Demo;

/// <summary>
/// Drives every case by hand: with and without the permission, own and somebody else's requisition,
/// above and below the limit, and a server that cannot be reached.
/// </summary>
public partial class MainPage : ContentPage
{
	private static readonly PurchaseRequisition RaisedBySomebodyElse =
		new("REQ-4471", "u-2093", 1_250m, RequisitionState.Submitted);

	private static readonly PurchaseRequisition RaisedByThisUser =
		new("REQ-4480", StandInPermissionSource.SignedInSubject, 1_250m, RequisitionState.Submitted);

	private readonly StandInPermissionSource _server;

	public MainPage(RequisitionViewModel detail, StandInPermissionSource server)
	{
		InitializeComponent();
		Detail = detail;
		_server = server;
		Detail.Requisition = RaisedBySomebodyElse;
	}

	public RequisitionViewModel Detail { get; }

	/// <summary>Whether the stand-in server grants the approval permission.</summary>
	public bool ServerGrantsApproval
	{
		get => _server.GrantsApproval;
		set
		{
			_server.GrantsApproval = value;
			OnPropertyChanged();
		}
	}

	/// <summary>Whether the stand-in server refuses to answer.</summary>
	public bool ServerIsUnreachable
	{
		get => _server.IsUnreachable;
		set
		{
			_server.IsUnreachable = value;
			OnPropertyChanged();
		}
	}

	/// <summary>Whether the requisition on screen was raised by the signed-in user.</summary>
	public bool ShowOwnRequisition
	{
		get => Detail.Requisition == RaisedByThisUser;
		set
		{
			Detail.Requisition = value ? RaisedByThisUser : RaisedBySomebodyElse;
			OnPropertyChanged();
		}
	}
}
