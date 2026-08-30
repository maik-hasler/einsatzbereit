using System.Net;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class GetPageMetaTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetPageMeta_ShouldReturnNotFound_ForAnUnknownSlug(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/v1/meta/pages/nope", cancellationToken);

		response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	// Deliberately one route, not all eight: the anonymous Read bucket is 60
	// permits a minute keyed by client IP (Api/Common/RateLimiting), so every
	// anonymous request in this suite draws on one shared budget - a case per
	// static page exhausted it and unrelated tests started getting 429s. What
	// needs a live server here is the wiring (route, status, content type); that
	// every slug in StaticPageCatalog resolves to its own URL is settled far
	// more cheaply by GetPageMetaQueryHandlerTests.
	[Test]
	public async Task GetPageMeta_ShouldPointCanonicalAtThePageItself(
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync("/v1/meta/pages/help", cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		response.EnsureSuccessStatusCode();
		response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

		// The defect was every static route falling through to index.html, whose
		// og:url is one hardcoded string naming the site root - so /help pasted
		// into a chat previewed as the homepage (einsatzbereit#2331).
		html.Should().MatchRegex("""<meta property="og:url" content="https?://[^"]+/help" />""");
		html.Should().MatchRegex("""<link rel="canonical" href="https?://[^"]+/help" />""");
		html.Should().Contain("/og-image.png");
	}
}
