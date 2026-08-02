using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.WithdrawEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Engagements.WithdrawEngagement;

public class EngagementWithdrawnDomainEventHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly EngagementWithdrawnDomainEventHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;

	public EngagementWithdrawnDomainEventHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Vera", "Volunteer", "vera@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new EngagementWithdrawnDomainEventHandler(
			_dbContext, _unitOfWork, _keycloakService, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder,
			NullLogger<EngagementWithdrawnDomainEventHandler>.Instance);
	}

	private static VolunteerOpportunity CreateOpportunity(OrganizationId organizationId) =>
		VolunteerOpportunity.Create(
			organizationId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Published,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).Value;

	[Test]
	public async Task Handle_ShouldEmailOrganizer_WhenSubscribedToWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(organizationId.Value, cancellationToken)
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		_unsubscribeLinkBuilder.Build(Arg.Any<UserId>(), Arg.Any<Guid>(), Arg.Any<EmailNotificationType>())
			.Returns("https://example.com/unsubscribe");

		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"olaf@example.com",
			Arg.Any<string>(),
			Arg.Is<string>(body => body!.Contains("https://example.com/unsubscribe")),
			Arg.Any<string>(),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotEmailOrganizer_WhenOptedOutOfWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(organizationId.Value, cancellationToken)
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

		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			"olaf@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRenderOrganizerEmail_InOrganizersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange - the organizer's own language, not the withdrawing
		// volunteer's, governs this email since the organizer is the recipient.
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(organizationId.Value, cancellationToken)
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		var organizer = User.Create(UserId.Create(organizerId).GetValueOrThrow());
		organizer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([organizer]);

		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementWithdrawnNotifyOrganizer,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenVolunteersKeycloakAccountIsAlreadyDeleted(
		CancellationToken cancellationToken)
	{
		// DeleteMyAccountCommandHandler withdraws non-terminal engagements and raises
		// UserAccountDeletedDomainEvent in the same commit (#1140/#1141) - both dispatch
		// from the same outbox batch with no ordering guarantee, so this must tolerate
		// the volunteer already being gone rather than dead-lettering forever.
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns<KeycloakUserProfile>(_ => throw new InvalidOperationException("404 Not Found"));
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotThrow_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunityId);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSaveChanges_AfterNotifying(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var domainEvent = new EngagementWithdrawnDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}
}
