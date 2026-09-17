using ContainerServices.Core;

namespace ContainerServices.Core.Tests;

internal sealed class FixedPlatform(RuntimePlatform platform) : IRuntimePlatform
{
    public RuntimePlatform Current => platform;
}

public class ServiceEndpointsTests
{
    private static readonly ContainerService Orders = new("orders", 5001);
    private static readonly ContainerService Catalogue = new("catalogue", 5002);

    [Fact]
    public void Android_ResolvesToTheEmulatorsHostAlias_OnTheServicesOwnPort()
    {
        // The emulator is isolated from the host's network interfaces; localhost inside it is the
        // emulator. 10.0.2.2 is the documented alias to the host's loopback.
        var address = ServiceEndpoints.BaseAddressFor(Orders, RuntimePlatform.Android);

        Assert.Equal("https://10.0.2.2:5001/", address.ToString());
    }

    [Theory]
    [InlineData(RuntimePlatform.IOS)]
    [InlineData(RuntimePlatform.MacCatalyst)]
    [InlineData(RuntimePlatform.Windows)]
    public void PlatformsOnTheHostNetwork_ResolveToLocalhost(RuntimePlatform platform)
    {
        var address = ServiceEndpoints.BaseAddressFor(Orders, platform);

        Assert.Equal("https://localhost:5001/", address.ToString());
    }

    [Fact]
    public void AnUnrecognisedPlatform_ResolvesToLocalhostRatherThanFailing()
    {
        // Most likely a desktop test host, which reaches the host machine directly. Refusing to
        // resolve would break a test run for a case that has a sensible answer.
        Assert.Equal("https://localhost:5001/", ServiceEndpoints.BaseAddressFor(Orders, RuntimePlatform.Other).ToString());
    }

    [Fact]
    public void EachServiceKeepsItsOwnPort_OnTheSameHost()
    {
        var orders = ServiceEndpoints.BaseAddressFor(Orders, RuntimePlatform.Android);
        var catalogue = ServiceEndpoints.BaseAddressFor(Catalogue, RuntimePlatform.Android);

        Assert.Equal(orders.Host, catalogue.Host);
        Assert.NotEqual(orders.Port, catalogue.Port);
        Assert.Equal(5002, catalogue.Port);
    }

    [Fact]
    public void TheSchemeIsCarriedThrough()
    {
        Assert.Equal("http://10.0.2.2:5001/", ServiceEndpoints.BaseAddressFor(Orders, RuntimePlatform.Android, "http").ToString());
        Assert.Equal("https://10.0.2.2:5001/", ServiceEndpoints.BaseAddressFor(Orders, RuntimePlatform.Android, "https").ToString());
    }

    [Fact]
    public void TheCatalogueResolvesEveryServiceForTheRunningPlatform()
    {
        var catalogue = new ServiceCatalogueViewModel(
            new FixedPlatform(RuntimePlatform.Android),
            [Orders, Catalogue]);

        Assert.Equal(ServiceEndpoints.AndroidHostLoopback, catalogue.Host);
        Assert.Collection(
            catalogue.Endpoints,
            first => Assert.Equal("https://10.0.2.2:5001/", first.BaseAddress.ToString()),
            second => Assert.Equal("https://10.0.2.2:5002/", second.BaseAddress.ToString()));
    }

    [Fact]
    public void OnIOS_TheCatalogueCarriesTheCaseNoAddressRuleCanSolve()
    {
        // An iOS simulator paired from Windows runs the app on the Mac, so localhost is the Mac.
        // The rule is right and the answer is still unreachable, so the screen says so.
        var ios = new ServiceCatalogueViewModel(new FixedPlatform(RuntimePlatform.IOS), [Orders]);
        var android = new ServiceCatalogueViewModel(new FixedPlatform(RuntimePlatform.Android), [Orders]);

        Assert.Contains("runs on the Mac", ios.Caveat);
        Assert.Equal(string.Empty, android.Caveat);
    }
}
