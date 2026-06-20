using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.CreateTimeSlot.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.CreateTimeSlot;

public class CreateTimeSlotCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IKeycloakOrganizationService _keycloakOrgService = Substitute.For<IKeycloakOrganizationService>();
	private readonly CreateTimeSlotCommandHandler _sut;

	private static readonly OrganizationId DefaultOrgId = new(Guid.CreateVersion7());
	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());
	private static readonly Address DefaultAddress = new("Hauptstrasse", "1", "12345", "Berlin");
	private static readonly DateTimeOffset BaseStart = DateTimeOffset.UtcNow.AddDays(7);
	private static readonly DateTimeOffset BaseEnd = DateTimeOffset.UtcNow.AddDays(7).AddHours(2);

	public CreateTimeSlotCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_keycloakOrgService
			.GetUserOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(DefaultOrgId.Value, "Test Org")]);
		_sut = new CreateTimeSlotCommandHandler(_dbContext, _keycloakOrgService);
	}

	private static VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress,
			Occurrence.Recurring, ParticipationType.Waitlist, CheckInMethod.None);

	[Test]
	public async Task Handle_ShouldCreateSingleSlot_WhenNoRecurrence(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(1);
		result[0].StartDateTime.Should().Be(BaseStart);
		result[0].EndDateTime.Should().Be(BaseEnd);
	}

	[Test]
	public async Task Handle_ShouldCreate8WeeklySlots_WithWeeklyFrequency(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 5, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 8);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(8);
		for (var i = 0; i < 8; i++)
			result[i].StartDateTime.Should().Be(BaseStart.AddDays(7 * i));
	}

	[Test]
	public async Task Handle_ShouldCreate3MonthlySlots_WithMonthlyFrequency(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 20, DefaultRequestingUserId,
			RecurrenceFrequency: "Monthly", RecurrenceCount: 3);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(3);
		result[0].StartDateTime.Should().Be(BaseStart);
		result[1].StartDateTime.Should().Be(BaseStart.AddMonths(1));
		result[2].StartDateTime.Should().Be(BaseStart.AddMonths(2));
	}

	[Test]
	public async Task Handle_ShouldClampCountTo52_WhenCountExceeds52(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 5, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 100);

		var result = await _sut.Handle(command, cancellationToken);

		result.Should().HaveCount(52);
	}

	[Test]
	public async Task Handle_ShouldPreserveDuration_ForAllRecurringSlots(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateOpportunity();
		var opportunityId = Guid.CreateVersion7();
		var expectedDuration = BaseEnd - BaseStart;
		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		var command = new CreateTimeSlotCommand(
			opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId,
			RecurrenceFrequency: "Weekly", RecurrenceCount: 4);

		var result = await _sut.Handle(command, cancellationToken);

		foreach (var slot in result)
			(slot.EndDateTime - slot.StartDateTime).Should().Be(expectedDuration);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		Func<Task> act = async () => await _sut.Handle(
			new CreateTimeSlotCommand(opportunityId, BaseStart, BaseEnd, 10, DefaultRequestingUserId),
			cancellationToken);

		await act.Should().ThrowAsync<DomainException>().WithMessage($"*{opportunityId}*");
	}
}
