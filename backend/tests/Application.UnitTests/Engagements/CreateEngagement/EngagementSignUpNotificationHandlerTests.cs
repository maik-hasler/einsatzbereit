using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.CreateEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Engagements.CreateEngagement;

public class EngagementSignUpNotificationHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<User, UserId> _userRepo =
		Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly EngagementSignUpNotificationHandler _sut;

	private static readonly Address TestAddress = Address.Create("Main St", "1", "12345", "Berlin").Value;

	public EngagementSignUpNotificationHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.Users.Returns(_userRepo);
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Test", "User", "volunteer@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new EngagementSignUpNotificationHandler(
			_dbContext, _keycloakService, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder,
			NullLogger<EngagementSignUpNotificationHandler>.Instance);
	}

	private VolunteerOpportunity SetupOpportunityExists(bool withTimeSlot, out Engagement engagement, out UserId volunteerId)
	{
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test Opportunity", "Description", false, TestAddress,
			Occurrence.OneTime, withTimeSlot ? ParticipationType.ScheduledSlots : ParticipationType.IndividualContact,
			CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft,
			validUntil: withTimeSlot ? null : DateTimeOffset.UtcNow.AddDays(30)).Value;

		volunteerId = UserId.New();
		if (withTimeSlot)
		{
			var timeSlot = opportunity.AddTimeSlot(
				DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 10, DateTimeOffset.UtcNow).Value;
			opportunity.Publish();
			engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, timeSlot.Id);
		}
		else
		{
			engagement = Engagement.CreateIndividualContact(opportunity.Id, volunteerId, "Hi!").GetValueOrThrow();
		}

		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		_engagementRepo.FindAsync(engagement.Id, Arg.Any<CancellationToken>()).Returns(engagement);
		return opportunity;
	}

	// --- Localized emails (#1052) ---

	[Test]
	public async Task Handle_ShouldRenderVolunteerEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		SetupOpportunityExists(withTimeSlot: false, out var engagement, out var volunteerId);
		var volunteer = User.Create(volunteerId);
		volunteer.SetPreferredLanguage("en");
		_userRepo.FindAsync(volunteerId, Arg.Any<CancellationToken>()).Returns(volunteer);
		var notification = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, engagement.OpportunityId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementRequestReceived,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldDefaultVolunteerEmailToGerman_WhenNoProfileExistsYet(
		CancellationToken cancellationToken)
	{
		// Arrange - a volunteer who signs up without ever having loaded their
		// profile page has no User row yet, so PreferredLanguage can't have
		// been seeded; the recipient's language must still resolve, never NRE.
		SetupOpportunityExists(withTimeSlot: false, out var engagement, out var volunteerId);
		_userRepo.FindAsync(volunteerId, Arg.Any<CancellationToken>()).Returns((User?)null);
		var notification = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, engagement.OpportunityId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementRequestReceived,
			"de",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldUseWaitlistedTemplate_ForAScheduledSlotsSignUp(
		CancellationToken cancellationToken)
	{
		// Arrange
		SetupOpportunityExists(withTimeSlot: true, out var engagement, out var volunteerId);
		var notification = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, engagement.OpportunityId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementWaitlisted,
			Arg.Any<string>(),
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	// --- Organizer email notification preferences (#1055) ---

	[Test]
	public async Task Handle_ShouldEmailOrganizer_WhenSubscribedToNewSignUp(
		CancellationToken cancellationToken)
	{
		// Arrange
		SetupOpportunityExists(withTimeSlot: true, out var engagement, out var volunteerId);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		_unsubscribeLinkBuilder.Build(Arg.Any<UserId>(), Arg.Any<Guid>(), Arg.Any<EmailNotificationType>())
			.Returns("https://example.com/unsubscribe");
		var notification = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, engagement.OpportunityId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"olaf@example.com",
			Arg.Any<string>(),
			Arg.Is<string>(body => body!.Contains("https://example.com/unsubscribe")),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotEmailOrganizer_WhenOptedOutOfNewSignUp(
		CancellationToken cancellationToken)
	{
		// Arrange
		SetupOpportunityExists(withTimeSlot: true, out var engagement, out var volunteerId);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		var organizerUserId = UserId.Create(organizerId).GetValueOrThrow();
		var optedOutOrganizer = User.Create(organizerUserId);
		optedOutOrganizer.UpdateNotificationPreferences(
			notifyOnNewSignUp: false,
			notifyOnWithdrawal: true,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: true,
			notifyOnEngagementReminder: true);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([optedOutOrganizer]);
		var notification = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, engagement.OpportunityId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			"olaf@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSendTheSameNotifications_ForAReactivatedEngagement(
		CancellationToken cancellationToken)
	{
		// A reactivation (re-signing-up after a withdrawal/cancellation) reads
		// exactly like a fresh sign-up to the volunteer and organizers (#1150).
		SetupOpportunityExists(withTimeSlot: true, out var engagement, out var volunteerId);
		var notification = new EngagementReactivatedDomainEvent(engagement.Id, volunteerId, engagement.OpportunityId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"volunteer@example.com", Arg.Any<string>(), Arg.Any<string>(), cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var notification = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
