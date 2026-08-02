using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.WithdrawEngagement.v1;
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

namespace Application.UnitTests.Engagements.WithdrawEngagement;

public class EngagementWithdrawnNotificationHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly EngagementWithdrawnNotificationHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public EngagementWithdrawnNotificationHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Test", "User", "volunteer@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new EngagementWithdrawnNotificationHandler(
			_dbContext, _unitOfWork, _keycloakService, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder,
			NullLogger<EngagementWithdrawnNotificationHandler>.Instance);
	}

	private VolunteerOpportunity CreateOpportunityForOrganizerNotification(out Guid organizerUserId)
	{
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test", "Test", false, DefaultAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None,
			_pinGenerator, status: OpportunityStatus.Draft).Value;
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);

		organizerUserId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(opportunity.OrganizationId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerUserId, "organizer", "Org", "Anizer", "organizer@example.com", true)]);
		return opportunity;
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>()).Returns((VolunteerOpportunity?)null);
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunityId);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRenderOrganizerEmail_InOrganizersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityForOrganizerNotification(out var organizerUserId);
		var organizerId = UserId.Create(organizerUserId).GetValueOrThrow();
		var organizer = User.Create(organizerId);
		organizer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([organizer]);
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert - the organizer's own language, not the withdrawing volunteer's,
		// governs this email since the organizer is the recipient.
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementWithdrawnNotifyOrganizer,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldCreateInAppNotification_ForEachOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityForOrganizerNotification(out var organizerUserId);
		var organizerId = UserId.Create(organizerUserId).GetValueOrThrow();
		var engagementId = EngagementId.New();
		var domainEvent = new EngagementWithdrawnDomainEvent(engagementId, UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == organizerId
				&& n.Kind == NotificationKind.EngagementWithdrawn
				&& n.RelatedEntityId == engagementId.Value),
			cancellationToken);
	}

	// --- Organizer email notification preferences (#1055) ---

	[Test]
	public async Task Handle_ShouldEmailOrganizer_WhenSubscribedToWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityForOrganizerNotification(out _);
		_unsubscribeLinkBuilder.Build(Arg.Any<UserId>(), Arg.Any<Guid>(), Arg.Any<EmailNotificationType>())
			.Returns("https://example.com/unsubscribe");
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"organizer@example.com",
			Arg.Any<string>(),
			Arg.Is<string>(body => body!.Contains("https://example.com/unsubscribe")),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotEmailOrganizer_WhenOptedOutOfWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunityForOrganizerNotification(out var organizerUserId);
		var optedOutOrganizer = User.Create(UserId.Create(organizerUserId).GetValueOrThrow());
		optedOutOrganizer.UpdateNotificationPreferences(
			notifyOnNewSignUp: true,
			notifyOnWithdrawal: false,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: true,
			notifyOnEngagementReminder: true);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([optedOutOrganizer]);
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			"organizer@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSaveChanges_AfterNotifying(
		CancellationToken cancellationToken)
	{
		// Arrange - regression: Publisher.Publish() resolves this handler from
		// its own child scope (a different IApplicationDbContext instance than
		// OutboxProcessorJob's), so nothing else persists the notification
		// writes unless this handler saves them itself.
		var opportunity = CreateOpportunityForOrganizerNotification(out _);
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}
}
