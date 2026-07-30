using Application.Achievements.AwardAchievement.v1;
using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.ConfirmEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.ConfirmEngagement;

public class ConfirmEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IAggregateRepository<UserStreak, UserStreakId> _streakRepo =
		Substitute.For<IAggregateRepository<UserStreak, UserStreakId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<User, UserId> _userRepo =
		Substitute.For<IAggregateRepository<User, UserId>>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly ConfirmEngagementCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;

	public ConfirmEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext.UserStreaks.Returns(_streakRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Users.Returns(_userRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_sut = new ConfirmEngagementCommandHandler(_dbContext, _keycloakUserService, _emailService, _emailTemplateRenderer, _sender);
	}

	[Test]
	public async Task Handle_ShouldConfirmEngagement_WhenEngagementIsPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			UserId.New(),
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Confirmed);
	}

	[Test]
	public async Task Handle_ShouldReturnEngagement_WithCorrectId(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			UserId.New(),
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeSameAs(engagement);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyConfirmed(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			UserId.New(),
			TimeSlotId.New());
		engagement.Confirm();

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*Only pending*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			UserId.New(),
			TimeSlotId.New());
		engagement.Cancel();

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*Only pending*");
	}

	[Test]
	public async Task Handle_ShouldAwardWeeklyHeroBadge_WhenActivityStreakReaches4(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Streak at 3 consecutive weeks; next activity takes it to 4
		var streak = BuildActivityStreakOf(volunteerId, 3);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns(streak);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "weekly-hero-4" && c.UserId == volunteerId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotAwardWeeklyHeroBadge_WhenActivityStreakIsBelow3(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Streak at 1; next activity takes it to 2 - no badge yet
		var streak = BuildActivityStreakOf(volunteerId, 1);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns(streak);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.DidNotReceive().Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "weekly-hero-4"),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAwardFirstStepBadge_WhenVolunteerHasNoPriorStreak(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns((UserStreak?)null);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "first-step" && c.UserId == volunteerId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAwardDedicatedBadge_WhenLifetimeConfirmationsReach5_EvenIfLiveConfirmedCountIsLower(
		CancellationToken cancellationToken)
	{
		// Regression for #668: a volunteer's live "currently confirmed" count can be
		// pulled back down by an unrelated opportunity deletion/cancellation elsewhere.
		// Milestone eligibility must key off the monotonic lifetime counter on the
		// volunteer's UserStreak, not that live count.
		var engagementId = EngagementId.New();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var streak = BuildStreakWithTotalConfirmedEngagementsOf(volunteerId, 4);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns(streak);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "dedicated-5" && c.UserId == volunteerId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotAwardDedicatedBadge_WhenLifetimeConfirmationsAreBelow5(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var streak = BuildStreakWithTotalConfirmedEngagementsOf(volunteerId, 2);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns(streak);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.DidNotReceive().Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "dedicated-5"),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldAwardCenturionBadge_WhenLifetimeConfirmationsReach100(
		CancellationToken cancellationToken)
	{
		var engagementId = EngagementId.New();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var streak = BuildStreakWithTotalConfirmedEngagementsOf(volunteerId, 99);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns(streak);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<AwardAchievementCommand>(c => c!.BadgeKey == "centurion-100" && c.UserId == volunteerId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRenderConfirmationEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		var volunteer = User.Create(volunteerId);
		volunteer.SetPreferredLanguage("en");
		_userRepo.FindAsync(volunteerId, Arg.Any<CancellationToken>()).Returns(volunteer);

		// Act
		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementConfirmed,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	private VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	private static UserStreak BuildStreakWithTotalConfirmedEngagementsOf(UserId userId, int count)
	{
		var streak = UserStreak.Create(userId);
		for (var i = 0; i < count; i++)
			streak.RecordConfirmedEngagement();
		return streak;
	}

	private static UserStreak BuildActivityStreakOf(UserId userId, int weeks)
	{
		var streak = UserStreak.Create(userId);
		// Use Europe/Berlin (matches the handler) so week boundaries agree at runtime.
		var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
		var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, berlin).DateTime;
		var currentYear = System.Globalization.ISOWeek.GetYear(now);
		var currentWeek = System.Globalization.ISOWeek.GetWeekOfYear(now);
		for (var i = weeks; i >= 1; i--)
		{
			var week = currentWeek - i;
			var year = currentYear;
			if (week <= 0)
			{
				year--;
				week += 52;
			}
			streak.RecordActivity(year, week);
		}
		return streak;
	}
}
