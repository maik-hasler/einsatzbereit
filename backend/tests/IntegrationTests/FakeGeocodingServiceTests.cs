using Application.Common.Geocoding;
using AwesomeAssertions;
using Infrastructure.Geocoding;
using Infrastructure.Geocoding.GermanPlaces;

namespace IntegrationTests;

public class FakeGeocodingServiceTests
{
	private readonly FakeGeocodingService _sut = new(new GermanPlaceDirectory());

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

	[Test]
	public async Task SearchCitiesAsync_PostalCode_ResolvesLocallyToo()
	{
		var results = await _sut.SearchCitiesAsync("26129", "de");

		results.Should().ContainSingle(r => r.Label == "26129 Oldenburg");
	}
}
