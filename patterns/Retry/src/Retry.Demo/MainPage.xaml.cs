using Retry.Core;

namespace Retry.Demo;

/// <summary>
/// Runs a flaky operation and shows every attempt.
/// </summary>
public partial class MainPage : ContentPage
{
	public MainPage(FlakyOperationViewModel operation)
	{
		InitializeComponent();
		Operation = operation;
	}

	public FlakyOperationViewModel Operation { get; }
}
