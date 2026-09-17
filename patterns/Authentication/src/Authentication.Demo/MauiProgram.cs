using System.Globalization;
using Authentication.Core;
using Microsoft.Extensions.Logging;

namespace Authentication.Demo;

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

		builder.Services.AddSingleton<IClock, SystemClock>();

		// SecureStorage.Default, not Preferences.Default. A setting and a credential do not go to
		// the same place: SecureStorage is backed by the platform key store, and the Application
		// Settings Management entry fenced secrets out by name and pointed here.
		builder.Services.AddSingleton(SecureStorage.Default);
		builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();

		builder.Services.AddSingleton<IIdentityProvider, StandInIdentityProvider>();
		builder.Services.AddSingleton(AuthenticationPolicy.Default);

		// One provider for the whole application. Two would each hold a session, each refresh
		// separately, and defeat the single-flight rule the type exists to enforce.
		builder.Services.AddSingleton<AccessTokenProvider>();

		builder.Services.AddTransient<SessionViewModel>();
		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}

internal sealed class SystemClock : IClock
{
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>
/// Keeps a session in the platform's own secure store.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three keys rather than one serialised blob.</b> It removes a serialiser from a security-
/// sensitive path, and it makes the store's contents legible to anyone inspecting it during
/// development.
/// </para>
/// <para>
/// <b><c>Remove</c>, never <c>RemoveAll</c>.</b> <c>SecureStorage.RemoveAll()</c> clears every
/// secret the application has stored, not only this session's — a sign-out that throws away another
/// feature's data is a defect that will be reported as something else entirely.
/// </para>
/// </remarks>
public sealed class SecureStorageTokenStore(ISecureStorage storage) : ITokenStore
{
	private const string AccessTokenKey = "authentication.access-token";
	private const string ExpiresAtKey = "authentication.expires-at";
	private const string RefreshTokenKey = "authentication.refresh-token";

	public async Task<AuthenticationSession?> LoadAsync(CancellationToken cancellationToken)
	{
		var value = await storage.GetAsync(AccessTokenKey).ConfigureAwait(false);
		var expiry = await storage.GetAsync(ExpiresAtKey).ConfigureAwait(false);
		var refresh = await storage.GetAsync(RefreshTokenKey).ConfigureAwait(false);

		// What came out of storage was written by a version of this application that no longer
		// exists. The Application Settings Management entry made that argument about a setting;
		// it is not weaker for a credential. A partial or unreadable store is no session at all.
		if (value is null || expiry is null || refresh is null
			|| !DateTimeOffset.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiresAt))
		{
			return null;
		}

		return new AuthenticationSession(new AccessToken(value, expiresAt), refresh);
	}

	public async Task SaveAsync(AuthenticationSession session, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(session);

		await storage.SetAsync(AccessTokenKey, session.AccessToken.Value).ConfigureAwait(false);
		await storage.SetAsync(ExpiresAtKey, session.AccessToken.ExpiresAt.ToString("O", CultureInfo.InvariantCulture)).ConfigureAwait(false);
		await storage.SetAsync(RefreshTokenKey, session.RefreshToken).ConfigureAwait(false);
	}

	public Task ClearAsync(CancellationToken cancellationToken)
	{
		storage.Remove(AccessTokenKey);
		storage.Remove(ExpiresAtKey);
		storage.Remove(RefreshTokenKey);
		return Task.CompletedTask;
	}
}

/// <summary>
/// Stands in for an identity provider, so a reader can drive the whole token lifetime by hand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is called and no network is reached.</b> There is no authority, no client id, no
/// redirect URI and no endpoint anywhere in this project. A real implementation would use the
/// Microsoft Authentication Library, or <c>WebAuthenticator</c> for a provider MSAL does not cover;
/// both live in the platform head, which is why <c>Authentication.Core</c> declares an interface
/// instead.
/// </para>
/// <para>
/// <b>Every value here announces that it is not a credential.</b> A placeholder that looked
/// plausible would be worse than none: the failure this guards against is a reader copying a
/// realistic-looking literal out of a reference implementation and into something real.
/// </para>
/// </remarks>
public sealed class StandInIdentityProvider(IClock clock) : IIdentityProvider
{
	private const string NotACredential = "not-a-real-token-this-is-a-stand-in";

	private int _issued;

	/// <summary>Whether the next refresh is refused, so a reader can see a session end.</summary>
	public bool RefusesRefresh { get; set; }

	public async Task<AuthenticationSession> SignInAsync(CancellationToken cancellationToken)
	{
		// A delay, so the screen behaves like one waiting on a browser-based sign-in.
		await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);
		return Issue();
	}

	public async Task<AuthenticationSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
	{
		await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);

		if (RefusesRefresh)
		{
			throw new InvalidOperationException("The stand-in provider is refusing to refresh.");
		}

		return Issue();
	}

	private AuthenticationSession Issue()
	{
		var number = Interlocked.Increment(ref _issued).ToString(CultureInfo.InvariantCulture);

		return new AuthenticationSession(
			new AccessToken($"{NotACredential}-{number}", clock.UtcNow + TimeSpan.FromMinutes(5)),
			$"{NotACredential}-refresh-{number}");
	}
}
