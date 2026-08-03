using System.Xml.Linq;
using Application.Common.Sitemap;
using Application.Organizations;
using Application.Sitemap.GetSitemap.v1;
using Application.VolunteerOpportunities;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Sitemap.GetSitemap;

public class GetSitemapQueryHandlerTests
{
	private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";

	private readonly IVolunteerOpportunityReadRepository _opportunityReadRepository =
		Substitute.For<IVolunteerOpportunityReadRepository>();
	private readonly IOrganizationReadRepository _organizationReadRepository =
		Substitute.For<IOrganizationReadRepository>();
	private readonly GetSitemapQueryHandler _sut;

	public GetSitemapQueryHandlerTests()
	{
		_opportunityReadRepository
			.GetPublishedForSitemapAsync(Arg.Any<CancellationToken>())
			.Returns([]);
		_organizationReadRepository
			.GetAllForSitemapAsync(Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new GetSitemapQueryHandler(_opportunityReadRepository, _organizationReadRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyUrlset_WhenNoOpportunitiesOrOrganizationsExist(
		CancellationToken cancellationToken)
	{
		var xml = await _sut.Handle(new GetSitemapQuery("https://einsatzbereit.example"), cancellationToken);

		var document = XDocument.Parse(xml);
		document.Root!.Name.Should().Be(SitemapNs + "urlset");
		document.Root!.Elements(SitemapNs + "url").Should().BeEmpty();
	}

	[Test]
	public async Task Handle_ShouldIncludeOneUrlEntry_PerOrganizationAndOpportunity(
		CancellationToken cancellationToken)
	{
		var orgId = Guid.NewGuid();
		var opportunityId = Guid.NewGuid();
		_organizationReadRepository
			.GetAllForSitemapAsync(Arg.Any<CancellationToken>())
			.Returns([new SitemapEntry(orgId, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero))]);
		_opportunityReadRepository
			.GetPublishedForSitemapAsync(Arg.Any<CancellationToken>())
			.Returns([new SitemapEntry(opportunityId, new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero))]);

		var xml = await _sut.Handle(new GetSitemapQuery("https://einsatzbereit.example"), cancellationToken);

		var document = XDocument.Parse(xml);
		var locs = document.Root!.Elements(SitemapNs + "url")
			.Select(u => u.Element(SitemapNs + "loc")!.Value)
			.ToList();

		locs.Should().BeEquivalentTo([
			$"https://einsatzbereit.example/organizations/{orgId}",
			$"https://einsatzbereit.example/volunteer-opportunities/{opportunityId}",
		]);
	}

	[Test]
	public async Task Handle_ShouldTrimTrailingSlash_FromBaseUrl(
		CancellationToken cancellationToken)
	{
		var orgId = Guid.NewGuid();
		_organizationReadRepository
			.GetAllForSitemapAsync(Arg.Any<CancellationToken>())
			.Returns([new SitemapEntry(orgId, DateTimeOffset.UtcNow)]);

		var xml = await _sut.Handle(new GetSitemapQuery("https://einsatzbereit.example/"), cancellationToken);

		var document = XDocument.Parse(xml);
		var loc = document.Root!.Element(SitemapNs + "url")!.Element(SitemapNs + "loc")!.Value;

		loc.Should().Be($"https://einsatzbereit.example/organizations/{orgId}");
	}

	[Test]
	public async Task Handle_ShouldFormatLastModified_AsDateOnly(
		CancellationToken cancellationToken)
	{
		var orgId = Guid.NewGuid();
		_organizationReadRepository
			.GetAllForSitemapAsync(Arg.Any<CancellationToken>())
			.Returns([new SitemapEntry(orgId, new DateTimeOffset(2026, 3, 7, 13, 45, 0, TimeSpan.Zero))]);

		var xml = await _sut.Handle(new GetSitemapQuery("https://einsatzbereit.example"), cancellationToken);

		var document = XDocument.Parse(xml);
		var lastmod = document.Root!.Element(SitemapNs + "url")!.Element(SitemapNs + "lastmod")!.Value;

		lastmod.Should().Be("2026-03-07");
	}
}
