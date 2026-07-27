using Application.Common.Geocoding;
using Application.Maps.SearchCities.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Maps.SearchCities;

public class SearchCitiesQueryHandlerTests
{
	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly SearchCitiesQueryHandler _sut;

	public SearchCitiesQueryHandlerTests()
	{
		_sut = new SearchCitiesQueryHandler(_geocodingService);
	}

	[Test]
	public async Task Handle_ShouldReturnSuggestions_WhenGeocodingServiceFindsMatches()
	{
		var suggestions = new List<CitySuggestion>
		{
			new("Berlin", 52.52, 13.405),
			new("Bern", 46.948, 7.4474),
		};
		_geocodingService
			.SearchCitiesAsync("Ber", Arg.Any<CancellationToken>())
			.Returns(suggestions);

		var result = await _sut.Handle(new SearchCitiesQuery("Ber"), CancellationToken.None);

		result.Should().BeEquivalentTo(suggestions);
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyList_WhenGeocodingServiceFindsNoMatches()
	{
		_geocodingService
			.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new List<CitySuggestion>());

		var result = await _sut.Handle(new SearchCitiesQuery("Xyzzyxyzzy"), CancellationToken.None);

		result.Should().BeEmpty();
	}

	[Test]
	public async Task Handle_ShouldForwardQueryText_ToGeocodingService()
	{
		_geocodingService
			.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new List<CitySuggestion>());

		await _sut.Handle(new SearchCitiesQuery("Hamburg"), CancellationToken.None);

		await _geocodingService.Received(1).SearchCitiesAsync("Hamburg", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldForwardCancellationToken_ToGeocodingService()
	{
		using var cts = new CancellationTokenSource();
		_geocodingService
			.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new List<CitySuggestion>());

		await _sut.Handle(new SearchCitiesQuery("Hamburg"), cts.Token);

		await _geocodingService.Received(1).SearchCitiesAsync("Hamburg", cts.Token);
	}
}
