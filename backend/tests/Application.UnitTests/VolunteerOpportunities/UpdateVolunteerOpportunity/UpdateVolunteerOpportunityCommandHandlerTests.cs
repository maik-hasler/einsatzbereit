using Application.Common.Geocoding;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.UpdateVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.UpdateVolunteerOpportunity;

public class UpdateVolunteerOpportunityCommandHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly UpdateVolunteerOpportunityCommandHandler _sut;

	private static readonly Address DefaultAddress = new("Hauptstraße", "1", "12345", "Berlin");
	private static readonly OrganizationId DefaultOrgId = new(Guid.CreateVersion7());

	public UpdateVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_sut = new UpdateVolunteerOpportunityCommandHandler(
			_dbContext,
			_geocodingService,
			NullLogger<UpdateVolunteerOpportunityCommandHandler>.Instance);
	}

	private static VolunteerOpportunity CreateOpportunity(string title = "Altes Thema", string description = "Alte Beschreibung") =>
		VolunteerOpportunity.Create(DefaultOrgId, title, description, false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist);

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
			opportunityId, "Neues Thema", "Neue Beschreibung", false, newAddress);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Should().BeTrue();
		opportunity.Title.Should().Be("Neues Thema");
		opportunity.Description.Should().Be("Neue Beschreibung");
		opportunity.Address.Should().Be(newAddress);
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
			opportunityId, "Neues Thema", "Neue Beschreibung", false, new Address("Neue Straße", "99", "20095", "Hamburg"));

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
			opportunityId, "Remote", "Desc", true, Address: null);

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
			opportunityId, "Titel", "Beschreibung", false, DefaultAddress);

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
			opportunityId, "   ", "Beschreibung", false, DefaultAddress);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Title must not be empty*");
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
			opportunityId, "Titel", "Beschreibung", false, Address: null);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<DomainException>()
			.WithMessage("*Address is required*");
	}
}
