using ContainerServices.Core;

namespace ContainerServices.Demo;

/// <summary>
/// Reports the platform from .NET MAUI's <c>DeviceInfo</c>.
/// </summary>
/// <remarks>
/// The whole of the platform dependency, in one file. <c>ContainerServices.Core</c> keeps its own
/// <see cref="RuntimePlatform"/> so the address rule can be tested without a device.
/// </remarks>
internal sealed class MauiRuntimePlatform : IRuntimePlatform
{
	public RuntimePlatform Current
	{
		get
		{
			if (DeviceInfo.Platform == DevicePlatform.Android)
			{
				return RuntimePlatform.Android;
			}

			if (DeviceInfo.Platform == DevicePlatform.iOS)
			{
				return RuntimePlatform.IOS;
			}

			if (DeviceInfo.Platform == DevicePlatform.MacCatalyst)
			{
				return RuntimePlatform.MacCatalyst;
			}

			return DeviceInfo.Platform == DevicePlatform.WinUI
				? RuntimePlatform.Windows
				: RuntimePlatform.Other;
		}
	}
}
