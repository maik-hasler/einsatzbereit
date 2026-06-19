using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.DeleteVolunteerOpportunity;

public class DeleteVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IEngagementReadRepository _engagementReadRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly IKeycloakOrganizationService _keycloakOrgService = Substitute.For<IKeycloakOrganizationService>();
	private readonly DeleteVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = new("Hauptstraße", "1", "12345", "Berlin");
	private static readonly OrganizationId DefaultOrgId = new(Guid.CreateVersion7());
	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());

	public DeleteVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_engagementReadRepository
			.GetByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakOrgService
			.GetUserOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(DefaultOrgId.Value, "Test Org")]);
		_sut = new DeleteVolunteerOpportunityCommandHandler(_dbContext, _engagementReadRepository, _keycloakOrgService);
	}

	private static VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None);

	[Test]
	public async Task Handle_ShouldReturnTrue_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		// Act
		var result = await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		result.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldCallDelete_OnRepository(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		// Act
		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		_opportunityRepo.Received(1).Delete(opportunity);
	}

	[Test]
	public async Task Handle_ShouldNotifyActiveVolunteers_WhenOpportunityDeleted(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var pendingVolunteer = Guid.NewGuid();
		var confirmedVolunteer = Guid.NewGuid();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetByOpportunityAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(
			[
				new EngagementSummary(Guid.NewGuid(), opportunityId, "T", Guid.NewGuid(), "Org", pendingVolunteer, null, null, "Pending", false, false, DateTimeOffset.UtcNow),
				new EngagementSummary(Guid.NewGuid(), opportunityId, "T", Guid.NewGuid(), "Org", confirmedVolunteer, null, null, "Confirmed", false, false, DateTimeOffset.UtcNow),
				new EngagementSummary(Guid.NewGuid(), opportunityId, "T", Guid.NewGuid(), "Org", Guid.NewGuid(), null, null, "Cancelled", false, false, DateTimeOffset.UtcNow),
			]);

		// Act
		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert - one OpportunityDeleted notification per active volunteer, none for cancelled.
		await _notifRepo.Received(2).AddAsync(
			Arg.Is<Notification>(n => n.Kind == NotificationKind.OpportunityDeleted && n.RelatedEntityId == opportunityId),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotNotify_WhenNoActiveEngagements(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetByOpportunityAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(
			[
				new EngagementSummary(Guid.NewGuid(), opportunityId, "T", Guid.NewGuid(), "Org", Guid.NewGuid(), null, null, "Cancelled", false, false, DateTimeOffset.UtcNow),
			]);

		// Act
		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		await _notifRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldNotCallDelete_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		// Act
		try { await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken); }
		catch (DomainException) { }

		// Assert
		_opportunityRepo.DidNotReceive().Delete(Arg.Any<VolunteerOpportunity>());
	}
}
