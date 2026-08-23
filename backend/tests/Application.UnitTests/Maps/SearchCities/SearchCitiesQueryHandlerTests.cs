using Application.Common.Geocoding;
using Application.Maps.SearchCities.v1;
using AwesomeAssertions;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Application.UnitTests.Maps.SearchCities;

public sealed class SearchCitiesQueryHandlerTests : IDisposable
{
	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());
	private readonly SearchCitiesQueryHandler _sut;

	public SearchCitiesQueryHandlerTests()
	{
		_sut = new SearchCitiesQueryHandler(_geocodingService, _cache);
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
			.SearchCitiesAsync("Ber", "en", Arg.Any<CancellationToken>())
			.Returns(suggestions);

		var result = await _sut.Handle(new SearchCitiesQuery("Ber", "en"), CancellationToken.None);

		result.Should().BeEquivalentTo(suggestions);
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyList_WhenGeocodingServiceFindsNoMatches()
	{
		_geocodingService
			.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new List<CitySuggestion>());

		var result = await _sut.Handle(new SearchCitiesQuery("Xyzzyxyzzy", "en"), CancellationToken.None);

		result.Should().BeEmpty();
	}

	[Test]
	public async Task Handle_ShouldForwardQueryTextAndLanguage_ToGeocodingService()
	{
		_geocodingService
			.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new List<CitySuggestion>());

		await _sut.Handle(new SearchCitiesQuery("Hamburg", "de"), CancellationToken.None);

		await _geocodingService.Received(1).SearchCitiesAsync("Hamburg", "de", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldForwardCancellationToken_ToGeocodingService()
	{
		using var cts = new CancellationTokenSource();
		_geocodingService
			.SearchCitiesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new List<CitySuggestion>());

		await _sut.Handle(new SearchCitiesQuery("Hamburg", "en"), cts.Token);

		await _geocodingService.Received(1).SearchCitiesAsync("Hamburg", "en", cts.Token);
	}

	[Test]
	public async Task Handle_ShouldNotCallGeocodingServiceTwice_ForRepeatedIdenticalQuery()
	{
		var suggestions = new List<CitySuggestion> { new("Hamburg", 53.5511, 9.9937) };
		_geocodingService
			.SearchCitiesAsync("Hamburg", "en", Arg.Any<CancellationToken>())
			.Returns(suggestions);

		var first = await _sut.Handle(new SearchCitiesQuery("Hamburg", "en"), CancellationToken.None);
		var second = await _sut.Handle(new SearchCitiesQuery("Hamburg", "en"), CancellationToken.None);

		first.Should().BeEquivalentTo(suggestions);
		second.Should().BeEquivalentTo(suggestions);
		await _geocodingService.Received(1).SearchCitiesAsync("Hamburg", "en", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldTreatQuery_CaseAndWhitespaceInsensitively_ForCaching()
	{
		var suggestions = new List<CitySuggestion> { new("Hamburg", 53.5511, 9.9937) };
		_geocodingService
			.SearchCitiesAsync("Hamburg", "en", Arg.Any<CancellationToken>())
			.Returns(suggestions);

		await _sut.Handle(new SearchCitiesQuery("Hamburg", "en"), CancellationToken.None);
		var second = await _sut.Handle(new SearchCitiesQuery("  HAMBURG  ", "en"), CancellationToken.None);

		second.Should().BeEquivalentTo(suggestions);
		await _geocodingService.Received(1).SearchCitiesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotCacheEmptyResult_SoARetryCanStillFindMatches()
	{
		_geocodingService
			.SearchCitiesAsync("Hamburg", "en", Arg.Any<CancellationToken>())
			.Returns(new List<CitySuggestion>());

		await _sut.Handle(new SearchCitiesQuery("Hamburg", "en"), CancellationToken.None);
		await _sut.Handle(new SearchCitiesQuery("Hamburg", "en"), CancellationToken.None);

		await _geocodingService.Received(2).SearchCitiesAsync("Hamburg", "en", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotShareCache_BetweenDifferentLanguagesForTheSameQuery()
	{
		var englishResult = new List<CitySuggestion> { new("Munich", 48.1372, 11.5755) };
		var germanResult = new List<CitySuggestion> { new("Munchen", 48.1372, 11.5755) };
		_geocodingService
			.SearchCitiesAsync("Munich", "en", Arg.Any<CancellationToken>())
			.Returns(englishResult);
		_geocodingService
			.SearchCitiesAsync("Munich", "de", Arg.Any<CancellationToken>())
			.Returns(germanResult);

		var english = await _sut.Handle(new SearchCitiesQuery("Munich", "en"), CancellationToken.None);
		var german = await _sut.Handle(new SearchCitiesQuery("Munich", "de"), CancellationToken.None);

		english.Should().BeEquivalentTo(englishResult);
		german.Should().BeEquivalentTo(germanResult);
		await _geocodingService.Received(1).SearchCitiesAsync("Munich", "en", Arg.Any<CancellationToken>());
		await _geocodingService.Received(1).SearchCitiesAsync("Munich", "de", Arg.Any<CancellationToken>());
	}

	public void Dispose() => _cache.Dispose();
}
