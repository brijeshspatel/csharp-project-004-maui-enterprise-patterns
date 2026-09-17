namespace ContainerServices.Core;

/// <summary>
/// The platform the application is running on, as this pattern needs it.
/// </summary>
/// <remarks>
/// Declared here rather than using .NET MAUI's <c>DeviceInfo.Platform</c>. That type lives in
/// <c>Microsoft.Maui.Devices</c>, and referencing MAUI from a plain <c>net10.0</c> project compiles
/// and then throws <c>NotImplementedInReferenceAssemblyException</c> when called — established by
/// probe during the Application Settings Management run.
/// </remarks>
public enum RuntimePlatform
{
    Android,
    IOS,
    MacCatalyst,
    Windows,

    /// <summary>Anything else, including a desktop test host.</summary>
    Other,
}

/// <summary>Which platform this is.</summary>
public interface IRuntimePlatform
{
    RuntimePlatform Current { get; }
}

/// <summary>
/// One containerized service, and the port its container publishes.
/// </summary>
public sealed record ContainerService(string Name, int Port);

/// <summary>
/// Where a containerized service running on the developer's machine can be reached from.
/// </summary>
/// <remarks>
/// <para>
/// The host differs by platform, and getting it wrong produces a connection failure that looks
/// like the service being down.
/// </para>
/// <para>
/// This is the whole of what containerization changes for the client. What the client then does
/// with the address — one attempt, what is cached, how failure is reported — is
/// <c>Accessing Remote Data (REST)</c>, and is not repeated here.
/// </para>
/// </remarks>
public static class ServiceEndpoints
{
    /// <summary>
    /// The Android emulator's alias for the host's loopback interface.
    /// </summary>
    /// <remarks>
    /// Each emulator instance is isolated from the development machine's network interfaces and
    /// runs behind a virtual router, so <c>localhost</c> inside the emulator is the emulator. This
    /// address is documented as an alias to 127.0.0.1 on the host.
    /// </remarks>
    public const string AndroidHostLoopback = "10.0.2.2";

    /// <summary>What every other platform here uses.</summary>
    public const string HostLoopback = "localhost";

    /// <summary>
    /// The host to use for a service running in a container on the developer's machine.
    /// </summary>
    /// <remarks>
    /// <c>Other</c> resolves to <c>localhost</c> rather than throwing. An unrecognised platform is
    /// most likely a desktop test host, which reaches the host machine directly, and refusing to
    /// resolve would break a test run for a case that has a sensible answer.
    /// </remarks>
    public static string HostFor(RuntimePlatform platform) =>
        platform == RuntimePlatform.Android ? AndroidHostLoopback : HostLoopback;

    /// <summary>
    /// The base address of <paramref name="service"/> from <paramref name="platform"/>.
    /// </summary>
    public static Uri BaseAddressFor(ContainerService service, RuntimePlatform platform, string scheme = "https") =>
        new($"{scheme}://{HostFor(platform)}:{service.Port}/");
}
