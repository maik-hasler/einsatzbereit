using Application.Common.Geocoding;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging;
using NSubstitute;


namespace Application.UnitTests.VolunteerOpportunities.CreateVolunteerOpportunity;

public class CreateVolunteerOpportunityCommandHandlerTests
{
	private static readonly OrganizationId TestOrganizationId = new(Guid.NewGuid());
	private static readonly Address TestAddress = new("Sample Street", "1", "12345", "Berlin");

	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly ILogger<CreateVolunteerOpportunityCommandHandler> _logger =
		Substitute.For<ILogger<CreateVolunteerOpportunityCommandHandler>>();
	private readonly CreateVolunteerOpportunityCommandHandler _sut;

	public CreateVolunteerOpportunityCommandHandlerTests()
	{
		_sut = new CreateVolunteerOpportunityCommandHandler(_dbContext, _geocodingService, _logger);
	}

	[Test]
	public async Task Handle_ShouldCreateAndPersistOpportunity_WithCorrectData(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new CreateVolunteerOpportunityCommand(
			"Helpers needed",
			"For moving",
			TestOrganizationId,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Title.Should().Be("Helpers needed");
		result.Description.Should().Be("For moving");
		result.OrganizationId.Should().Be(TestOrganizationId);
		result.IsRemote.Should().BeFalse();
		result.Address.Should().NotBeNull();
		result.Address!.Street.Should().Be(TestAddress.Street);
		result.Occurrence.Should().Be(Occurrence.OneTime);
		result.ParticipationType.Should().Be(ParticipationType.Waitlist);
	}

	[Test]
	public async Task Handle_ShouldCallRepositoryAndUnitOfWork(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new CreateVolunteerOpportunityCommand(
			"Title",
			"Description",
			TestOrganizationId,
			false,
			TestAddress,
			Occurrence.Recurring,
			ParticipationType.IndividualContact);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext
			.VolunteerOpportunities
			.Received(1)
			.AddAsync(Arg.Any<VolunteerOpportunity>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldPersistCoordinates_WhenGeocodingSucceeds(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(new GeoCoordinates(52.52, 13.405));

		var command = new CreateVolunteerOpportunityCommand(
			"Title", "Description", TestOrganizationId, false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Address!.Latitude.Should().Be(52.52);
		result.Address!.Longitude.Should().Be(13.405);
	}

	[Test]
	public async Task Handle_ShouldSaveWithoutCoordinates_WhenGeocodingReturnsNull(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns((GeoCoordinates?)null);

		var command = new CreateVolunteerOpportunityCommand(
			"Title", "Description", TestOrganizationId, false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Address!.Latitude.Should().BeNull();
		result.Address!.Longitude.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldSaveWithoutCoordinates_WhenGeocodingThrows(
		CancellationToken cancellationToken)
	{
		// Arrange
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromException<GeoCoordinates?>(new HttpRequestException("boom")));

		var command = new CreateVolunteerOpportunityCommand(
			"Title", "Description", TestOrganizationId, false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Address!.Latitude.Should().BeNull();
	}

	[Test]
	public async Task Handle_ShouldNotGeocode_WhenRemote(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new CreateVolunteerOpportunityCommand(
			"Title", "Description", TestOrganizationId, true, null, Occurrence.OneTime, ParticipationType.Waitlist);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _geocodingService
			.DidNotReceive()
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}
}
