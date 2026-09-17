using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Authentication.Core;

/// <summary>
/// The screen a user signs in and out from, and which reports the state of the session.
/// </summary>
/// <remarks>
/// <b>It never displays a token.</b> The status line prints the <see cref="AccessToken"/> object,
/// which redacts itself. That is the point of the redaction: a screen, a log and a crash report all
/// reach a credential the same way, through a format string written by someone who was not thinking
/// about credentials at the time.
/// </remarks>
public sealed partial class SessionViewModel(AccessTokenProvider tokens) : ObservableObject
{
    [ObservableProperty]
    private string _status = "Not signed in.";

    [ObservableProperty]
    private bool _isSignedIn;

    /// <summary>Sign in interactively.</summary>
    [RelayCommand]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        try
        {
            await tokens.SignInAsync(cancellationToken).ConfigureAwait(true);
            Status = "Signed in.";
            IsSignedIn = true;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            Status = $"Sign-in failed. {failure.Message}";
            IsSignedIn = false;
        }
    }

    /// <summary>
    /// Acquire a token the way a request would, and report what came back.
    /// </summary>
    [RelayCommand]
    private async Task UseTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(true);

            // The token object, not its value. A reader who changes this line to token.Value has
            // just written the credential to the screen, and would do the same to a log.
            Status = $"Acquired {token}";
            IsSignedIn = true;
        }
        catch (AuthenticationRequiredException failure)
        {
            Status = $"Sign-in required. {failure.Reason}";
            IsSignedIn = false;
        }
    }

    /// <summary>End the session.</summary>
    [RelayCommand]
    private async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await tokens.SignOutAsync(cancellationToken).ConfigureAwait(true);
        Status = "Signed out.";
        IsSignedIn = false;
    }
}
