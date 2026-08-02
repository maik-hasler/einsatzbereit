using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements.WithdrawEngagement.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.WithdrawEngagement;

public class WithdrawEngagementCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IKeycloakOrganizationService _keycloakService =
		Substitute.For<IKeycloakOrganizationService>();
	private readonly IKeycloakUserService _keycloakUserService =
		Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly IAggregateRepository<Engagement, EngagementId> _engagementRepo =
		Substitute.For<IAggregateRepository<Engagement, EngagementId>>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IUnsubscribeLinkBuilder _unsubscribeLinkBuilder = Substitute.For<IUnsubscribeLinkBuilder>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly WithdrawEngagementCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;
	private static readonly Address TestAddress = Address.Create("Main St", "1", "12345", "Berlin").Value;

	public WithdrawEngagementCommandHandlerTests()
	{
		_dbContext.Engagements.Returns(_engagementRepo);
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_keycloakUserService
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "volunteer", "Test", "User", "volunteer@example.com"));
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(new EmailContent("Test Subject", "Test Body"));
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_sut = new WithdrawEngagementCommandHandler(_dbContext, _keycloakService, _keycloakUserService, _emailService, _emailTemplateRenderer, _unsubscribeLinkBuilder);
	}

	private VolunteerOpportunity CreateOpportunityForOrganizerNotification(VolunteerOpportunityId opportunityId, out Guid organizerUserId)
	{
		var opportunity = VolunteerOpportunity.Create(
			OrganizationId.New(), "Test", "Test", false, DefaultAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None,
			_pinGenerator, status: OpportunityStatus.Draft).Value;
		_opportunityRepo.FindAsync(opportunityId, Arg.Any<CancellationToken>()).Returns(opportunity);

		organizerUserId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(opportunity.OrganizationId.Value, Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerUserId, "organizer", "Org", "Anizer", "organizer@example.com", true)]);
		return opportunity;
	}

	private static (Engagement engagement, UserId volunteerId) CreatePendingEngagementWithVolunteer()
	{
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(
			VolunteerOpportunityId.New(),
			volunteerId,
			TimeSlotId.New());
		return (engagement, volunteerId);
	}

	private VolunteerOpportunity CreateOpportunity() =>
		VolunteerOpportunity.Create(
			OrganizationId.New(), "Test Opportunity", "Description", false, TestAddress,
			Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;

	[Test]
	public async Task Handle_ShouldWithdrawEngagement_WhenCalledByOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public async Task Handle_ShouldWithdrawConfirmedEngagement_WhenCalledByOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Confirm();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Status.Should().Be(EngagementStatus.Withdrawn);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		// Arrange
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns((Engagement?)null);

		var command = new WithdrawEngagementCommand(engagementId, Guid.NewGuid());

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{engagementId.Value}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenCallerIsNotOwner(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, _) = CreatePendingEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		var differentUserId = Guid.NewGuid();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, differentUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Only the volunteer*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsAlreadyWithdrawn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Withdraw();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*already terminated*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCancelled(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Cancel();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*already terminated*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenEngagementIsCheckedIn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		engagement.Confirm();
		engagement.CheckIn();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*checked-in*");
	}

	[Test]
	public async Task Handle_ShouldRenderOrganizerEmail_InOrganizersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var (engagement, volunteerId) = CreatePendingEngagementWithVolunteer();
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		var opportunity = CreateOpportunityForOrganizerNotification(engagement.OpportunityId, out var organizerUserId);
		var organizerId = UserId.Create(organizerUserId).GetValueOrThrow();
		var organizer = User.Create(organizerId);
		organizer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([organizer]);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert - the organizer's own language, not the withdrawing volunteer's,
		// governs this email since the organizer is the recipient.
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.EngagementWithdrawnNotifyOrganizer,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	// --- Organizer email notification preferences (#1055) ---

	[Test]
	public async Task Handle_ShouldEmailOrganizer_WhenSubscribedToWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, TimeSlotId.New());
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		_unsubscribeLinkBuilder.Build(Arg.Any<UserId>(), Arg.Any<Guid>(), Arg.Any<EmailNotificationType>())
			.Returns("https://example.com/unsubscribe");

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _emailService.Received(1).SendAsync(
			"olaf@example.com",
			Arg.Any<string>(),
			Arg.Is<string>(body => body!.Contains("https://example.com/unsubscribe")),
			Arg.Any<string>(),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldNotEmailOrganizer_WhenOptedOutOfWithdrawal(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunity = CreateOpportunity();
		var volunteerId = UserId.New();
		var engagement = Engagement.CreateSlotSignUp(opportunity.Id, volunteerId, TimeSlotId.New());
		var engagementId = EngagementId.New();
		_engagementRepo.FindAsync(engagementId, cancellationToken).Returns(engagement);
		_opportunityRepo.FindAsync(opportunity.Id, Arg.Any<CancellationToken>()).Returns(opportunity);
		var organizerId = Guid.NewGuid();
		_keycloakService.GetMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(organizerId, "olaf", "Olaf", "Organizer", "olaf@example.com", true)]);
		var optedOutOrganizer = User.Create(UserId.Create(organizerId).GetValueOrThrow());
		optedOutOrganizer.UpdateNotificationPreferences(
			notifyOnNewSignUp: true,
			notifyOnWithdrawal: false,
			notifyOnEngagementConfirmed: true,
			notifyOnEngagementCancelled: true,
			notifyOnEngagementReminder: true);
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([optedOutOrganizer]);

		var command = new WithdrawEngagementCommand(engagementId, volunteerId.Value);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _emailService.DidNotReceive().SendAsync(
			"olaf@example.com", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
