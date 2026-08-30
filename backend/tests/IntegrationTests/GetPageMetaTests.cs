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

	[Test]
	[Arguments("home", "/")]
	[Arguments("opportunities", "/opportunities")]
	[Arguments("organizations", "/organizations")]
	[Arguments("help", "/help")]
	[Arguments("contact", "/contact")]
	[Arguments("imprint", "/imprint")]
	[Arguments("privacy-policy", "/privacy-policy")]
	[Arguments("terms-of-use", "/terms-of-use")]
	public async Task GetPageMeta_ShouldPointCanonicalAtThePageItself(
		string slug,
		string path,
		CancellationToken cancellationToken)
	{
		using var httpClient = fixture.CreateHttpClient();

		var response = await httpClient.GetAsync($"/v1/meta/pages/{slug}", cancellationToken);
		var html = await response.Content.ReadAsStringAsync(cancellationToken);

		response.EnsureSuccessStatusCode();
		response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

		// The defect was every static route falling through to index.html, whose
		// og:url is one hardcoded string naming the site root - so /help pasted
		// into a chat previewed as the homepage (einsatzbereit#2331).
		html.Should().MatchRegex($"""<meta property="og:url" content="https?://[^"]+{path}" />""");
		html.Should().MatchRegex($"""<link rel="canonical" href="https?://[^"]+{path}" />""");
		html.Should().Contain("/og-image.png");
	}
}
