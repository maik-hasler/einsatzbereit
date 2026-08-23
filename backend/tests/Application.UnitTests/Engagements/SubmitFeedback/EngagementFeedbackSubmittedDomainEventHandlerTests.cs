using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.SubmitFeedback.v1;
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

namespace Application.UnitTests.Engagements.SubmitFeedback;

public class EngagementFeedbackSubmittedDomainEventHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IKeycloakOrganizationService _keycloakService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly EngagementFeedbackSubmittedDomainEventHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;

	public EngagementFeedbackSubmittedDomainEventHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_sut = new EngagementFeedbackSubmittedDomainEventHandler(
			_dbContext, _unitOfWork, _keycloakService, NullLogger<EngagementFeedbackSubmittedDomainEventHandler>.Instance);
	}

	private VolunteerOpportunity CreateOpportunity(OrganizationId organizationId) =>
		VolunteerOpportunity.Create(
			organizationId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact,
			CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Published,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).Value;

	[Test]
	public async Task Handle_ShouldCreateInAppNotification_ForEachOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var organizerId1 = Guid.NewGuid();
		var organizerId2 = Guid.NewGuid();
		_keycloakService.GetMembersAsync(organizationId.Value, cancellationToken)
			.Returns([
				new KeycloakOrganizationMember(organizerId1, "olaf", "Olaf", "Organizer", "olaf@example.com", true),
				new KeycloakOrganizationMember(organizerId2, "petra", "Petra", "Organizer", "petra@example.com", true),
			]);
		var engagementId = EngagementId.New();
		var domainEvent = new EngagementFeedbackSubmittedDomainEvent(engagementId, UserId.New(), opportunity.Id, 5);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == UserId.Create(organizerId1).GetValueOrThrow()
				&& n.Kind == NotificationKind.FeedbackSubmitted
				&& n.RelatedEntityId == engagementId.Value),
			Arg.Any<CancellationToken>());
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == UserId.Create(organizerId2).GetValueOrThrow()
				&& n.Kind == NotificationKind.FeedbackSubmitted
				&& n.RelatedEntityId == engagementId.Value),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotNotifyNonOrganizerMembers(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var memberId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(organizationId.Value, cancellationToken)
			.Returns([new KeycloakOrganizationMember(memberId, "vera", "Vera", "Volunteer", "vera@example.com", false)]);
		var domainEvent = new EngagementFeedbackSubmittedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, 5);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _notifRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSaveChanges_AfterNotifying(
		CancellationToken cancellationToken)
	{
		// Arrange
		var organizationId = OrganizationId.New();
		var opportunity = CreateOpportunity(organizationId);
		_opportunityRepo.FindAsync(opportunity.Id, cancellationToken).Returns(opportunity);
		var domainEvent = new EngagementFeedbackSubmittedDomainEvent(EngagementId.New(), UserId.New(), opportunity.Id, 5);

		// Act
		await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotThrow_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(opportunityId, cancellationToken).Returns((VolunteerOpportunity?)null);
		var domainEvent = new EngagementFeedbackSubmittedDomainEvent(EngagementId.New(), UserId.New(), opportunityId, 5);

		// Act
		Func<Task> act = async () => await _sut.Handle(domainEvent, cancellationToken);

		// Assert
		await act.Should().NotThrowAsync();
		await _notifRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
	}
}
