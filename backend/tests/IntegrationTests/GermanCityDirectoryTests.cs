using AwesomeAssertions;
using Infrastructure.Geocoding.GermanCities;

namespace IntegrationTests;

public class GermanCityDirectoryTests
{
	private readonly GermanCityDirectory _sut = new();

	[Test]
	public void SearchByPrefix_IncompleteCityName_ReturnsCitiesStartingWithIt()
	{
		var results = _sut.SearchByPrefix("Leip", 6);

		results.Should().Contain(r => r.Label == "Leipzig");
		results.Should().OnlyContain(r => r.Label.StartsWith("Leip"));
	}

	[Test]
	public void SearchByPrefix_OrdersMatchesByPopulationDescending()
	{
		var results = _sut.SearchByPrefix("Leip", 6);

		results.Select(r => r.Label).Should().ContainInConsecutiveOrder("Leipzig", "Leipheim");
	}

	[Test]
	public void SearchByPrefix_IsCaseInsensitive()
	{
		var results = _sut.SearchByPrefix("BERLIN", 6);

		results.Should().Contain(r => r.Label == "Berlin");
	}

	[Test]
	public void SearchByPrefix_AsciiTranscriptionOfAnUmlaut_MatchesTheNativeSpelling()
	{
		var results = _sut.SearchByPrefix("Muenchen", 6);

		results.Should().ContainSingle(r => r.Label == "München");
	}

	[Test]
	public void SearchByPrefix_EszettTranscribedAsSs_MatchesTheNativeSpelling()
	{
		var results = _sut.SearchByPrefix("Giessen", 6);

		results.Should().Contain(r => r.Label == "Gießen");
	}

	[Test]
	public void SearchByPrefix_RespectsTheRequestedLimit()
	{
		var results = _sut.SearchByPrefix("B", 3);

		results.Should().HaveCount(3);
	}

	[Test]
	public void SearchByPrefix_QueryMatchingNoCity_ReturnsEmpty()
	{
		var results = _sut.SearchByPrefix("9nonexistent", 6);

		results.Should().BeEmpty();
	}

	[Test]
	public void SearchByPrefix_EmptyQuery_ReturnsEmpty()
	{
		var results = _sut.SearchByPrefix("   ", 6);

		results.Should().BeEmpty();
	}

	[Test]
	public void SearchByPrefix_MatchOnlyAsAContainedSubstring_IsNotReturned()
	{
		// "eipzig" is a real substring of "Leipzig" but not a prefix of any
		// German city - the directory must not fall back to substring search.
		var results = _sut.SearchByPrefix("eipzig", 6);

		results.Should().BeEmpty();
	}
}
