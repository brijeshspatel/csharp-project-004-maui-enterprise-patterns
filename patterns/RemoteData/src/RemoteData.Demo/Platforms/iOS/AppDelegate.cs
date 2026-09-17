using Foundation;

namespace RemoteData.Demo;

// AppDelegate is the .NET MAUI/iOS platform template's own name, matching Apple's
// UIApplicationDelegate convention. Not renamed to satisfy CA1711: the platform-idiomatic
// name is the correct one here, not a naming defect.
#pragma warning disable CA1711
[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
#pragma warning restore CA1711
