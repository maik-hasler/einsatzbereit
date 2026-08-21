using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1089: <c>index.html</c>'s <c>og:image</c>/<c>twitter:image</c>
/// meta tags pointed at <c>/og-image.png</c>, but no such file existed in
/// <c>frontend/public/</c> - every shared link (WhatsApp, Signal, Mastodon,
/// Facebook, ...) rendered a broken image in its link preview. Asserts the
/// referenced file actually exists and is served as an image, not just that
/// the meta tag is present.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SocialPreviewImageTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task HomePage_OgImageMetaTag_PointsToAnImageThatActuallyExists()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync(origin);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var ogImageContent = await Page.Locator("meta[property='og:image']").GetAttributeAsync("content");
		var twitterImageContent = await Page.Locator("meta[name='twitter:image']").GetAttributeAsync("content");
		ogImageContent.Should().NotBeNullOrEmpty();
		twitterImageContent.Should().Be(ogImageContent);

		var imagePath = new Uri(ogImageContent!).PathAndQuery;

		using var http = new HttpClient { BaseAddress = frontend };
		var response = await http.GetAsync(imagePath);

		response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK,
			$"the og:image meta tag points at {imagePath}, which nginx must actually serve");
		response.Content.Headers.ContentType?.MediaType.Should().Be("image/png");

		var bytes = await response.Content.ReadAsByteArrayAsync();
		bytes.Should().NotBeEmpty();
	}
}
