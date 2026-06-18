using Application.Common.Geocoding;
using Application.Common.Keycloak;
using Application.Common.Persistence;
using Application.Engagements;
using Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;
using AwesomeAssertions;
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
	private readonly IKeycloakOrganizationService _keycloakOrgService = Substitute.For<IKeycloakOrganizationService>();
	private readonly UpdateVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = new("Hauptstraße", "1", "12345", "Berlin");
	private static readonly OrganizationId DefaultOrgId = new(Guid.CreateVersion7());
	private static readonly UserId DefaultRequestingUserId = new(Guid.CreateVersion7());

	public UpdateVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_dbContext.Notifications.Returns(_notifRepo);
		_engagementReadRepository
			.GetByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns([]);
		_keycloakOrgService
			.GetUserOrganizationsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganization(DefaultOrgId.Value, "Test Org")]);
		_sut = new UpdateVolunteerOpportunityCommandHandler(
			_dbContext,
			_engagementReadRepository,
			_geocodingService,
			_keycloakOrgService,
			NullLogger<UpdateVolunteerOpportunityCommandHandler>.Instance);
	}

	private static VolunteerOpportunity CreateOpportunity(string title = "Altes Thema", string description = "Alte Beschreibung") =>
		VolunteerOpportunity.Create(DefaultOrgId, title, description, false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None);

	[Test]
	public async Task Handle_ShouldUpdateFields_WhenOpportunityExists(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();
		var newAddress = new Address("Neue Straße", "99", "20095", "Hamburg");

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

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
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
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
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetByOpportunityAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(
			[
				new EngagementSummary(Guid.NewGuid(), opportunityId, "Test Opportunity", Guid.NewGuid(), "Org", Guid.NewGuid(), null, null, "Pending", false, DateTimeOffset.UtcNow)
			]);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.IndividualContact, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*ParticipationType cannot be changed*");
	}

	[Test]
	public async Task Handle_ShouldAllowParticipationTypeChange_WhenOnlyCancelledEngagementsExist(
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
				new EngagementSummary(Guid.NewGuid(), opportunityId, "Test Opportunity", Guid.NewGuid(), "Org", Guid.NewGuid(), null, null, "Cancelled", false, DateTimeOffset.UtcNow)
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
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new GeoCoordinates(53.55, 9.99));

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, new Address("Neue Straße", "99", "20095", "Hamburg"), Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

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
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
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
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns((VolunteerOpportunity?)null);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
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
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "   ", "Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Title must not be empty*");
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
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetByOpportunityAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(
			[
				new EngagementSummary(Guid.NewGuid(), opportunityId, "T", Guid.NewGuid(), "Org", activeVolunteer, null, null, "Confirmed", false, DateTimeOffset.UtcNow),
			]);

		// Material change: new address (city changed).
		var newAddress = new Address("Neue Straße", "99", "20095", "Hamburg");
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, newAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _notifRepo.Received(1).AddAsync(
			Arg.Is<Notification>(n => n.Kind == NotificationKind.OpportunityUpdated && n.RecipientId.Value == activeVolunteer),
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
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		_engagementReadRepository
			.GetByOpportunityAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(
			[
				new EngagementSummary(Guid.NewGuid(), opportunityId, "T", Guid.NewGuid(), "Org", activeVolunteer, null, null, "Confirmed", false, DateTimeOffset.UtcNow),
			]);

		// Cosmetic change only: title and description change, address/remote/occurrence unchanged.
		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Neues Thema", "Neue Beschreibung", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert - no notification should be added
		await _notifRepo.DidNotReceive().AddAsync(
			Arg.Any<Notification>(),
			cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenNonRemoteAndNoAddress(
		CancellationToken cancellationToken)
	{
		// Arrange
		var opportunityId = Guid.CreateVersion7();
		var opportunity = CreateOpportunity();

		_opportunityRepo
			.FindAsync(new VolunteerOpportunityId(opportunityId), cancellationToken)
			.Returns(opportunity);

		var command = new UpdateVolunteerOpportunityCommand(
			opportunityId, "Titel", "Beschreibung", false, Address: null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Address is required*");
	}
}
