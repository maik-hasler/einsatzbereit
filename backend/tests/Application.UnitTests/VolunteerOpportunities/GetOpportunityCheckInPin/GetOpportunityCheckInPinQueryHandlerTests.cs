using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.GetOpportunityCheckInPin.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.GetOpportunityCheckInPin;

public class GetOpportunityCheckInPinQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly GetOpportunityCheckInPinQueryHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOpportunityCheckInPinQueryHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOpportunityCheckInPinQueryHandler(_dbContext, _unitOfWork, _pinGenerator);
	}

	private VolunteerOpportunity CreateOpportunityWithPin() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.PINCode, _pinGenerator,
			status: OpportunityStatus.Draft, checkInPin: "482134").Value;

	[Test]
	public async Task Handle_ShouldReturnCheckInPin_WhenOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityWithPin();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var query = new GetOpportunityCheckInPinQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().Be("482134");
	}

	[Test]
	public async Task Handle_ShouldRotatePinAndPersist_WhenCurrentOccurrenceHasMovedOn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityWithPin();
		var now = DateTimeOffset.UtcNow;
		opportunity.AddTimeSlot(now.AddDays(-10), now.AddDays(-10).AddHours(2), 10, now.AddDays(-11));
		opportunity.EnsureCurrentCheckInPin(now.AddDays(-10), _pinGenerator);
		opportunity.AddTimeSlot(now.AddDays(1), now.AddDays(1).AddHours(2), 10, now);
		_pinGenerator.GeneratePin().Returns("111222");
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var query = new GetOpportunityCheckInPinQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(query, cancellationToken);

		// Assert
		result.Should().Be("111222");
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotPersist_WhenPinDidNotNeedToRotate(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityWithPin();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		var query = new GetOpportunityCheckInPinQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		await _sut.Handle(query, cancellationToken);

		// Assert
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange

		var opportunity = CreateOpportunityWithPin();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetOpportunityCheckInPinQuery(opportunity.Id, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
	}
}
