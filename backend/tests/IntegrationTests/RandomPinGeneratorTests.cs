using System.Text.RegularExpressions;
using AwesomeAssertions;
using Domain.VolunteerOpportunities;
using Infrastructure.VolunteerOpportunities;

namespace IntegrationTests;

// Pure generation logic - no database needed - but RandomPinGenerator is internal
// to Infrastructure (InternalsVisibleTo only grants IntegrationTests, see
// Infrastructure.csproj), so this has to live here rather than in a project that
// can't see it.
public partial class RandomPinGeneratorTests
{
	private readonly RandomPinGenerator _sut = new();

	[Test]
	public void GeneratePin_ShouldReturnSixDigits()
	{
		var pin = _sut.GeneratePin();

		SixDigitsRegex().IsMatch(pin).Should().BeTrue($"'{pin}' must be exactly 6 digits");
	}

	[Test]
	public void GeneratePin_ShouldNeverReturnATrivialPin()
	{
		// 500 draws over a 1,000,000-value space gives no realistic chance of
		// missing a bug that let a trivial PIN slip through, without making the
		// test itself slow.
		for (var i = 0; i < 500; i++)
		{
			var pin = _sut.GeneratePin();
			CheckInPinPolicy.IsTrivial(pin).Should().BeFalse($"'{pin}' should have been rejected and re-rolled");
		}
	}

	[Test]
	public void GeneratePin_ShouldVaryAcrossCalls()
	{
		var pins = Enumerable.Range(0, 20).Select(_ => _sut.GeneratePin()).ToHashSet();

		pins.Count.Should().BeGreaterThan(1, "a cryptographically random generator should not repeatedly draw the same value");
	}

	[GeneratedRegex(@"^\d{6}$")]
	private static partial Regex SixDigitsRegex();
}
