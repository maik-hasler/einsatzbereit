using Application.Engagements;
using Application.Engagements.GetEngagementCalendarInfo.v1;
using AwesomeAssertions;
using Domain.Engagements;
using NSubstitute;

namespace Application.UnitTests.Engagements.GetEngagementCalendarInfo;

public class GetEngagementCalendarInfoQueryHandlerTests
{
	private readonly IEngagementReadRepository _readRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly GetEngagementCalendarInfoQueryHandler _sut;

	public GetEngagementCalendarInfoQueryHandlerTests()
	{
		_sut = new GetEngagementCalendarInfoQueryHandler(_readRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnCalendarInfo_WhenReadRepositoryFindsIt(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		var info = new EngagementCalendarInfo(
			engagementId,
			Guid.CreateVersion7(),
			"Test Opportunity",
			"Description",
			"Main St 1, 12345 Berlin",
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2));
		_readRepository
			.GetCalendarInfoAsync(new EngagementId(engagementId), cancellationToken)
			.Returns(info);

		var query = new GetEngagementCalendarInfoQuery(engagementId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().Be(info);
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenReadRepositoryFindsNothing(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = Guid.CreateVersion7();
		_readRepository
			.GetCalendarInfoAsync(new EngagementId(engagementId), cancellationToken)
			.Returns((EngagementCalendarInfo?)null);

		var query = new GetEngagementCalendarInfoQuery(engagementId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeNull();
	}
}
