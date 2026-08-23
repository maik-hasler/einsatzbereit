using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.CreateEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Engagements.CreateEngagement;

public class EngagementCreatedDomainEventHandlerTests
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
	private readonly IAggregateRepository<User, UserId> _userRepo = Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly EngagementCreatedDomainEventHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;

	public EngagementCreatedDomainEventHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Users.Returns(_userRepo);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Vera", "Volunteer", "vera@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_emailTemplateRenderer
			.Render(EmailTemplateKind.EmailFooter, Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(call => new EmailContent(
				string.Empty,
				$"\n\n---\n{((IReadOnlyDictionary<string, string>)call[2]!)["UnsubscribeUrl"]}"));
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new EngagementCreatedDomainEventHandler(
			_dbContext, _unitOfWork, _keycloakService, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder,
			NullLogger<EngagementCreatedDomainEventHandler>.Instance);
	}

	private static VolunteerOpportunity CreateOpportunity(OrganizationId organizationId) =>
		VolunteerOpportunity.Create(
			organizationId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Published,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).Value;

	[Test]
	public async Task Handle_ShouldEmailOrganizer_WhenSubscribedToNewSignUp(
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

		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, IsSlotSignUp: false);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert

		await _emailService.Received(1).SendBatchAsync(
			Arg.Is<IReadOnlyList<EmailMessage>>(messages => messages!.Any(m =>
				m.To == "olaf@example.com" && m.Body.Contains("https://example.com/unsubscribe"))),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotEmailOrganizer_WhenOptedOutOfNewSignUp(
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
			notifyOnNewSignUp: false,
			notifyOnWithdrawal: true,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: true,
			notifyOnEngagementReminder: true);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([optedOutOrganizer]);

		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, IsSlotSignUp: false);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendBatchAsync(
			Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSkip_WhenVolunteersKeycloakAccountIsAlreadyDeleted(
		CancellationToken cancellationToken)
	{
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns<KeycloakUserProfile>(_ => throw new InvalidOperationException("404 Not Found"));
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, IsSlotSignUp: false);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		await _emailService.DidNotReceive().SendBatchAsync(
			Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotThrow_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunityId, IsSlotSignUp: false);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		await _emailService.DidNotReceive().SendBatchAsync(
			Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSaveChanges_AfterNotifying(
		CancellationToken cancellationToken)
	{
		// Arrange

		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, IsSlotSignUp: false);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldEmailVolunteer_WithTheirOwnSignUpReceipt(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, IsSlotSignUp: false);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"vera@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), cancellationToken);
	}

	[Test]
	[Arguments(true, EmailTemplateKind.EngagementWaitlisted)]
	[Arguments(false, EmailTemplateKind.EngagementRequestReceived)]
	public async Task Handle_ShouldPickVolunteerEmailTemplate_MatchingIsSlotSignUp(
		bool isSlotSignUp, EmailTemplateKind expectedTemplate, CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, isSlotSignUp);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			expectedTemplate, Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldRenderVolunteerEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var volunteerId = UserId.New();
		var volunteer = User.Create(volunteerId);
		volunteer.SetPreferredLanguage("en");
		_userRepo.FindAsync(volunteerId, Arg.Any<CancellationToken>()).Returns(volunteer);
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), volunteerId, opportunity.Id, IsSlotSignUp: false);

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
		// Arrange

		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var volunteerId = UserId.New();
		_userRepo.FindAsync(volunteerId, Arg.Any<CancellationToken>()).Returns((User?)null);
		var domainEvent = new EngagementCreatedDomainEvent(EngagementId.New(), volunteerId, opportunity.Id, IsSlotSignUp: false);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementRequestReceived,
			"de",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}
}
