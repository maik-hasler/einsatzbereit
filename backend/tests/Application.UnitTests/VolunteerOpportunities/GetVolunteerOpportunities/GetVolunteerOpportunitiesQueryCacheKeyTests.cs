using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using AwesomeAssertions;

namespace Application.UnitTests.VolunteerOpportunities.GetVolunteerOpportunities;

public class GetVolunteerOpportunitiesQueryCacheKeyTests
{
	private static GetVolunteerOpportunitiesQuery CreateQuery(
		int pageNumber = 1,
		int pageSize = 20,
		string? city = "Berlin",
		string[]? categories = null,
		string? tag = null) =>
		new(pageNumber, pageSize, city, null, null, null, null, null, null, null, null, null, null, null, null, categories, tag);

	[Test]
	public void CacheKey_ShouldBeEqual_ForEquivalentQueries()
	{
		CreateQuery().CacheKey.Should().Be(CreateQuery().CacheKey);
	}

	[Test]
	public void CacheKey_ShouldDiffer_WhenPageNumberDiffers()
	{
		CreateQuery(pageNumber: 1).CacheKey.Should().NotBe(CreateQuery(pageNumber: 2).CacheKey);
	}

	[Test]
	public void CacheKey_ShouldDiffer_WhenTagDiffers()
	{
		CreateQuery(tag: "cleanup").CacheKey.Should().NotBe(CreateQuery(tag: "other").CacheKey);
	}

	[Test]
	public void CacheKey_ShouldBeCaseInsensitive_ForCity()
	{
		CreateQuery(city: "berlin").CacheKey.Should().Be(CreateQuery(city: "BERLIN").CacheKey);
	}

	[Test]
	public void CacheKey_ShouldBeOrderIndependent_ForCategories()
	{
		CreateQuery(categories: ["Environment", "Social"]).CacheKey.Should()
			.Be(CreateQuery(categories: ["Social", "Environment"]).CacheKey);
	}

	[Test]
	public void CacheKey_ShouldDiffer_WhenCategoriesDiffer()
	{
		CreateQuery(categories: ["Environment"]).CacheKey.Should().NotBe(CreateQuery(categories: ["Social"]).CacheKey);
	}

	[Test]
	public void CacheKey_ShouldTreatNullAndEmptyCategories_AsEqual()
	{
		CreateQuery(categories: null).CacheKey.Should().Be(CreateQuery(categories: []).CacheKey);
	}
}
