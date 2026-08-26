using Application.Common.Geocoding;
using Application.Maps.GeocodeAddress.v1;
using AwesomeAssertions;
using Domain.Common;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace Application.UnitTests.Maps.GeocodeAddress;

public sealed class GeocodeAddressQueryHandlerTests : IDisposable
{
	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;

	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 100 });
	private readonly GeocodeAddressQueryHandler _sut;

	public GeocodeAddressQueryHandlerTests()
	{
		_sut = new GeocodeAddressQueryHandler(_geocodingService, _cache);
	}

	[Test]
	public async Task Handle_ShouldReturnFoundWithCoordinates_WhenGeocodingFindsMatch(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync("Hauptstraße", "1", "12345", "Berlin", Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.Found(new GeoCoordinates(52.52, 13.405)));

		// Act
		var result = await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);

		// Assert
		result.Outcome.Should().Be(GeocodingOutcome.Found);
		result.Coordinates.Should().Be(new GeoCoordinates(52.52, 13.405));
	}

	[Test]
	public async Task Handle_ShouldReturnNotFound_WhenGeocodingConfirmsNoMatch(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.NotFound);

		// Act
		var result = await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);

		// Assert
		result.Outcome.Should().Be(GeocodingOutcome.NotFound);
	}

	[Test]
	public async Task Handle_ShouldReturnTransientFailure_WhenGeocodingProviderIsUnavailable(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.TransientFailure);

		// Act
		var result = await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);

		// Assert
		result.Outcome.Should().Be(GeocodingOutcome.TransientFailure);
	}

	[Test]
	public async Task Handle_ShouldNotCallGeocodingServiceTwice_ForRepeatedIdenticalAddress(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync("Hauptstraße", "1", "12345", "Berlin", Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.Found(new GeoCoordinates(52.52, 13.405)));

		// Act
		await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);
		await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);

		// Assert
		await _geocodingService.Received(1)
			.GeocodeAsync("Hauptstraße", "1", "12345", "Berlin", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotCacheTransientFailure_SoARetryCanStillSucceed(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync("Hauptstraße", "1", "12345", "Berlin", Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.TransientFailure);

		// Act
		await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);
		await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);

		// Assert
		await _geocodingService.Received(2)
			.GeocodeAsync("Hauptstraße", "1", "12345", "Berlin", Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotShareCache_BetweenDifferentAddresses(
		CancellationToken cancellationToken)
	{
		// Arrange
		var otherAddress = Address.Create("Nebenstraße", "2", "54321", "Hamburg").Value;
		_geocodingService
			.GeocodeAsync("Hauptstraße", "1", "12345", "Berlin", Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.Found(new GeoCoordinates(52.52, 13.405)));
		_geocodingService
			.GeocodeAsync("Nebenstraße", "2", "54321", "Hamburg", Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.NotFound);

		// Act
		var first = await _sut.Handle(new GeocodeAddressQuery(DefaultAddress), cancellationToken);
		var second = await _sut.Handle(new GeocodeAddressQuery(otherAddress), cancellationToken);

		// Assert
		first.Outcome.Should().Be(GeocodingOutcome.Found);
		second.Outcome.Should().Be(GeocodingOutcome.NotFound);
	}

	public void Dispose() => _cache.Dispose();
}
