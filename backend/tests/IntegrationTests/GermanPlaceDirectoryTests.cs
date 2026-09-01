using AwesomeAssertions;
using Infrastructure.Geocoding.GermanPlaces;

namespace IntegrationTests;

public class GermanPlaceDirectoryTests
{
	private readonly GermanPlaceDirectory _sut = new();

	[Test]
	public void Search_IncompleteCityName_ReturnsCitiesStartingWithIt()
	{
		var results = _sut.Search("Leip", 6);

		results.Should().Contain(r => r.Label == "Leipzig");
		results.Should().OnlyContain(r => r.Label.StartsWith("Leip"));
	}

	[Test]
	public void Search_OrdersMatchesByPopulationDescending()
	{
		var results = _sut.Search("Leip", 6);

		results.Select(r => r.Label).Should().ContainInConsecutiveOrder("Leipzig", "Leipheim");
	}

	[Test]
	public void Search_IsCaseInsensitive()
	{
		var results = _sut.Search("BERLIN", 6);

		results.Should().Contain(r => r.Label == "Berlin");
	}

	[Test]
	public void Search_AsciiTranscriptionOfAnUmlaut_MatchesTheNativeSpelling()
	{
		var results = _sut.Search("Muenchen", 6);

		results.Should().ContainSingle(r => r.Label == "München");
	}

	[Test]
	public void Search_EszettTranscribedAsSs_MatchesTheNativeSpelling()
	{
		var results = _sut.Search("Giessen", 6);

		results.Should().Contain(r => r.Label == "Gießen");
	}

	[Test]
	public void Search_RespectsTheRequestedLimit()
	{
		var results = _sut.Search("B", 3);

		results.Should().HaveCount(3);
	}

	[Test]
	public void Search_QueryMatchingNoPlace_ReturnsEmpty()
	{
		var results = _sut.Search("Wolkenkuckucksheim", 6);

		results.Should().BeEmpty();
	}

	[Test]
	public void Search_EmptyQuery_ReturnsEmpty()
	{
		var results = _sut.Search("   ", 6);

		results.Should().BeEmpty();
	}

	[Test]
	public void Search_MatchOnlyAsAContainedSubstring_IsNotReturned()
	{
		// "eipzig" is a real substring of "Leipzig" but starts neither the name nor
		// any word in it - nobody types a city from the middle, and matching that
		// way only fills the six slots with noise.
		var results = _sut.Search("eipzig", 6);

		results.Should().BeEmpty();
	}

	[Test]
	public void Search_WordInsideTheName_MatchesTheQualifiedCity()
	{
		// "Bad Homburg vor der Höhe" is filed under its qualifier, but nobody types
		// the "Bad" first.
		var results = _sut.Search("Homburg", 6);

		results.Should().Contain(r => r.Label.Contains("Bad Homburg"));
	}

	[Test]
	public void Search_NamePrefix_OutranksAWordDeeperInTheName()
	{
		var results = _sut.Search("Frankfurt", 6);

		results[0].Label.Should().Be("Frankfurt am Main");
	}

	[Test]
	public void Search_ExactName_OutranksALongerCityWithMorePeople()
	{
		// "Halle" is smaller than "Halle (Saale)" - typing a city's full name has to
		// put that city first regardless.
		var results = _sut.Search("Halle", 6);

		results[0].Label.Should().Be("Halle");
	}

	[Test]
	public void Search_PostalCode_ResolvesToItsTownAndKeepsTheCodeInTheLabel()
	{
		// The location field advertises "city or postal code" - typing 26129 used to
		// find nothing at all.
		var results = _sut.Search("26129", 6);

		results.Should().ContainSingle(r => r.Label == "26129 Oldenburg");
	}

	[Test]
	public void Search_PartialPostalCode_ReturnsTheCodesStartingWithIt()
	{
		var results = _sut.Search("261", 6);

		results.Should().HaveCount(6);
		results.Should().OnlyContain(r => r.Label.StartsWith("261"));
	}

	[Test]
	public void Search_PostalCodeReservedForASingleCompany_IsNotAPlace()
	{
		// 70140 is a "Grossempfaenger" code belonging to Commerzbank AG, not a town
		// anyone volunteers in. Nominatim still answers for it - see
		// NominatimGeocodingService's fallback.
		var results = _sut.Search("70140", 6);

		results.Should().BeEmpty();
	}

	[Test]
	public void Search_PostalCodeCoordinates_ArePinnedToTheCodeNotTheCity()
	{
		var results = _sut.Search("26129", 6);

		// Oldenburg's city centre is 53.1387/8.2146; 26129 is the Wechloy district
		// north-west of it.
		results[0].Latitude.Should().BeApproximately(53.1547, 0.001);
		results[0].Longitude.Should().BeApproximately(8.1706, 0.001);
	}
}
