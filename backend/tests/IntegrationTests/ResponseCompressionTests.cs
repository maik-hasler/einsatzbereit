using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class ResponseCompressionTests(IntegrationTestFixture fixture)
{
	[Test]
	public async Task GetBadgeCatalog_ShouldReturnBrotliEncodedResponse_WhenClientAcceptsBrotli(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/badges");
		request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

		var response = await httpClient.SendAsync(request, cancellationToken);

		response.Content.Headers.ContentEncoding.Should().Contain("br");
	}

	[Test]
	public async Task GetBadgeCatalog_ShouldReturnGzipEncodedResponse_WhenClientAcceptsGzip(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/badges");
		request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

		var response = await httpClient.SendAsync(request, cancellationToken);

		response.Content.Headers.ContentEncoding.Should().Contain("gzip");
	}

	[Test]
	public async Task GetBadgeCatalog_ShouldReturnUncompressedResponse_WhenClientSendsNoAcceptEncoding(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/badges");

		var response = await httpClient.SendAsync(request, cancellationToken);

		response.Content.Headers.ContentEncoding.Should().BeEmpty(
			"a client that never advertised support for any encoding must get a plain response");
	}
}
