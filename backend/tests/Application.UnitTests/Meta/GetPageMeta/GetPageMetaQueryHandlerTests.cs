using Application.Common.StaticPages;
using Application.Meta.GetPageMeta.v1;
using AwesomeAssertions;

namespace Application.UnitTests.Meta.GetPageMeta;

// These carry this endpoint alone: there is deliberately no IntegrationTests
// counterpart. The anonymous Read bucket is 60 permits a minute keyed by the
// connection IP (Api/Common/RateLimiting), and RateLimitingTests and
// MapTileRateLimitingTests each burn a full window on purpose, so the suite's
// shared anonymous budget has no headroom - adding even two requests to it
// made unrelated anonymous tests fail with 429 (einsatzbereit#2331). The
// endpoint's own wiring is the same shape as the two sibling meta endpoints,
// which are integration-tested, and is held by EndpointConventionTests.
public class GetPageMetaQueryHandlerTests
{
	private readonly GetPageMetaQueryHandler _sut = new();

	[Test]
	[Arguments("home", "https://einsatzbereit.example/")]
	[Arguments("opportunities", "https://einsatzbereit.example/opportunities")]
	[Arguments("organizations", "https://einsatzbereit.example/organizations")]
	[Arguments("help", "https://einsatzbereit.example/help")]
	[Arguments("contact", "https://einsatzbereit.example/contact")]
	[Arguments("imprint", "https://einsatzbereit.example/imprint")]
	[Arguments("privacy-policy", "https://einsatzbereit.example/privacy-policy")]
	[Arguments("terms-of-use", "https://einsatzbereit.example/terms-of-use")]
	public async Task Handle_ShouldPointCanonicalAndOgUrl_AtThePageItself(
		string slug,
		string expectedUrl,
		CancellationToken cancellationToken)
	{
		var html = await _sut.Handle(
			new GetPageMetaQuery(slug, "https://einsatzbereit.example"), cancellationToken);

		html.Should().NotBeNull();
		html.Should().Contain($"""<link rel="canonical" href="{expectedUrl}" />""");
		html.Should().Contain($"""<meta property="og:url" content="{expectedUrl}" />""");
	}

	[Test]
	public async Task Handle_ShouldUseThePagesOwnTitle_NotTheSiteWideOne(
		CancellationToken cancellationToken)
	{
		var html = await _sut.Handle(
			new GetPageMetaQuery("help", "https://einsatzbereit.example"), cancellationToken);

		var page = StaticPageCatalog.Find("help");
		page.Should().NotBeNull();
		html.Should().Contain($"<title>{page.Title}</title>");
		html.Should().Contain($"""<meta property="og:title" content="{page.Title}" />""");
		html.Should().Contain($"""<meta name="description" content="{page.Description}" />""");
	}

	[Test]
	public async Task Handle_ShouldFallBackToTheSiteImage(CancellationToken cancellationToken)
	{
		var html = await _sut.Handle(
			new GetPageMetaQuery("imprint", "https://einsatzbereit.example"), cancellationToken);

		html.Should().Contain(
			"""<meta property="og:image" content="https://einsatzbereit.example/og-image.png" />""");
	}

	[Test]
	public async Task Handle_ShouldTrimTrailingSlash_FromBaseUrl(CancellationToken cancellationToken)
	{
		var html = await _sut.Handle(
			new GetPageMetaQuery("contact", "https://einsatzbereit.example/"), cancellationToken);

		html.Should().Contain(
			"""<meta property="og:url" content="https://einsatzbereit.example/contact" />""");
	}

	[Test]
	[Arguments("unknown-page")]
	[Arguments("")]
	[Arguments("Home")]
	public async Task Handle_ShouldReturnNull_ForAnUnknownSlug(
		string slug,
		CancellationToken cancellationToken)
	{
		var html = await _sut.Handle(
			new GetPageMetaQuery(slug, "https://einsatzbereit.example"), cancellationToken);

		html.Should().BeNull();
	}
}
