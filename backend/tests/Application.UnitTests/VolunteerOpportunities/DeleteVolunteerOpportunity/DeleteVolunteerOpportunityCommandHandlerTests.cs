using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
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
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly DeleteVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public DeleteVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(new List<Engagement>());
		_dbContext
			.GetOpenReportsForTargetAsync(Arg.Any<ReportTargetType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new List<Report>());
		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<TimeSlotId?>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new DeleteVolunteerOpportunityCommandHandler(
			_dbContext, _engagementReadRepository, NullLogger<DeleteVolunteerOpportunityCommandHandler>.Instance);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldReturnTrue_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var result = await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		result.Should().BeTrue();
	}

	[Test]
	public async Task Handle_ShouldCallDelete_OnRepository(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		_opportunityRepo.Received(1).Delete(opportunity);
	}

	[Test]
	public async Task Handle_ShouldNotifyActiveVolunteers_WhenOpportunityDeleted(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var pendingVolunteer = Guid.NewGuid();
		var confirmedVolunteer = Guid.NewGuid();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([pendingVolunteer, confirmedVolunteer]);

		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert - one OpportunityDeleted notification per active volunteer, none for cancelled.
		// TitleSnapshot must be captured here since the opportunity row is hard-deleted
		// right after, so a later live lookup by relatedEntityId would find nothing to
		// interpolate {{title}} with (einsatzbereit#2073).
		await _notifRepo.Received(2).AddAsync(
			Arg.Is<Notification>(n => n!.Kind == NotificationKind.OpportunityDeleted
				&& n.RelatedEntityId == opportunityId
				&& n.TitleSnapshot == "Titel"),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldCancelActiveEngagements_WhenOpportunityDeleted(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var timeSlotId = TimeSlotId.New();
		var pendingEngagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), UserId.New(), timeSlotId);
		var confirmedEngagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), UserId.New(), timeSlotId);
		confirmedEngagement.Confirm();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_dbContext
			.GetActiveEngagementsForOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns([pendingEngagement, confirmedEngagement]);

		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		// Assert - active engagements are cancelled, not left dangling after the opportunity is gone.
		pendingEngagement.Status.Should().Be(EngagementStatus.Cancelled);
		pendingEngagement.CancellationReason.Should().Be("Opportunity was deleted.");
		confirmedEngagement.Status.Should().Be(EngagementStatus.Cancelled);
		confirmedEngagement.CancellationReason.Should().Be("Opportunity was deleted.");
	}

	[Test]
	public async Task Handle_ShouldNotifyAndCancelEachVolunteer_WhenActiveEngagementsAutoCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange - a deletion must in-app-notify+cancel the volunteer the same way
		// an organizer-triggered single-engagement cancel does (einsatzbereit#1057),
		// not just via the opportunity-level "was removed" notification. The email
		// itself now happens post-commit via EngagementCancelledNotificationHandler
		// (#1150), so this only proves each engagement is cancelled and carries the
		// right title on its event.
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var timeSlotId = TimeSlotId.New();
		var pendingEngagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), UserId.New(), timeSlotId);
		var confirmedEngagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), UserId.New(), timeSlotId);
		confirmedEngagement.Confirm();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.GetActiveEngagementsForOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns([pendingEngagement, confirmedEngagement]);

		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == pendingEngagement.VolunteerId!.Value
				&& n.Kind == NotificationKind.EngagementCancelled
				&& n.RelatedEntityId == pendingEngagement.Id.Value
				&& n.TitleSnapshot == "Titel"),
			cancellationToken);
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.RecipientId == confirmedEngagement.VolunteerId!.Value
				&& n.Kind == NotificationKind.EngagementCancelled
				&& n.RelatedEntityId == confirmedEngagement.Id.Value
				&& n.TitleSnapshot == "Titel"),
			cancellationToken);
		pendingEngagement.Events.Should().ContainSingle(e => e is EngagementCancelledDomainEvent)
			.Which.Should().BeOfType<EngagementCancelledDomainEvent>()
			.Which.OpportunityTitle.Should().Be("Titel");
		confirmedEngagement.Events.Should().ContainSingle(e => e is EngagementCancelledDomainEvent);
	}

	[Test]
	public async Task Handle_ShouldNotNotify_WhenNoActiveEngagements(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([]);

		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await _notifRepo.DidNotReceive().AddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldMarkOpenReportsActioned_WhenOpportunityDeleted(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var report = Report.Create(ReportTargetType.VolunteerOpportunity, opportunityId, UserId.New(), ReportReason.Spam, null).Value;

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_dbContext
			.GetOpenReportsForTargetAsync(ReportTargetType.VolunteerOpportunity, opportunityId, cancellationToken)
			.Returns([report]);

		await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		report.Status.Should().Be(ReportStatus.Actioned);
		report.ResolvedByUserId.Should().Be(DefaultRequestingUserId);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		Func<Task> act = async () => await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldNotCallDelete_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		try { await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken); }
		catch (ResultFailureException) { }

		_opportunityRepo.DidNotReceive().Delete(Arg.Any<VolunteerOpportunity>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		Func<Task> act = async () => await _sut.Handle(new DeleteVolunteerOpportunityCommand(opportunityId, DefaultRequestingUserId), cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		_opportunityRepo.DidNotReceive().Delete(Arg.Any<VolunteerOpportunity>());
	}
}
