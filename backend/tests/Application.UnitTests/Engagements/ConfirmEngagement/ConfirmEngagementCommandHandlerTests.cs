using Application.Achievements.AwardAchievement.v1;
using Application.Common.Email;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.ConfirmEngagement.v1;
using AwesomeAssertions;
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
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IKeycloakOrganizationService _keycloakOrgService = Substitute.For<IKeycloakOrganizationService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly ISender _sender = Substitute.For<ISender>();
	private readonly ConfirmEngagementCommandHandler _sut;

	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());
	private static readonly OrganizationId DefaultOrgId = new(Guid.Empty);
	private static readonly Address DefaultAddress = new("Teststraße", "1", "12345", "Berlin");

	public ConfirmEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_dbContext.UserStreaks.Returns(_streakRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_keycloakOrgService
			.GetUserOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(Guid.Empty, "any")]);
		_sut = new ConfirmEngagementCommandHandler(_dbContext, _keycloakUserService, _keycloakOrgService, _emailService, _sender);
	}

	[Test]
	public async Task Handle_ShouldConfirmEngagement_WhenEngagementIsPending(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));

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
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));

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
		var engagementId = new EngagementId(Guid.CreateVersion7());
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyConfirmed(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));
		engagement.Confirm();

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>().WithMessage("*Only pending*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			new UserId(Guid.CreateVersion7()),
			new TimeSlotId(Guid.CreateVersion7()));
		engagement.Cancel();

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>().WithMessage("*Only pending*");
	}

	[Test]
	public async Task Handle_ShouldAwardWeeklyHeroBadge_WhenActivityStreakReaches4(
		CancellationToken cancellationToken)
	{
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var volunteerId = new UserId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			volunteerId,
			new TimeSlotId(Guid.CreateVersion7()));

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Streak at 3 consecutive weeks; next activity takes it to 4
		var streak = BuildActivityStreakOf(volunteerId, 3);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns(streak);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.Received(1).Send(
			Arg.Is<AwardAchievementCommand>(c => c.BadgeKey == "weekly-hero-4" && c.UserId == volunteerId),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotAwardWeeklyHeroBadge_WhenActivityStreakIsBelow3(
		CancellationToken cancellationToken)
	{
		var engagementId = new EngagementId(Guid.CreateVersion7());
		var volunteerId = new UserId(Guid.CreateVersion7());
		var engagement = Engagement.CreateWaitlistSignUp(
			new VolunteerOpportunityId(Guid.CreateVersion7()),
			volunteerId,
			new TimeSlotId(Guid.CreateVersion7()));

		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		// Streak at 1; next activity takes it to 2 - no badge yet
		var streak = BuildActivityStreakOf(volunteerId, 1);
		_dbContext.GetUserStreakAsync(volunteerId, cancellationToken).Returns(streak);

		await _sut.Handle(new ConfirmEngagementCommand(engagementId, DefaultRequestingUserId), cancellationToken);

		await _sender.DidNotReceive().Send(
			Arg.Is<AwardAchievementCommand>(c => c.BadgeKey == "weekly-hero-4"),
			Arg.Any<CancellationToken>());
	}

	private static VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None);

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
