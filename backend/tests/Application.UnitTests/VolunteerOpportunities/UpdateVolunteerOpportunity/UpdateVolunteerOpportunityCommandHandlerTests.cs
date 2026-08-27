using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.UpdateVolunteerOpportunity;

public class UpdateVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IAggregateRepository<Notification, NotificationId> _notifRepo =
		Substitute.For<IAggregateRepository<Notification, NotificationId>>();
	private readonly IEngagementReadRepository _engagementReadRepository = Substitute.For<IEngagementReadRepository>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
	private readonly IEmailTemplateRenderer _emailTemplateRenderer = Substitute.For<IEmailTemplateRenderer>();
	private readonly UpdateVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public UpdateVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_engagementReadRepository
			.GetByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<TimeSlotId?>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => callInfo.Arg<IReadOnlyList<Guid>>()!
				.ToDictionary(id => id, id => new KeycloakUserProfile(id, "user", null, null, "user@example.com")));
		_emailService
			.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => callInfo.Arg<IReadOnlyList<EmailMessage>>()!.Select(_ => true).ToList());
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns(call => ((IReadOnlyCollection<UserId>)call[0]!).Select(User.Create).ToList());
		_emailTemplateRenderer
			.Render(Arg.Any<EmailTemplateKind>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>())
			.Returns(callInfo =>
			{
				var placeholders = (IReadOnlyDictionary<string, string>)callInfo[2]!;
				return new EmailContent("Test Subject", $"Test Body {string.Join(" ", placeholders.Values)}");
			});
		_sut = new UpdateVolunteerOpportunityCommandHandler(
			_dbContext,
			_engagementReadRepository,
			_pinGenerator,
			_keycloakUserService,
			_emailService,
			_emailTemplateRenderer);
	}

	private VolunteerOpportunity CreateOpportunity(string title = "Altes Thema", string description = "Alte Beschreibung") =>
		VolunteerOpportunity.Create(
			DefaultOrgId, title, null, description, null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).Value;

	private VolunteerOpportunity CreateDraftOpportunity(string title = "Altes Thema", string description = "Alte Beschreibung") =>
		VolunteerOpportunity.Create(DefaultOrgId, title, null, description, null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	private VolunteerOpportunity CreatePublishedScheduledSlotsOpportunity()
	{
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Altes Thema", null, "Alte Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, _pinGenerator,
			status: OpportunityStatus.Draft).Value;
		opportunity.AddTimeSlot(DateTimeOffset.UtcNow.AddDays(7), DateTimeOffset.UtcNow.AddDays(7).AddHours(2), 10, DateTimeOffset.UtcNow);
		opportunity.Publish();
		return opportunity;
	}

	[Test]
	public async Task Handle_ShouldUseGivenCheckInPin(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).Value, cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.PINCode, null, [], DefaultRequestingUserId,
			CheckInPin: "13579");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.CheckInPin.Should().Be("13579");
	}

	[Test]
	public async Task Handle_ShouldUpdateTitleEnAndDescriptionEn(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).Value, cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "New topic", "Neue Beschreibung", "New description", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.TitleDe.Should().Be("Neues Thema");
		opportunity.TitleEn.Should().Be("New topic");
		opportunity.DescriptionDe.Should().Be("Neue Beschreibung");
		opportunity.DescriptionEn.Should().Be("New description");
	}

	[Test]
	public async Task Handle_ShouldUpdateFields_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var newAddress = Address.Create("Neue Straße", "99", "20095", "Hamburg").Value;

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, newAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.Manual, null, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.TitleDe.Should().Be("Neues Thema");
		opportunity.DescriptionDe.Should().Be("Neue Beschreibung");
		opportunity.Address.Should().Be(newAddress);
	}

	[Test]
	public async Task Handle_ShouldUpdateOccurrenceAndParticipationType(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.Recurring, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public async Task Handle_ShouldSetValidUntil_WhenGivenForIndividualContact(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var validUntil = DateTimeOffset.UtcNow.AddDays(60);

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId,
			ValidUntil: validUntil);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.ValidUntil.Should().Be(validUntil);
	}

	[Test]
	public async Task Handle_ShouldClearValidUntil_WhenSwitchingToScheduledSlots(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();
		opportunity.SetValidUntil(DateTimeOffset.UtcNow.AddDays(30), DateTimeOffset.UtcNow);

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.ValidUntil.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenValidUntilNotInFuture(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId,
			ValidUntil: DateTimeOffset.UtcNow.AddDays(-1));

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Deadline must be in the future*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenParticipationTypeChanges_AndActiveEngagementsExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedScheduledSlotsOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(
			[
				new EngagementSummary(Guid.NewGuid(), opportunityId, "Test Opportunity", Guid.NewGuid(), "Org", Guid.NewGuid(), null, null, "Pending", false, false, DateTimeOffset.UtcNow)
			]);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*ParticipationType cannot be changed*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenParticipationTypeChanges_AndOnlyCancelledEngagementsExist(
		CancellationToken cancellationToken)
	{
		// Arrange

		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedScheduledSlotsOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(
			[
				new EngagementSummary(Guid.NewGuid(), opportunityId, "Test Opportunity", Guid.NewGuid(), "Org", Guid.NewGuid(), null, null, "Cancelled", false, false, DateTimeOffset.UtcNow)
			]);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*ParticipationType cannot be changed*");
	}

	[Test]

	public async Task Handle_ShouldAllowRemote_WithNullAddress(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Remote", null, "Desc", null, true, Address: null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.IsRemote.Should().BeTrue();
		opportunity.Address.Should().BeNull();
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

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage($"*{opportunityId}*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenTitleIsEmpty(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "   ", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Title must not be empty*");
	}

	[Test]
	public async Task Handle_ShouldAllowEmptyTitle_WhenDraft(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateDraftOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.TitleDe.Should().Be(string.Empty);
	}

	[Test]
	public async Task Handle_ShouldNotifyActiveVolunteers_WhenAddressChanges(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var activeVolunteer = Guid.NewGuid();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([activeVolunteer]);

		var newAddress = Address.Create("Neue Straße", "99", "20095", "Hamburg").Value;
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, newAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.Kind == NotificationKind.OpportunityUpdated && n.RecipientId.Value == activeVolunteer),
			cancellationToken);
		await _emailService.Received(1).SendBatchAsync(
			Arg.Is<IReadOnlyList<EmailMessage>>(messages =>
				messages!.Count == 1 && messages[0].To == "user@example.com" && messages[0].Body.Contains("Neues Thema")),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldStillUpdateOpportunity_WhenVolunteerNotificationEmailFailsToSend(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var activeVolunteer = Guid.NewGuid();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([activeVolunteer]);

		_emailService.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), cancellationToken)
			.Returns([false]);

		var newAddress = Address.Create("Neue Straße", "99", "20095", "Hamburg").Value;
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, newAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue(
			"a notification email failing to send must not block or roll back an otherwise-valid opportunity update - " +
			"unlike an outbox-dispatched handler there is no retry path here, and a permanently bad recipient address " +
			"would otherwise deterministically block every future edit to this opportunity");
		opportunity.TitleDe.Should().Be("Neues Thema");
	}

	[Test]
	public async Task Handle_ShouldRenderOpportunityUpdatedEmail_InVolunteersPreferredLanguage(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var activeVolunteerId = UserId.New();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([activeVolunteerId.Value]);

		var activeVolunteer = User.Create(activeVolunteerId);
		activeVolunteer.SetPreferredLanguage("en");
		_dbContext.GetOrCreateUsersAsync(Arg.Any<IReadOnlyCollection<UserId>>(), Arg.Any<CancellationToken>())
			.Returns([activeVolunteer]);

		var newAddress = Address.Create("Neue Straße", "99", "20095", "Hamburg").Value;
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, newAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		_emailTemplateRenderer.Received(1).Render(
			EmailTemplateKind.OpportunityUpdated,
			"en",
			Arg.Any<IReadOnlyDictionary<string, string>>());
	}

	[Test]
	public async Task Handle_ShouldNotNotifyVolunteers_WhenOnlyCosmeticFieldsChange(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var activeVolunteer = Guid.NewGuid();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([activeVolunteer]);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _notifRepo.DidNotReceive().AddAsync(
			Arg.Any<Notification>(),
			cancellationToken);
		await _emailService.DidNotReceive().SendBatchAsync(
			Arg.Any<IReadOnlyList<EmailMessage>>(),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenTooManyTags(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var tooManyTags = Enumerable.Range(0, VolunteerOpportunity.MaxTagsCount + 1).Select(i => $"tag{i}").ToList();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, tooManyTags, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*cannot have more than*");
	}

	[Test]
	public async Task Handle_ShouldSkipVolunteer_WhenKeycloakProfileLookupFails(
		CancellationToken cancellationToken)
	{
		// Arrange

		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var activeVolunteer = Guid.NewGuid();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetActiveVolunteerIdsByOpportunityAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), Arg.Any<TimeSlotId?>(), cancellationToken)
			.Returns([activeVolunteer]);

		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>());

		var newAddress = Address.Create("Neue Straße", "99", "20095", "Hamburg").Value;
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, newAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n!.Kind == NotificationKind.OpportunityUpdated && n.RecipientId.Value == activeVolunteer),
			cancellationToken);
		await _emailService.DidNotReceive().SendBatchAsync(
			Arg.Any<IReadOnlyList<EmailMessage>>(),
			Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenNonRemoteAndNoAddress(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", null, "Beschreibung", null, false, Address: null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Address is required*");
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		opportunity.TitleDe.Should().Be("Altes Thema");
		opportunity.DescriptionDe.Should().Be("Alte Beschreibung");
	}

	[Test]
	public async Task Handle_ShouldRaiseGeocodingRequestedEvent_AndResetCoordinates_WhenAddressTextChanges(
		CancellationToken cancellationToken)
	{
		// Arrange

		var opportunityId = Guid.CreateVersion7();
		var geocodedAddress = DefaultAddress.WithCoordinates(52.52, 13.405).GetValueOrThrow();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Altes Thema", null, "Alte Beschreibung", null, false, geocodedAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).GetValueOrThrow();
		opportunity.ClearEvents();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var newAddress = Address.Create("Nirgendwostraße", "999", "99999", "Nirgendwo").Value;
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, newAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.Address.Should().Be(newAddress);
		opportunity.Address!.Latitude.Should().BeNull();
		opportunity.Events.OfType<VolunteerOpportunityGeocodingRequestedDomainEvent>()
			.Should().ContainSingle()
			.Which.OpportunityId.Should().Be(opportunity.Id);
	}

	[Test]
	public async Task Handle_ShouldNotRaiseGeocodingRequestedEvent_AndShouldPreserveExistingCoordinates_WhenAddressTextUnchanged(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var geocodedAddress = DefaultAddress.WithCoordinates(52.52, 13.405).GetValueOrThrow();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Altes Thema", null, "Alte Beschreibung", null, false, geocodedAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).GetValueOrThrow();
		opportunity.ClearEvents();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.Events.Should().NotContain(e => e is VolunteerOpportunityGeocodingRequestedDomainEvent);
		opportunity.Address!.Latitude.Should().Be(52.52);
		opportunity.Address!.Longitude.Should().Be(13.405);
	}

	[Test]
	public async Task Handle_ShouldRaiseGeocodingRequestedEvent_WhenSwitchingFromRemoteToPhysicalAddress(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Altes Thema", null, "Alte Beschreibung", null, true, null, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).GetValueOrThrow();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", null, "Neue Beschreibung", null, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.Address.Should().Be(DefaultAddress);
		opportunity.Events.OfType<VolunteerOpportunityGeocodingRequestedDomainEvent>().Should().ContainSingle();
	}
}
