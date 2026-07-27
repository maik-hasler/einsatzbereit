using Application.Common.Maps;
using Application.Maps.GetMapTile.v1;
using AwesomeAssertions;
using NSubstitute;

namespace Application.UnitTests.Maps.GetMapTile;

public class GetMapTileQueryHandlerTests
{
	private readonly IMapTileService _mapTileService = Substitute.For<IMapTileService>();
	private readonly GetMapTileQueryHandler _sut;

	public GetMapTileQueryHandlerTests()
	{
		_sut = new GetMapTileQueryHandler(_mapTileService);
	}

	[Test]
	public async Task Handle_ShouldReturnTile_WhenMapTileServiceFindsOne()
	{
		var tile = new MapTile([1, 2, 3], "image/png");
		_mapTileService
			.GetTileAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(tile);

		var result = await _sut.Handle(new GetMapTileQuery(14, 8803, 5375), CancellationToken.None);

		result.Should().BeEquivalentTo(tile);
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenMapTileServiceHasNoTile()
	{
		_mapTileService
			.GetTileAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns((MapTile?)null);

		var result = await _sut.Handle(new GetMapTileQuery(99, -1, -1), CancellationToken.None);

		result.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldForwardZoomXY_ToMapTileService()
	{
		_mapTileService
			.GetTileAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns((MapTile?)null);

		await _sut.Handle(new GetMapTileQuery(14, 8803, 5375), CancellationToken.None);

		await _mapTileService.Received(1).GetTileAsync(14, 8803, 5375, Arg.Any<CancellationToken>());
	}
}
