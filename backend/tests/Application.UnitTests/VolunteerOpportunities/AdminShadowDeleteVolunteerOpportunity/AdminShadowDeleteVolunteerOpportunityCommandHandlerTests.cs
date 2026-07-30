using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.AdminShadowDeleteVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.AdminShadowDeleteVolunteerOpportunity;

public class AdminShadowDeleteVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IAggregateRepository<Report, ReportId> _reportRepo =
		Substitute.For<IAggregateRepository<Report, ReportId>>();
	private readonly IEngagementReadRepository _engagementReadRepository =
		Substitute.For<IEngagementReadRepository>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly AdminShadowDeleteVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultAdminUserId = UserId.New();

	public AdminShadowDeleteVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext.Reports.Returns(_reportRepo);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(new List<Engagement>());
		_dbContext
			.GetOpenReportsForTargetAsync(Arg.Any<ReportTargetType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new List<Report>());
		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<TimeSlotId?>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_sut = new AdminShadowDeleteVolunteerOpportunityCommandHandler(_dbContext, _engagementReadRepository, _keycloakUserService, _emailService);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldShadowDeleteOpportunity_WithoutCheckingOwnership(
		CancellationToken cancellationToken)
	{
		// Arrange: no IsOrganizerAsync stub configured at all - if the handler
		// called OwnershipGuard, NSubstitute's default (false) would make this fail.
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		// Act
		var result = await _sut.Handle(new AdminShadowDeleteVolunteerOpportunityCommand(opportunityId, DefaultAdminUserId), cancellationToken);

		// Assert: shadow-deleted, not removed - the takedown must be restorable.
		result.Should().BeTrue();
		opportunity.IsDeleted.Should().BeTrue();
		_opportunityRepo.DidNotReceive().Delete(Arg.Any<VolunteerOpportunity>());
	}

	[Test]
	public async Task Handle_ShouldMarkOpenReportsActioned_WhenOpportunityShadowDeleted(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		var report = Report.Create(ReportTargetType.VolunteerOpportunity, opportunityId, UserId.New(), ReportReason.IllegalContent, null).Value;
		_dbContext
			.GetOpenReportsForTargetAsync(ReportTargetType.VolunteerOpportunity, opportunityId, cancellationToken)
			.Returns([report]);

		// Act
		await _sut.Handle(new AdminShadowDeleteVolunteerOpportunityCommand(opportunityId, DefaultAdminUserId), cancellationToken);

		// Assert
		report.Status.Should().Be(ReportStatus.Actioned);
		report.ResolvedByUserId.Should().Be(DefaultAdminUserId);
	}

	[Test]
	public async Task Handle_ShouldNotifyAndEmailEachVolunteer_WhenActiveEngagementsAutoCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange - same guarantee as the organizer-triggered delete (#1057): a
		// shadow-delete's auto-cancelled engagements must notify+email too.
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var timeSlotId = TimeSlotId.New();
		var engagement = Engagement.CreateWaitlistSignUp(
			VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), UserId.New(), timeSlotId);
		engagement.Confirm();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns([engagement]);

		// Act
		await _sut.Handle(new AdminShadowDeleteVolunteerOpportunityCommand(opportunityId, DefaultAdminUserId), cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == engagement.VolunteerId!.Value
				&& n.Kind == NotificationKind.EngagementCancelled
				&& n.RelatedEntityId == engagement.Id.Value),
			cancellationToken);
		await _emailService.Received(1).SendAsync(
			"user@example.com",
			"Your engagement has been cancelled",
			Arg.Is<string>(body => body!.Contains("Reason: Opportunity was deleted.")),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		// Act
		Func<Task> act = async () => await _sut.Handle(new AdminShadowDeleteVolunteerOpportunityCommand(opportunityId, DefaultAdminUserId), cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}
}
