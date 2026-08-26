using Application.Common.Geocoding;
using AwesomeAssertions;
using Infrastructure.Geocoding;
using Infrastructure.Geocoding.GermanCities;

namespace IntegrationTests;

public class FakeGeocodingServiceTests
{
	private readonly FakeGeocodingService _sut = new(new GermanCityDirectory());

	[Test]
	public async Task GeocodeAsync_AnyAddress_ReturnsTransientFailure()
	{
		var result = await _sut.GeocodeAsync("Karl-Heine-Straße", "12", "04177", "Leipzig");

		result.Outcome.Should().Be(GeocodingOutcome.TransientFailure);
	}

	[Test]
	public async Task SearchCitiesAsync_PrefixOfARealCity_ReturnsSuggestions_WithoutAnyNetworkCall()
	{
		var results = await _sut.SearchCitiesAsync("Leip", "de");

		results.Should().Contain(r => r.Label == "Leipzig");
	}
}
