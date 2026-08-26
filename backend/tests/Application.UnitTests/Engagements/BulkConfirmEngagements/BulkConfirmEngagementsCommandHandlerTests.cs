using Application.Achievements.AwardAchievement.v1;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.BulkConfirmEngagements.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.BulkConfirmEngagements;

public class BulkConfirmEngagementsCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notificationRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly BulkConfirmEngagementsCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly VolunteerOpportunityId DefaultOpportunityId = VolunteerOpportunityId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public BulkConfirmEngagementsCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notificationRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_dbContext
			.GetOrCreateUserStreakAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => UserStreak.Create(callInfo.Arg<UserId>()));
		_dbContext
			.GetEngagementsByIdsAsync(Arg.Any<IReadOnlyCollection<EngagementId>>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_sut = new BulkConfirmEngagementsCommandHandler(_dbContext, _sender);
	}

	private VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", null, "Test", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	private static Engagement CreatePendingEngagement(VolunteerOpportunityId? opportunityId = null) =>
		Engagement.CreateSlotSignUp(opportunityId ?? DefaultOpportunityId, UserId.New(), TimeSlotId.New());

	private void SeedEngagements(params Engagement[] engagements) =>
		_dbContext
			.GetEngagementsByIdsAsync(Arg.Any<IReadOnlyCollection<EngagementId>>(), Arg.Any<CancellationToken>())
			.Returns(engagements.ToList());

	[Test]
	public async Task Handle_ShouldReturnSucceeded_ForEveryConfirmableEngagement(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementA = CreatePendingEngagement();
		var engagementB = CreatePendingEngagement();
		SeedEngagements(engagementA, engagementB);

		var command = new BulkConfirmEngagementsCommand(
			DefaultOpportunityId,
			[engagementA.Id, engagementB.Id],
			DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Failed.Should().BeEmpty();
		result.Succeeded.Should().HaveCount(2);
		result.Succeeded.Should().Contain(s => s.EngagementId == engagementA.Id.Value && s.Status == "Confirmed");
		result.Succeeded.Should().Contain(s => s.EngagementId == engagementB.Id.Value && s.Status == "Confirmed");
		engagementA.Status.Should().Be(EngagementStatus.Confirmed);
		engagementB.Status.Should().Be(EngagementStatus.Confirmed);
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WithoutAbortingTheRestOfTheBatch_WhenAConfirmFails(
		CancellationToken cancellationToken)
	{
		// Arrange
		var okEngagement = CreatePendingEngagement();
		var badEngagement = CreatePendingEngagement();
		badEngagement.Confirm();
		SeedEngagements(okEngagement, badEngagement);

		var command = new BulkConfirmEngagementsCommand(
			DefaultOpportunityId,
			[okEngagement.Id, badEngagement.Id],
			DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().ContainSingle(s => s.EngagementId == okEngagement.Id.Value);
		result.Failed.Should().ContainSingle(f => f.EngagementId == badEngagement.Id.Value
			&& f.ErrorCode == "Engagement.NotPending"
			&& f.Message == "Only pending engagements can be confirmed.");
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WhenEngagementDoesNotExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var missingId = EngagementId.New();

		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [missingId], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().ContainSingle(f => f.EngagementId == missingId.Value && f.ErrorCode == "Engagement.NotFound");
		await _sender.DidNotReceive().Send(Arg.Any<AwardAchievementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldCollectFailure_WhenEngagementBelongsToADifferentOpportunity(
		CancellationToken cancellationToken)
	{
		// Arrange
		var foreignEngagement = CreatePendingEngagement(VolunteerOpportunityId.New());
		SeedEngagements(foreignEngagement);

		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [foreignEngagement.Id], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().ContainSingle(f => f.EngagementId == foreignEngagement.Id.Value && f.ErrorCode == "Engagement.WrongOpportunity");
		foreignEngagement.Status.Should().Be(EngagementStatus.Pending);
		await _sender.DidNotReceive().Send(Arg.Any<AwardAchievementCommand>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldConfirmEachDistinctEngagementIdOnlyOnce_WhenDuplicatesArePassed(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagement = CreatePendingEngagement();
		SeedEngagements(engagement);

		var command = new BulkConfirmEngagementsCommand(
			DefaultOpportunityId,
			[engagement.Id, engagement.Id],
			DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().ContainSingle();
		engagement.Status.Should().Be(EngagementStatus.Confirmed);
		await _dbContext.Received(1).GetEngagementsByIdsAsync(
			Arg.Is<IReadOnlyCollection<EngagementId>>(ids => ids.Count == 1 && ids.Contains(engagement.Id)),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		_opportunityRepo.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);
		var opportunityId = VolunteerOpportunityId.New();
		var command = new BulkConfirmEngagementsCommand(opportunityId, [EngagementId.New()], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage($"*{opportunityId.Value}*");
		await _dbContext.DidNotReceive().GetEngagementsByIdsAsync(
			Arg.Any<IReadOnlyCollection<EngagementId>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		_dbContext.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);
		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [EngagementId.New()], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*permission*");
		await _dbContext.DidNotReceive().GetEngagementsByIdsAsync(
			Arg.Any<IReadOnlyCollection<EngagementId>>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyResult_WhenNoEngagementIdsProvided(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new BulkConfirmEngagementsCommand(DefaultOpportunityId, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Succeeded.Should().BeEmpty();
		result.Failed.Should().BeEmpty();
	}
}
