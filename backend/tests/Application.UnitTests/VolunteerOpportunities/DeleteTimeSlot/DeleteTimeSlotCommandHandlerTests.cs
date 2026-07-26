using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.DeleteTimeSlot.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.DeleteTimeSlot;

public class DeleteTimeSlotCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly DeleteTimeSlotCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public DeleteTimeSlotCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_dbContext
			.CountActiveEngagementsForTimeSlotAsync(Arg.Any<TimeSlotId>(), Arg.Any<CancellationToken>())
			.Returns(0);
		_sut = new DeleteTimeSlotCommandHandler(_dbContext);
	}

	private VolunteerOpportunity CreateOpportunityWithTimeSlot(out TimeSlot timeSlot)
	{
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", false, Address.Create("Hauptstrasse", "1", "12345", "Berlin").Value,
			Occurrence.Recurring, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		timeSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(7).AddHours(2), 10, DateTimeOffset.UtcNow).Value;
		return opportunity;
	}

	[Test]
	public async Task Handle_ShouldRemoveTimeSlot_WhenNoActiveEngagements(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityWithTimeSlot(out var timeSlot);
		var opportunityId = opportunity.Id.Value;
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new DeleteTimeSlotCommand(opportunityId, timeSlot.Id.Value, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.TimeSlots.Should().BeEmpty();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller belongs to a different organization than the opportunity's.
		var opportunity = CreateOpportunityWithTimeSlot(out var timeSlot);
		var opportunityId = opportunity.Id.Value;
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new DeleteTimeSlotCommand(opportunityId, timeSlot.Id.Value, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		opportunity.TimeSlots.Should().ContainSingle();
	}
}
