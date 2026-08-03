using Application.Common.Persistence;
using Application.Users.GetSearchAlert.v1;
using AwesomeAssertions;
using Domain.SearchAlerts;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Users.GetSearchAlert;

public class GetSearchAlertQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly GetSearchAlertQueryHandler _sut;

	private static readonly UserId TestUserId = UserId.New();

	public GetSearchAlertQueryHandlerTests()
	{
		_sut = new GetSearchAlertQueryHandler(_dbContext);
	}

	[Test]
	public async Task Handle_ShouldReturnHasActiveAlertFalse_WhenNoneExists(
		CancellationToken cancellationToken)
	{
		_dbContext
			.GetSearchAlertForUserAsync(TestUserId, cancellationToken)
			.Returns((SearchAlert?)null);

		var result = await _sut.Handle(new GetSearchAlertQuery(TestUserId), cancellationToken);

		result.HasActiveAlert.Should().BeFalse();
		result.Categories.Should().BeEmpty();
	}

	[Test]
	public async Task Handle_ShouldReturnCriteria_WhenAlertExists(
		CancellationToken cancellationToken)
	{
		var alert = SearchAlert.Create(
			TestUserId, Occurrence.Recurring, ParticipationType.ScheduledSlots, false, 52.5, 13.4, 25, ["Environment"], "cleanup");
		_dbContext
			.GetSearchAlertForUserAsync(TestUserId, cancellationToken)
			.Returns(alert);

		var result = await _sut.Handle(new GetSearchAlertQuery(TestUserId), cancellationToken);

		result.HasActiveAlert.Should().BeTrue();
		result.Occurrence.Should().Be("Recurring");
		result.ParticipationType.Should().Be("ScheduledSlots");
		result.IsRemote.Should().BeFalse();
		result.CenterLatitude.Should().Be(52.5);
		result.CenterLongitude.Should().Be(13.4);
		result.RadiusKm.Should().Be(25);
		result.Categories.Should().BeEquivalentTo(["Environment"]);
		result.Tag.Should().Be("cleanup");
	}
}
