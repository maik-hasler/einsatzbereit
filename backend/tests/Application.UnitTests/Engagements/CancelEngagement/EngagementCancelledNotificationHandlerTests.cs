using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.CancelEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.Engagements.CancelEngagement;

public class EngagementCancelledNotificationHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly EngagementCancelledNotificationHandler _sut;

	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public EngagementCancelledNotificationHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
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
		_sut = new EngagementCancelledNotificationHandler(
			_dbContext, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder, NullLogger<EngagementCancelledNotificationHandler>.Instance);
	}

	private VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldEmailVolunteer_WhenEngagementCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		_keycloakUserService
			.GetUserAsync(volunteerId.Value, Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(volunteerId.Value, "vera", "Vera", null, "vera@example.com"));
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), volunteerId, VolunteerOpportunityId.New(), "No longer needed.");

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"vera@example.com",
			"Test Subject",
			Arg.Is<string>(body => body!.StartsWith("Test Body")),
			Arg.Any<string>(),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldRenderCancellationEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var volunteer = User.Create(volunteerId);
		volunteer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([volunteer]);
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), volunteerId, VolunteerOpportunityId.New(), null);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementCancelled,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldEmailVolunteer_WhenSubscribedToEngagementCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		_unsubscribeLinkBuilder.Build(Arg.Any<UserId>(), Arg.Any<Guid>(), Arg.Any<EmailNotificationType>())
			.Returns("https://example.com/unsubscribe");
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), null);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"user@example.com",
			Arg.Any<string>(),
			Arg.Is<string>(body => body!.Contains("https://example.com/unsubscribe")),
			Arg.Any<string>(),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldRenderReasonSuffix_WhenReasonIsGiven(
		CancellationToken cancellationToken)
	{
		// Arrange
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), "Not enough sign-ups");

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementCancelledReasonSuffix,
			Arg.Any<string>(),
			Arg.Is<IReadOnlyDictionary<string, string>>(p => p!["Reason"] == "Not enough sign-ups"));
	}

	[Test]
	public async Task Handle_ShouldNotRenderReasonSuffix_WhenNoReasonIsGiven(
		CancellationToken cancellationToken)
	{
		// Arrange
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), null);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		_emailTemplateRenderer.DidNotReceive().Render(
			EmailTemplateKind.EngagementCancelledReasonSuffix,
			Arg.Any<string>(),
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldNotEmailVolunteer_WhenOptedOutOfEngagementCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var volunteerId = UserId.New();
		var optedOutVolunteer = User.Create(volunteerId);
		optedOutVolunteer.UpdateNotificationPreferences(
			notifyOnNewSignUp: true,
			notifyOnWithdrawal: true,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: false,
			notifyOnEngagementReminder: true);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([optedOutVolunteer]);
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), volunteerId, VolunteerOpportunityId.New(), null);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldUseOpportunityTitleFromEvent_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// The cascade from deleting/shadow-deleting an opportunity cancels its
		// engagements in the same transaction, so the opportunity row is already
		// gone (or filtered out) by the time this dispatches post-commit (#1150).
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), null, OpportunityTitle: "Deleted Opportunity");

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementCancelled,
			Arg.Any<string>(),
			Arg.Is<IReadOnlyDictionary<string, string>>(p => p!["OpportunityTitle"] == "Deleted Opportunity"));
	}

	[Test]
	public async Task Handle_ShouldSkipSendingEmail_WhenOpportunityGoneAndEventHasNoTitle(
		CancellationToken cancellationToken)
	{
		// Arrange
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var notification = new EngagementCancelledDomainEvent(
			EngagementId.New(), UserId.New(), VolunteerOpportunityId.New(), null);

		// Act
		await _sut.Handle(notification, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
