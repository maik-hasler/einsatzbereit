using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

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
