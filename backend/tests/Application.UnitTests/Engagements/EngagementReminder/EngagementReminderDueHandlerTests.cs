using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.EngagementReminder.v1;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Engagements.EngagementReminder;

public class EngagementReminderDueHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly EngagementReminderDueHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();

	public EngagementReminderDueHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new EngagementReminderDueHandler(
			_dbContext, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder, NullLogger<EngagementReminderDueHandler>.Instance);
	}

	private VolunteerOpportunity CreateOpportunityWithTimeSlot(out TimeSlotId timeSlotId)
	{
		// ScheduledSlots opportunities can't be created directly as Published (they must have
		// at least one time slot first - see VolunteerOpportunity.Create) - Draft is fine
		// here since the handler doesn't look at Status at all.
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Beach Cleanup", "Help clean the beach", true, null,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

		var now = DateTimeOffset.UtcNow;
		var timeSlot = opportunity.AddTimeSlot(now.AddHours(24), now.AddHours(26), 10, now).Value;
		timeSlotId = timeSlot.Id;
		return opportunity;
	}

	[Test]
	public async Task Handle_ShouldSendReminderEmail_WhenOpportunityAndTimeSlotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityWithTimeSlot(out var timeSlotId);
		var volunteerId = UserId.New();
		var domainEvent = new EngagementReminderDueDomainEvent(
			EngagementId.New(), volunteerId, opportunity.Id, timeSlotId);

		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_keycloakUserService.GetUserAsync(volunteerId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(volunteerId.Value, "vera", "Vera", "Volunteer", "vera@example.com"));
		_emailService.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), cancellationToken)
			.Returns([true]);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.Received(1).SendBatchAsync(
			Arg.Is<IReadOnlyList<EmailMessage>>(m => m!.Count == 1 && m[0].To == "vera@example.com"),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotThrow_AndShouldNotSendEmail_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		var domainEvent = new EngagementReminderDueDomainEvent(
			EngagementId.New(), UserId.New(), opportunityId, TimeSlotId.New());

		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotThrow_AndShouldNotSendEmail_WhenTimeSlotNoLongerExistsOnOpportunity(
		CancellationToken cancellationToken)
	{
		// Arrange: the time slot the event was queued for was removed from the
		// opportunity between claim and dispatch.
		var opportunity = CreateOpportunityWithTimeSlot(out _);
		var domainEvent = new EngagementReminderDueDomainEvent(
			EngagementId.New(), UserId.New(), opportunity.Id, TimeSlotId.New());

		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEmailSendFails(
		CancellationToken cancellationToken)
	{
		// Arrange: a failed send must propagate so OutboxProcessorJob records the
		// error and retries on its next poll cycle instead of losing the reminder.
		var opportunity = CreateOpportunityWithTimeSlot(out var timeSlotId);
		var volunteerId = UserId.New();
		var domainEvent = new EngagementReminderDueDomainEvent(
			EngagementId.New(), volunteerId, opportunity.Id, timeSlotId);

		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_keycloakUserService.GetUserAsync(volunteerId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(volunteerId.Value, "vera", "Vera", "Volunteer", "vera@example.com"));
		_emailService.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), cancellationToken)
			.Returns([false]);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>();
	}

	[Test]
	public async Task Handle_ShouldRenderReminderEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange - this handler runs from a background job with no HTTP request
		// to read a language header from, so the recipient's persisted
		// preference is the only source of truth here.
		var opportunity = CreateOpportunityWithTimeSlot(out var timeSlotId);
		var volunteerId = UserId.New();
		var domainEvent = new EngagementReminderDueDomainEvent(
			EngagementId.New(), volunteerId, opportunity.Id, timeSlotId);

		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_keycloakUserService.GetUserAsync(volunteerId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(volunteerId.Value, "vera", "Vera", "Volunteer", "vera@example.com"));
		_emailService.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), cancellationToken)
			.Returns([true]);
		var volunteer = User.Create(volunteerId);
		volunteer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([volunteer]);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementReminder,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldFormatStartTime_InEuropeBerlinTimeZone_NotServerLocalTime(
		CancellationToken cancellationToken)
	{
		// Arrange - #1252: the container has no TZ set (server-local == UTC), so
		// formatting via .ToLocalTime() silently announced the wrong hour. A
		// winter instant is used so Europe/Berlin is deterministically UTC+1
		// (CET, no DST) regardless of when this test runs.
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Beach Cleanup", "Help clean the beach", true, null,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		var artificialNow = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var startUtc = new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.Zero);
		var timeSlot = opportunity.AddTimeSlot(startUtc, startUtc.AddHours(2), 10, artificialNow).Value;

		var volunteerId = UserId.New();
		var domainEvent = new EngagementReminderDueDomainEvent(
			EngagementId.New(), volunteerId, opportunity.Id, timeSlot.Id);

		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_keycloakUserService.GetUserAsync(volunteerId.Value, cancellationToken)
			.Returns(new KeycloakUserProfile(volunteerId.Value, "vera", "Vera", "Volunteer", "vera@example.com"));
		_emailService.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), cancellationToken)
			.Returns([true]);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert - 12:00 UTC on a winter date is 13:00 in Europe/Berlin (CET, UTC+1).
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementReminder,
			Arg.Any<string>(),
			Arg.Is<IReadOnlyDictionary<string, string>>(d => d!["StartFormatted"].Contains("13:00")));
	}

	// --- Volunteer email notification preferences (#1055) ---

	[Test]
	public async Task Handle_ShouldNotSendReminderEmail_WhenVolunteerOptedOutOfReminders(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityWithTimeSlot(out var timeSlotId);
		var volunteerId = UserId.New();
		var domainEvent = new EngagementReminderDueDomainEvent(
			EngagementId.New(), volunteerId, opportunity.Id, timeSlotId);

		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var optedOutVolunteer = User.Create(volunteerId);
		optedOutVolunteer.UpdateNotificationPreferences(
			notifyOnNewSignUp: true,
			notifyOnWithdrawal: true,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: true,
			notifyOnEngagementReminder: false);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([optedOutVolunteer]);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>());
	}
}
