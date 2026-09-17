using System.Net.Security;

namespace ContainerServices.Core;

/// <summary>
/// Whether a server certificate may be accepted.
/// </summary>
/// <remarks>
/// <para>
/// The ASP.NET Core development certificate is self-signed, and neither Android nor iOS trusts a
/// self-signed certificate. Android reports <c>CertPathValidatorException</c> and iOS an
/// <c>NSURLErrorDomain</c> error. Microsoft's documented remedy is a custom validation callback
/// that trusts the development certificate, wrapped in <c>#if DEBUG</c>.
/// </para>
/// <para>
/// <b>The rule here is two conditions, not one.</b> A callback that accepts every certificate when
/// a "development" flag is set is one careless line away — a release build that sets the flag, or a
/// <c>#if DEBUG</c> that somebody widens — from accepting every certificate in production. The
/// issuer check is what makes enabling the bypass insufficient on its own, and it is the condition
/// a test can hold.
/// </para>
/// <para>
/// <c>#if DEBUG</c> is still used at the composition root, and is not the safeguard: it cannot be
/// tested, and this can.
/// </para>
/// </remarks>
public static class DevelopmentCertificatePolicy
{
    /// <summary>
    /// The issuer the ASP.NET Core development certificate presents.
    /// </summary>
    public const string DevelopmentCertificateIssuer = "CN=localhost";

    /// <summary>
    /// Whether to accept the server's certificate.
    /// </summary>
    /// <param name="developmentBypassEnabled">
    /// Set only by a debug build. Necessary for the bypass, and never sufficient.
    /// </param>
    /// <param name="certificateIssuer">The issuer the server presented, or null.</param>
    /// <param name="errors">What the platform found wrong with it.</param>
    public static bool IsAcceptable(
        bool developmentBypassEnabled,
        string? certificateIssuer,
        SslPolicyErrors errors)
    {
        // A certificate the platform already trusts needs no bypass, and this is the branch that
        // runs in production. It is deliberately narrow: anything other than "no errors at all"
        // falls through to the bypass rules below.
        if (errors == SslPolicyErrors.None)
        {
            return true;
        }

        // Both conditions, every time. Neither alone is enough.
        return developmentBypassEnabled
            && string.Equals(certificateIssuer, DevelopmentCertificateIssuer, StringComparison.Ordinal);
    }
}
