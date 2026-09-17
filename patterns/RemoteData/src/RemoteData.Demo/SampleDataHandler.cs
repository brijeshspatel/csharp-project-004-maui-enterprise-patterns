using System.Net;

namespace RemoteData.Demo;

/// <summary>
/// Answers every request from a constant, so this demonstration reaches no network.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing in this repository calls a service.</b> The handler is named for what it is, and the
/// screen says so, because canned orders returned through a real <see cref="HttpClient"/> are
/// indistinguishable on screen from fetched ones — and a reference implementation should not let a
/// reader believe otherwise.
/// </para>
/// <para>
/// It is also the same seam the tests use. That is the point of choosing
/// <see cref="HttpMessageHandler"/> rather than an interface over the client: the demonstration and
/// the tests substitute at the framework's own extension point, and the code under both is the real
/// client.
/// </para>
/// </remarks>
internal sealed class SampleDataHandler : HttpMessageHandler
{
	private const string Orders =
		"""
		[
		  { "id": 1, "customerReference": "ACME-01", "total": 100.00 },
		  { "id": 2, "customerReference": "ACME-02", "total": 250.00 },
		  { "id": 3, "customerReference": "GLOBEX-01", "total": 400.00 }
		]
		""";

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
		Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(Orders, System.Text.Encoding.UTF8, "application/json"),
		});
}
