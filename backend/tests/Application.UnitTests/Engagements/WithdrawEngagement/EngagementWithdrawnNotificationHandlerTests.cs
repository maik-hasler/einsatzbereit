using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.WithdrawEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
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
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IKeycloakOrganizationService _keycloakOrganizationService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly EngagementWithdrawnNotificationHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public EngagementWithdrawnNotificationHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Test", "User", "volunteer@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_keycloakOrganizationService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new EngagementWithdrawnNotificationHandler(
			_dbContext, _keycloakOrganizationService, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder,
			NullLogger<EngagementWithdrawnNotificationHandler>.Instance);
	}

	private VolunteerOpportunity CreateOpportunityForOrganizerNotification(VolunteerOpportunityId opportunityId, out Guid organizerUserId)
	{
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test", "Test", false, DefaultAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None,
			_pinGenerator, status: OpportunityStatus.Draft).Value;
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>()).Returns(opportunity);

		organizerUserId = Guid.NewGuid();
		_keycloakOrganizationService.GetMembersAsync(opportunity.OrganizationId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerUserId, "organizer", "Org", "Anizer", "organizer@example.com", true)]);
		return opportunity;
	}

	[Test]
	public async Task Handle_ShouldRenderOrganizerEmail_InOrganizersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		CreateOpportunityForOrganizerNotification(opportunityId, out var organizerUserId);
		var organizerId = UserId.Create(organizerUserId).GetValueOrThrow();
		var organizer = User.Create(organizerId);
		organizer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([organizer]);
		var notification = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunityId);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert - the organizer's own language, not the withdrawing volunteer's,
		// governs this email since the organizer is the recipient.
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementWithdrawnNotifyOrganizer,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldEmailOrganizer_WhenSubscribedToWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test Opportunity", "Description", false, DefaultAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		var organizerId = Guid.NewGuid();
		_keycloakOrganizationService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		_unsubscribeLinkBuilder.Build(Arg.Any<UserId>(), Arg.Any<Guid>(), Arg.Any<EmailNotificationType>())
			.Returns("https://example.com/unsubscribe");
		var notification = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

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
	public async Task Handle_ShouldNotEmailOrganizer_WhenOptedOutOfWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test Opportunity", "Description", false, DefaultAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		var organizerId = Guid.NewGuid();
		_keycloakOrganizationService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		var optedOutOrganizer = User.Create(UserId.Create(organizerId).GetValueOrThrow());
		optedOutOrganizer.UpdateNotificationPreferences(
			notifyOnNewSignUp: true,
			notifyOnWithdrawal: false,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: true,
			notifyOnEngagementReminder: true);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([optedOutOrganizer]);
		var notification = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			"olaf@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenVolunteersKeycloakAccountIsAlreadyDeleted(
		CancellationToken cancellationToken)
	{
		// DeleteMyAccountCommandHandler withdraws non-terminal engagements and deletes
		// the Keycloak identity in the same commit (#1140/#1141) - both dispatch from
		// the same outbox batch with no ordering guarantee, so this must tolerate the
		// volunteer already being gone rather than dead-lettering forever.
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test Opportunity", "Description", false, DefaultAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns<KeycloakUserProfile>(_ => throw new InvalidOperationException("404 Not Found"));
		var notification = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		Func<Task> act = async () => await _sut.Handle(notification, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var notification = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
