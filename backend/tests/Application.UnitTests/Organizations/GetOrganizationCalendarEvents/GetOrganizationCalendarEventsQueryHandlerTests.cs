using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations.GetOrganizationCalendarEvents.v1;
using Application.VolunteerOpportunities;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.GetOrganizationCalendarEvents;

public class GetOrganizationCalendarEventsQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IVolunteerOpportunityReadRepository _readRepository = Substitute.For<IVolunteerOpportunityReadRepository>();
	private readonly GetOrganizationCalendarEventsQueryHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOrganizationCalendarEventsQueryHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOrganizationCalendarEventsQueryHandler(_dbContext, _readRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnCalendarEvents_WhenOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var events = new List<OrganizationCalendarEventDto>
		{
			new(Guid.NewGuid(), "Title", "#ff0000", []),
		};
		_readRepository.GetCalendarEventsAsync(DefaultOrgId, cancellationToken).Returns(events);

		var query = new GetOrganizationCalendarEventsQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeEquivalentTo(events);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller is not an organizer of the target organization.
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetOrganizationCalendarEventsQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _readRepository.DidNotReceive().GetCalendarEventsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}
}
