using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class ResponseCompressionTests(IntegrationTestFixture fixture)
{
	[Test]
	public async Task GetBadgeCatalog_ShouldCompressWithBrotli_WhenClientAcceptsBrotli(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/badges");
		request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));

		var response = await httpClient.SendAsync(request, cancellationToken);

		response.Content.Headers.ContentEncoding.Should().Contain("br");
	}

	[Test]
	public async Task GetBadgeCatalog_ShouldCompressWithGzip_WhenClientOnlyAcceptsGzip(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();
		using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/badges");
		request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

		var response = await httpClient.SendAsync(request, cancellationToken);

		response.Content.Headers.ContentEncoding.Should().Contain("gzip");
	}

	[Test]
	public async Task GetBadgeCatalog_ShouldNotCompress_WhenClientSendsNoAcceptEncoding(CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/v1/badges", cancellationToken);

		response.Content.Headers.ContentEncoding.Should().BeEmpty();
	}
}
