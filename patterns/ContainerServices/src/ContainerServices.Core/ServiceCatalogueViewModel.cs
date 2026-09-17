using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ContainerServices.Core;

/// <summary>One service and where it is reached from on this platform.</summary>
public sealed record ResolvedEndpoint(string Name, int Port, Uri BaseAddress);

/// <summary>
/// Shows where each containerized service is reached from, on the platform actually running.
/// </summary>
/// <remarks>
/// The address rule is invisible until it is wrong, and then it looks like the service being down.
/// Putting the resolved addresses on screen makes the rule inspectable on the device where it
/// matters.
/// </remarks>
public sealed partial class ServiceCatalogueViewModel : ObservableObject
{
    public ServiceCatalogueViewModel(IRuntimePlatform platform, IEnumerable<ContainerService> services, string scheme = "https")
    {
        Platform = platform.Current;
        Host = ServiceEndpoints.HostFor(Platform);

        Endpoints = new ObservableCollection<ResolvedEndpoint>(
            services.Select(service => new ResolvedEndpoint(
                service.Name,
                service.Port,
                ServiceEndpoints.BaseAddressFor(service, Platform, scheme))));
    }

    public RuntimePlatform Platform { get; }

    /// <summary>The host every service on this platform is reached through.</summary>
    public string Host { get; }

    public ObservableCollection<ResolvedEndpoint> Endpoints { get; }

    /// <summary>
    /// The one case no address rule can solve.
    /// </summary>
    /// <remarks>
    /// An iOS app running in the simulator from a Windows machine executes on the paired Mac, so
    /// the Windows host's loopback is not reachable at all. The platform is iOS and the rule
    /// correctly chooses localhost — which is the Mac.
    /// </remarks>
    public string Caveat =>
        Platform == RuntimePlatform.IOS
            ? "If this simulator is paired from a Windows machine, the app runs on the Mac, and localhost is that Mac. Use the Windows machine's network address instead."
            : string.Empty;
}
