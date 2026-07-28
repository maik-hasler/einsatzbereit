using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements.CheckInEngagement.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.CheckInEngagement;

public class CheckInEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CheckInEngagementCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public CheckInEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new CheckInEngagementCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", true, null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	private static Engagement CreateConfirmedEngagement(VolunteerOpportunityId opportunityId)
	{
		var engagement = Engagement.CreateWaitlistSignUp(opportunityId, UserId.New(), TimeSlotId.New());
		engagement.Confirm();
		return engagement;
	}

	[Test]
	public async Task Handle_ShouldCheckInEngagement_WhenConfirmed(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var command = new CheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller belongs to a different organization than the opportunity's.
		var opportunity = CreateOpportunity();
		var engagement = CreateConfirmedEngagement(opportunity.Id);
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new CheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		engagement.IsCheckedIn.Should().BeFalse();
	}

	[Test]
	public async Task Handle_ShouldThrowNotFound_WhenOpportunityIsGone(
		CancellationToken cancellationToken)
	{
		// Arrange: opportunity row is gone (e.g. hard-deleted) but its engagement
		// survived as a non-terminal row. The ownership guard must not be silently
		// skipped in this case - it must reject before ever reaching CheckIn.
		var opportunityId = VolunteerOpportunityId.New();
		var engagement = CreateConfirmedEngagement(opportunityId);
		var engagementId = engagement.Id;

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);

		var command = new CheckInEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
		engagement.IsCheckedIn.Should().BeFalse();
		await _dbContext
			.DidNotReceive()
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>());
	}
}
