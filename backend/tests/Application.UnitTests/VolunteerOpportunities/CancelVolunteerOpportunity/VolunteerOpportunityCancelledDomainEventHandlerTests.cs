using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.CancelVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.CancelVolunteerOpportunity;

public class VolunteerOpportunityCancelledDomainEventHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IEngagementReadRepository _engagementReadRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly VolunteerOpportunityCancelledDomainEventHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();

	public VolunteerOpportunityCancelledDomainEventHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(new List<Engagement>());
		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<TimeSlotId?>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new VolunteerOpportunityCancelledDomainEventHandler(
			_dbContext, _unitOfWork, _engagementReadRepository, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder,
			NullLogger<VolunteerOpportunityCancelledDomainEventHandler>.Instance);
	}

	private static VolunteerOpportunity CreatePublishedOpportunity() =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Published,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).Value;

	[Test]
	public async Task Handle_ShouldNotifyActiveVolunteers_WithOpportunityCancelledKind(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreatePublishedOpportunity();
		var volunteerId = Guid.NewGuid();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(opportunity.Id, Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([volunteerId]);

		var domainEvent = new VolunteerOpportunityCancelledDomainEvent(opportunity.Id, DefaultOrgId, "No longer needed");

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.Kind == NotificationKind.OpportunityCancelled && n.RelatedEntityId == opportunity.Id.Value),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCancelActiveEngagements_WithOrganizerReasonIncluded(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreatePublishedOpportunity();
		var timeSlotId = TimeSlotId.New();
		var pendingEngagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), timeSlotId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(opportunity.Id, cancellationToken)
			.Returns([pendingEngagement]);

		var domainEvent = new VolunteerOpportunityCancelledDomainEvent(opportunity.Id, DefaultOrgId, "Venue flooded");

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		pendingEngagement.Status.Should().Be(EngagementStatus.Cancelled);
		pendingEngagement.CancellationReason.Should().Be("Opportunity was cancelled: Venue flooded");
	}

	[Test]
	public async Task Handle_ShouldCancelActiveEngagements_WithDefaultReason_WhenNoOrganizerReasonGiven(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreatePublishedOpportunity();
		var timeSlotId = TimeSlotId.New();
		var pendingEngagement = Engagement.CreateSlotSignUp(opportunity.Id, UserId.New(), timeSlotId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(opportunity.Id, cancellationToken)
			.Returns([pendingEngagement]);

		var domainEvent = new VolunteerOpportunityCancelledDomainEvent(opportunity.Id, DefaultOrgId, null);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		pendingEngagement.CancellationReason.Should().Be("Opportunity was cancelled.");
	}

	[Test]
	public async Task Handle_ShouldNotThrow_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);
		var domainEvent = new VolunteerOpportunityCancelledDomainEvent(opportunityId, DefaultOrgId, "reason");

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _notifRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSaveChanges_AfterCascade(
		CancellationToken cancellationToken)
	{
		// Arrange - regression: Publisher.Publish() resolves this handler from
		// its own child scope (a different IApplicationDbContext instance than
		// OutboxProcessorJob's), so nothing else persists the engagement
		// cancellation/notification writes unless this handler saves them itself.
		var opportunity = CreatePublishedOpportunity();
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var domainEvent = new VolunteerOpportunityCancelledDomainEvent(opportunity.Id, DefaultOrgId, "reason");

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotSaveChanges_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);
		var domainEvent = new VolunteerOpportunityCancelledDomainEvent(opportunityId, DefaultOrgId, "reason");

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}
}
