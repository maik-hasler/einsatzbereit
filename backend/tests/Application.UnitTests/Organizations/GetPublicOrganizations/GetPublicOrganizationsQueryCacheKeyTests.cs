using Application.Organizations.GetPublicOrganizations.v1;
using AwesomeAssertions;

namespace Application.UnitTests.Organizations.GetPublicOrganizations;

public class GetPublicOrganizationsQueryCacheKeyTests
{
	[Test]
	public void CacheKey_ShouldBeEqual_WhenSearchCasingDiffers()
	{
		new GetPublicOrganizationsQuery(1, 20, "berlin").CacheKey.Should()
			.Be(new GetPublicOrganizationsQuery(1, 20, "BERLIN").CacheKey);
	}

	[Test]
	public void CacheKey_ShouldTreatNullAndEmptySearch_AsEqual()
	{
		new GetPublicOrganizationsQuery(1, 20, null).CacheKey.Should()
			.Be(new GetPublicOrganizationsQuery(1, 20, "").CacheKey);
	}

	[Test]
	public void CacheKey_ShouldDiffer_WhenPageNumberDiffers()
	{
		new GetPublicOrganizationsQuery(1, 20, "berlin").CacheKey.Should()
			.NotBe(new GetPublicOrganizationsQuery(2, 20, "berlin").CacheKey);
	}

	[Test]
	public void CacheKey_ShouldDiffer_WhenSearchDiffers()
	{
		new GetPublicOrganizationsQuery(1, 20, "berlin").CacheKey.Should()
			.NotBe(new GetPublicOrganizationsQuery(1, 20, "hamburg").CacheKey);
	}
}
