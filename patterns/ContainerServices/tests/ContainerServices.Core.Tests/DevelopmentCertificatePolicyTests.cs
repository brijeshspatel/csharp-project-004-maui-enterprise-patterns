using System.Net.Security;
using ContainerServices.Core;

namespace ContainerServices.Core.Tests;

/// <summary>
/// The rule that decides whether a self-signed development certificate is accepted.
/// </summary>
/// <remarks>
/// These tests exist because the difference between "trust the development certificate" and "trust
/// everything" is one condition, and the second is a shipped vulnerability.
/// </remarks>
public class DevelopmentCertificatePolicyTests
{
    private const string Development = DevelopmentCertificatePolicy.DevelopmentCertificateIssuer;
    private const string Anything = "CN=attacker.example";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACertificateWithNoErrors_IsAccepted_WithOrWithoutTheBypass(bool bypass)
    {
        // The production branch. A certificate the platform already trusts needs no bypass.
        Assert.True(DevelopmentCertificatePolicy.IsAcceptable(bypass, Anything, SslPolicyErrors.None));
    }

    [Fact]
    public void ACertificateWithAnyError_AndNoBypass_IsRejected()
    {
        // Pins the production branch from the other side. Without this, widening the first
        // condition — to anything short of "no errors at all" — would pass every other test here.
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(false, Development, SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(false, Development, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(false, Development, SslPolicyErrors.RemoteCertificateNotAvailable));
    }

    [Fact]
    public void TheDevelopmentCertificate_IsAccepted_OnlyWhenTheBypassIsEnabled()
    {
        Assert.True(DevelopmentCertificatePolicy.IsAcceptable(true, Development, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(false, Development, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void EnablingTheBypass_IsNotSufficient_AnyOtherIssuerIsStillRejected()
    {
        // The safeguard, and the reason this rule is two conditions rather than a flag. A callback
        // that returned true whenever the bypass was on would pass every other test in this class.
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(true, Anything, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(true, "CN=localhost.attacker.example", SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(true, "cn=localhost", SslPolicyErrors.RemoteCertificateChainErrors));
    }

    [Fact]
    public void AMissingIssuer_IsRejected_EvenWithTheBypassEnabled()
    {
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(true, null, SslPolicyErrors.RemoteCertificateNotAvailable));
        Assert.False(DevelopmentCertificatePolicy.IsAcceptable(true, string.Empty, SslPolicyErrors.RemoteCertificateChainErrors));
    }
}
