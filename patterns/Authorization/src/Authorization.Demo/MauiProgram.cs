using Authorization.Core;
using Microsoft.Extensions.Logging;

// Two different meanings of "permission" meet in this file. Microsoft.Maui.ApplicationModel
// .Permissions -- which MAUI's implicit usings bring in -- is what the OS lets the application do:
// the camera, the location. Authorization.Core.Permissions is what the SERVER lets the USER do.
// Neither name is wrong, so the head disambiguates rather than either side renaming.
//
// The collision is invisible to Authorization.Core and to the tests, because neither references
// MAUI. Only the platform heads see it, and only the build says so.
using UserPermissions = Authorization.Core.Permissions;

namespace Authorization.Demo;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<StandInPermissionSource>();
		builder.Services.AddSingleton<IPermissionSource>(p => p.GetRequiredService<StandInPermissionSource>());

		// One set of capabilities for the whole application. Two would each hold their own idea of
		// what the user may do, and a 403 would correct only one of them.
		builder.Services.AddSingleton<UserCapabilities>();

		builder.Services.AddSingleton(p => new RequisitionAuthorization(
			p.GetRequiredService<UserCapabilities>(),
			approvalLimit: 5_000m));

		builder.Services.AddTransient<RequisitionViewModel>();
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

/// <summary>
/// Stands in for the server, so a reader can drive every case by hand.
/// </summary>
/// <remarks>
/// <b>Nothing is called and no network is reached.</b> A real implementation would fetch this over
/// the authenticated channel from the same server that enforces the permissions — which is the only
/// honest source, because it is the party that actually decides.
/// </remarks>
public sealed class StandInPermissionSource : IPermissionSource
{
	/// <summary>Who the server says this application is acting for.</summary>
	public const string SignedInSubject = "u-1042";

	/// <summary>Whether the stand-in grants the approval permission.</summary>
	public bool GrantsApproval { get; set; } = true;

	/// <summary>Whether the stand-in refuses to answer, so a reader can see a stale set.</summary>
	public bool IsUnreachable { get; set; }

	public async Task<PermissionSet> LoadAsync(CancellationToken cancellationToken)
	{
		// A delay, so the screen behaves like one waiting on a server.
		await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);

		if (IsUnreachable)
		{
			throw new InvalidOperationException("The stand-in permission source is unreachable.");
		}

		return new PermissionSet(
			SignedInSubject,
			GrantsApproval
				? [UserPermissions.ViewRequisition, UserPermissions.ApproveRequisition]
				: [UserPermissions.ViewRequisition]);
	}
}
