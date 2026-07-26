using Application.Common.Exceptions;
using Application.Common.Geocoding;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.CreateVolunteerOpportunity;

public class CreateVolunteerOpportunityCommandHandlerTests
{
	private static readonly OrganizationId TestOrganizationId = OrganizationId.New();
	private static readonly Address TestAddress = Address.Create("Sample Street", "1", "12345", "Berlin").Value;
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CreateVolunteerOpportunityCommandHandler _sut;

	public CreateVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new CreateVolunteerOpportunityCommandHandler(
			_dbContext,
			_geocodingService,
			_pinGenerator,
			NullLogger<CreateVolunteerOpportunityCommandHandler>.Instance);
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
			ParticipationType.Waitlist,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Draft,
			DefaultRequestingUserId);

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
	public async Task Handle_ShouldPersistEmptyTitle_WhenDraftAndTitleOmitted(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new CreateVolunteerOpportunityCommand(
			string.Empty,
			"For moving",
			TestOrganizationId,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Draft,
			DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Title.Should().Be(string.Empty);
	}

	[Test]
	public async Task Handle_ShouldUseGivenCheckInPin(
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
			ParticipationType.Waitlist,
			CheckInMethod.PINCode,
			null,
			[],
			OpportunityStatus.Draft,
			DefaultRequestingUserId,
			CheckInPin: "13579");

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.CheckInPin.Should().Be("13579");
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
			ParticipationType.IndividualContact,
			CheckInMethod.Manual,
			null,
			[],
			OpportunityStatus.Published,
			DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext
			.VolunteerOpportunities
			.Received(1)
			.AddAsync(Arg.Any<VolunteerOpportunity>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenPublishingWaitlistDirectlyWithNoTimeSlots(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new CreateVolunteerOpportunityCommand(
			"Title",
			"Description",
			TestOrganizationId,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Published,
			DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Waitlist opportunity*");
		await _dbContext
			.VolunteerOpportunities
			.DidNotReceive()
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
			"Title", "Description", TestOrganizationId, false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], OpportunityStatus.Draft, DefaultRequestingUserId);

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
			"Title", "Description", TestOrganizationId, false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], OpportunityStatus.Draft, DefaultRequestingUserId);

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
			"Title", "Description", TestOrganizationId, false, TestAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], OpportunityStatus.Draft, DefaultRequestingUserId);

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
			"Title", "Description", TestOrganizationId, true, null, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, null, [], OpportunityStatus.Draft, DefaultRequestingUserId);

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _geocodingService
			.DidNotReceive()
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller is not an organizer of the target organization.
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var command = new CreateVolunteerOpportunityCommand(
			"Title",
			"Description",
			TestOrganizationId,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.Waitlist,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Draft,
			DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _dbContext
			.VolunteerOpportunities
			.DidNotReceive()
			.AddAsync(Arg.Any<VolunteerOpportunity>(), Arg.Any<CancellationToken>());
	}
}
