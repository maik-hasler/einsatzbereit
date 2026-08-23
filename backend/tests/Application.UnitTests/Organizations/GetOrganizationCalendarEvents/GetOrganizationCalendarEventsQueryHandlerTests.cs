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
	private static readonly DateTimeOffset DefaultFrom = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset DefaultTo = new(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);

	public GetOrganizationCalendarEventsQueryHandlerTests()
	{
		_dbContext
			.IsMemberAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOrganizationCalendarEventsQueryHandler(_dbContext, _readRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnCalendarEvents_WhenOrganizer(
		CancellationToken cancellationToken)
	{
		var events = new List<OrganizationCalendarEventDto>
		{
			new(Guid.NewGuid(), "Title", null, "#ff0000", []),
		};
		_readRepository.GetCalendarEventsAsync(DefaultOrgId, DefaultFrom, DefaultTo, cancellationToken).Returns(events);

		var query = new GetOrganizationCalendarEventsQuery(DefaultOrgId, DefaultRequestingUserId, DefaultFrom, DefaultTo);

		var result = await _sut.Handle(query, cancellationToken);

		result.Should().BeEquivalentTo(events);
	}

	[Test]
	public async Task Handle_ShouldPassThroughFromAndTo_Unchanged(
		CancellationToken cancellationToken)
	{
		var capturedFrom = DateTimeOffset.MinValue;
		var capturedTo = DateTimeOffset.MinValue;
		_readRepository
			.GetCalendarEventsAsync(
				Arg.Any<Guid>(),
				Arg.Do<DateTimeOffset>(f => capturedFrom = f),
				Arg.Do<DateTimeOffset>(t => capturedTo = t),
				Arg.Any<CancellationToken>())
			.Returns([]);

		var query = new GetOrganizationCalendarEventsQuery(DefaultOrgId, DefaultRequestingUserId, DefaultFrom, DefaultTo);

		await _sut.Handle(query, cancellationToken);

		capturedFrom.Should().Be(DefaultFrom);
		capturedTo.Should().Be(DefaultTo);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotAMember(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsMemberAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetOrganizationCalendarEventsQuery(DefaultOrgId, DefaultRequestingUserId, DefaultFrom, DefaultTo);

		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _readRepository.DidNotReceive().GetCalendarEventsAsync(
			Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
	}
}
