using AwesomeAssertions;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.VolunteerOpportunities;

public class GeoMathTests
{
	[Test]
	public void DistanceKm_ShouldReturnZero_ForSamePoint()
	{
		GeoMath.DistanceKm(52.52, 13.405, 52.52, 13.405).Should().BeApproximately(0, 0.001);
	}

	[Test]
	public void DistanceKm_ShouldApproximateKnownDistance_BerlinToMunich()
	{
		var distance = GeoMath.DistanceKm(52.52, 13.405, 48.137, 11.575);

		distance.Should().BeInRange(495, 515);
	}

	[Test]
	public void BoundingBoxFor_ShouldContainCenter()
	{
		var box = GeoMath.BoundingBoxFor(52.52, 13.405, 10);

		box.South.Should().BeLessThan(52.52);
		box.North.Should().BeGreaterThan(52.52);
		box.West.Should().BeLessThan(13.405);
		box.East.Should().BeGreaterThan(13.405);
	}

	[Test]
	public void BoundingBoxFor_ShouldBeWiderForLargerRadius()
	{
		var small = GeoMath.BoundingBoxFor(52.52, 13.405, 5);
		var large = GeoMath.BoundingBoxFor(52.52, 13.405, 50);

		(large.North - large.South).Should().BeGreaterThan(small.North - small.South);
	}
}
