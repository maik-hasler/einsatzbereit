using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Geocoding;
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
using Microsoft.Extensions.Logging.Abstractions;
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
	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IEmailService _emailService = Substitute.For<IEmailService>();
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
			.GetUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns(new KeycloakUserProfile(Guid.NewGuid(), "user", null, null, "user@example.com"));
		_emailService
			.SendBatchAsync(Arg.Any<IReadOnlyList<EmailMessage>>(), Arg.Any<CancellationToken>())
			.Returns(callInfo => callInfo.Arg<IReadOnlyList<EmailMessage>>()!.Select(_ => true).ToList());
		_sut = new UpdateVolunteerOpportunityCommandHandler(
			_dbContext,
			_engagementReadRepository,
			_geocodingService,
			_pinGenerator,
			_keycloakUserService,
			_emailService,
			NullLogger<UpdateVolunteerOpportunityCommandHandler>.Instance);
	}

	private VolunteerOpportunity CreateOpportunity(string title = "Altes Thema", string description = "Alte Beschreibung") =>
		VolunteerOpportunity.Create(DefaultOrgId, title, description, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator).Value;

	private VolunteerOpportunity CreateDraftOpportunity(string title = "Altes Thema", string description = "Alte Beschreibung") =>
		VolunteerOpportunity.Create(DefaultOrgId, title, description, false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator, status: OpportunityStatus.Draft).Value;

	private VolunteerOpportunity CreatePublishedWaitlistOpportunity()
	{
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Altes Thema", "Alte Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, _pinGenerator,
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
			opportunityId, "Neues Thema", "Neue Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.PINCode, null, [], DefaultRequestingUserId,
			CheckInPin: "13579");

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.CheckInPin.Should().Be("13579");
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

		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.TransientFailure);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, newAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.Manual, null, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.Title.Should().Be("Neues Thema");
		opportunity.Description.Should().Be("Neue Beschreibung");
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
			opportunityId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.Recurring, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.Occurrence.Should().Be(Occurrence.Recurring);
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenParticipationTypeChanges_AndActiveEngagementsExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedWaitlistOpportunity();

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
			opportunityId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*ParticipationType cannot be changed*");
	}

	[Test]
	public async Task Handle_ShouldAllowParticipationTypeChange_WhenOnlyCancelledEngagementsExist(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreatePublishedWaitlistOpportunity();

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
			opportunityId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.ParticipationType.Should().Be(ParticipationType.IndividualContact);
	}

	[Test]
	public async Task Handle_ShouldPersistCoordinates_WhenGeocodingSucceeds(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.Found(new GeoCoordinates(53.55, 9.99)));

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, Address.Create("Neue Straße", "99", "20095", "Hamburg").Value, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		opportunity.Address!.Latitude.Should().Be(53.55);
		opportunity.Address!.Longitude.Should().Be(9.99);
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
			opportunityId, "Remote", "Desc", true, Address: null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

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
			opportunityId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

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
			opportunityId, "   ", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

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
			opportunityId, "", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.Title.Should().Be(string.Empty);
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

		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.TransientFailure);

		// Material change: new address (city changed).
		var newAddress = Address.Create("Neue Straße", "99", "20095", "Hamburg").Value;
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, newAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

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

		// Cosmetic change only: title and description change, address/remote/occurrence unchanged.
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert - no notification and no email should be sent
		await _notifRepo.DidNotReceive().AddAsync(
			Arg.Any<Notification>(),
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
			opportunityId, "Titel", "Beschreibung", false, Address: null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

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
		// Arrange: caller belongs to a different organization than the opportunity's.
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		opportunity.Title.Should().Be("Altes Thema");
		opportunity.Description.Should().Be("Alte Beschreibung");
	}

	[Test]
	public async Task Handle_ShouldThrowValidationError_WhenGeocodingReturnsNotFound_ForChangedAddress(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.NotFound);

		var newAddress = Address.Create("Nirgendwostraße", "999", "99999", "Nirgendwo").Value;
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, newAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Code.Should().Be("Address.NotFound");
		opportunity.Title.Should().Be("Altes Thema");
		opportunity.Address.Should().Be(DefaultAddress);
	}

	[Test]
	public async Task Handle_ShouldNotReGeocode_AndShouldPreserveExistingCoordinates_WhenAddressTextUnchanged(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var geocodedAddress = DefaultAddress.WithCoordinates(52.52, 13.405).GetValueOrThrow();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Altes Thema", "Alte Beschreibung", false, geocodedAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator).GetValueOrThrow();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		// Same street/house number/zip/city as geocodedAddress - only cosmetic fields change.
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _geocodingService
			.DidNotReceive()
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		opportunity.Address!.Latitude.Should().Be(52.52);
		opportunity.Address!.Longitude.Should().Be(13.405);
	}

	[Test]
	public async Task Handle_ShouldReGeocode_WhenSwitchingFromRemoteToPhysicalAddress(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = VolunteerOpportunity.Create(
			DefaultOrgId, "Altes Thema", "Alte Beschreibung", true, null, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator).GetValueOrThrow();

		_opportunityRepo
			.FindAsync(VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow(), cancellationToken)
			.Returns(opportunity);

		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.Found(new GeoCoordinates(52.52, 13.405)));

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _geocodingService
			.Received(1)
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
		opportunity.Address!.Latitude.Should().Be(52.52);
	}
}
