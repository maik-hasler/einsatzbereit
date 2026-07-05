using Application.Common.Persistence;
using Application.Engagements.GetEngagementCalendarInfo.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.GetEngagementCalendarInfo;

public class GetEngagementCalendarInfoQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly GetEngagementCalendarInfoQueryHandler _sut;

	private static readonly Address TestAddress = new("Main St", "1", "12345", "Berlin");

	public GetEngagementCalendarInfoQueryHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_sut = new GetEngagementCalendarInfoQueryHandler(_dbContext);
	}

	private static VolunteerOpportunity CreateOpportunity(bool isRemote) =>
		VolunteerOpportunity.Create(
			new OrganizationId(Guid.NewGuid()),
			"Test Opportunity",
			"Description",
			isRemote,
			isRemote ? null : TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			status: OpportunityStatus.Draft);

	[Test]
	public async Task Handle_ShouldReturnCalendarInfo_WhenEngagementHasATimeSlot(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity(isRemote: false);
		var timeSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
			10);
		var engagement = Engagement.CreateWaitlistSignUp(
			opportunity.Id,
			new UserId(Guid.CreateVersion7()),
			timeSlot.Id);
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var query = new GetEngagementCalendarInfoQuery(engagementId.Value);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.OpportunityTitle.Should().Be("Test Opportunity");
		result.Location.Should().Be("Main St 1, 12345 Berlin");
		result.StartDateTime.Should().Be(timeSlot.StartDateTime);
		result.EndDateTime.Should().Be(timeSlot.EndDateTime);
	}

	[Test]
	public async Task Handle_ShouldReturnRemoteLocation_WhenOpportunityIsRemote(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity(isRemote: true);
		var timeSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1),
			DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
			10);
		var engagement = Engagement.CreateWaitlistSignUp(
			opportunity.Id,
			new UserId(Guid.CreateVersion7()),
			timeSlot.Id);
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var query = new GetEngagementCalendarInfoQuery(engagementId.Value);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().NotBeNull();
		result!.Location.Should().Be("Remote");
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var query = new GetEngagementCalendarInfoQuery(engagementId.Value);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenEngagementHasNoTimeSlot(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity(isRemote: false);
		var engagement = Engagement.CreateIndividualContact(
			opportunity.Id,
			new UserId(Guid.CreateVersion7()),
			"I'd like to help.");
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var query = new GetEngagementCalendarInfoQuery(engagementId.Value);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().BeNull();
	}
}
