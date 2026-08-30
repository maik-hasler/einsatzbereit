using Application.Common.StaticPages;
using AwesomeAssertions;

namespace Application.UnitTests.Common.StaticPages;

public class StaticPageCatalogTests
{
	[Test]
	public void All_ShouldStartAtTheSiteRoot()
	{
		StaticPageCatalog.All[0].Path.Should().Be("/");
	}

	[Test]
	public void All_ShouldHaveUniqueSlugsAndPaths()
	{
		StaticPageCatalog.All.Select(p => p.Slug).Should().OnlyHaveUniqueItems();
		StaticPageCatalog.All.Select(p => p.Path).Should().OnlyHaveUniqueItems();
	}

	[Test]
	public void All_ShouldUseRootRelativePaths()
	{
		StaticPageCatalog.All.Should().AllSatisfy(page => page.Path.Should().StartWith("/"));
	}

	// nginx.conf.template's /__meta/pages/ location matches ^[a-z-]+$ - a slug
	// outside that set could never be reached through the social-crawler branch.
	[Test]
	public void All_ShouldUseSlugsTheCrawlerRouteCanMatch()
	{
		StaticPageCatalog.All.Should().AllSatisfy(page =>
			page.Slug.Should().MatchRegex("^[a-z-]+$"));
	}

	[Test]
	public void All_ShouldCarryTitleAndDescriptionCopy()
	{
		StaticPageCatalog.All.Should().AllSatisfy(page =>
		{
			page.Title.Should().NotBeNullOrWhiteSpace();
			page.Description.Should().NotBeNullOrWhiteSpace();
		});
	}

	[Test]
	public void Find_ShouldResolveEveryCatalogEntry()
	{
		foreach (var page in StaticPageCatalog.All)
			StaticPageCatalog.Find(page.Slug).Should().Be(page);
	}

	[Test]
	public void Find_ShouldReturnNull_ForAnUnknownSlug()
	{
		StaticPageCatalog.Find("nope").Should().BeNull();
	}
}
