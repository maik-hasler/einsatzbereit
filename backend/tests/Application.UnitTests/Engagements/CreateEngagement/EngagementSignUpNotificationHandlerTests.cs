using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.CreateEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
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
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly EngagementSignUpNotificationHandler _sut;

	private static readonly Address TestAddress = Address.Create("Main St", "1", "12345", "Berlin").Value;

	public EngagementSignUpNotificationHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
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
			_dbContext, _unitOfWork, _keycloakService, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder,
			NullLogger<EngagementSignUpNotificationHandler>.Instance);
	}

	private VolunteerOpportunity CreateTestOpportunity(VolunteerOpportunityId id) =>
		VolunteerOpportunity.Create(
			OrganizationId.New(),
			"Test Opportunity",
			"Description",
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			_pinGenerator,
			status: OpportunityStatus.Draft).Value;

	private (VolunteerOpportunity opportunity, Engagement engagement) SetupSlotSignUp(UserId volunteerId)
	{
		var opportunity = CreateTestOpportunity(VolunteerOpportunityId.New());
		var timeSlot = opportunity.AddTimeSlot(
			DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddHours(2), 10, DateTimeOffset.UtcNow).Value;
		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, timeSlot.Id);
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		_engagementRepo.FindAsync(engagement.Id, Arg.Any<CancellationToken>()).Returns(engagement);
		return (opportunity, engagement);
	}

	private (VolunteerOpportunity opportunity, Engagement engagement) SetupIndividualContact(UserId volunteerId)
	{
		var opportunity = CreateTestOpportunity(VolunteerOpportunityId.New());
		var engagement = Engagement.CreateIndividualContact(opportunity.Id, volunteerId, "Hi!").GetValueOrThrow();
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		_engagementRepo.FindAsync(engagement.Id, Arg.Any<CancellationToken>()).Returns(engagement);
		return (opportunity, engagement);
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>()).Returns((VolunteerOpportunity?)null);
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunityId);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenEngagementNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateTestOpportunity(VolunteerOpportunityId.New());
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, Arg.Any<CancellationToken>()).Returns((Engagement?)null);
		var domainEvent = new EngagementCreatedDomainEvent(engagementId, UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRenderRequestReceivedTemplate_ForIndividualContact(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupIndividualContact(volunteerId);
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementRequestReceived,
			Arg.Any<string>(),
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldRenderWaitlistedTemplate_ForSlotSignUp(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupSlotSignUp(volunteerId);
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementWaitlisted,
			Arg.Any<string>(),
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldRenderVolunteerEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupIndividualContact(volunteerId);
		var volunteer = User.Create(volunteerId);
		volunteer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Is<IReadOnlyCollection<UserId>>(ids => ids!.Contains(volunteerId)), Arg.Any<CancellationToken>())
			.Returns([volunteer]);
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

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
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupIndividualContact(volunteerId);
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementRequestReceived,
			"de",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	// --- Organizer email notification preferences (#1055) ---

	[Test]
	public async Task Handle_ShouldEmailOrganizer_WhenSubscribedToNewSignUp(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupSlotSignUp(volunteerId);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(opportunity.OrganizationId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		_unsubscribeLinkBuilder.Build(Arg.Any<UserId>(), Arg.Any<Guid>(), Arg.Any<EmailNotificationType>())
			.Returns("https://example.com/unsubscribe");
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

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
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupSlotSignUp(volunteerId);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(opportunity.OrganizationId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		var organizerUserId = UserId.Create(organizerId).GetValueOrThrow();
		var optedOutOrganizer = User.Create(organizerUserId);
		optedOutOrganizer.UpdateNotificationPreferences(
			notifyOnNewSignUp: false,
			notifyOnWithdrawal: true,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: true,
			notifyOnEngagementReminder: true);
		_dbContext.GetOrCreateUsersAsync(Arg.Is<IReadOnlyCollection<UserId>>(ids => ids!.Contains(organizerUserId)), Arg.Any<CancellationToken>())
			.Returns([optedOutOrganizer]);
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			"olaf@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCreateInAppNotification_ForEachOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupSlotSignUp(volunteerId);
		var organizerId = Guid.NewGuid();
		var organizerUserId = UserId.Create(organizerId).GetValueOrThrow();
		_keycloakService.GetMembersAsync(opportunity.OrganizationId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == organizerUserId
				&& n.Kind == NotificationKind.EngagementCreated
				&& n.RelatedEntityId == engagement.Id.Value),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldSaveChanges_AfterNotifying(
		CancellationToken cancellationToken)
	{
		// Arrange - regression: Publisher.Publish() resolves this handler from
		// its own child scope (a different IApplicationDbContext instance than
		// OutboxProcessorJob's), so nothing else persists the notification
		// writes unless this handler saves them itself.
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupIndividualContact(volunteerId);
		var domainEvent = new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotifyVolunteer_WhenReactivatedEventReceived(
		CancellationToken cancellationToken)
	{
		// Arrange - EngagementReactivatedDomainEvent goes through the same
		// notification path as EngagementCreatedDomainEvent.
		var volunteerId = UserId.New();
		var (opportunity, engagement) = SetupIndividualContact(volunteerId);
		var domainEvent = new EngagementReactivatedDomainEvent(engagement.Id, volunteerId, opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"volunteer@example.com", Arg.Any<string>(), Arg.Any<string>(), cancellationToken);
	}
}
