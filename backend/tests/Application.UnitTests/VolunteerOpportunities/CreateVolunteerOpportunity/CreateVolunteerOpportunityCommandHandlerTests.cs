using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.CreateVolunteerOpportunity.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.CreateVolunteerOpportunity;

public class CreateVolunteerOpportunityCommandHandlerTests
{
	private static readonly OrganizationId TestOrganizationId = OrganizationId.New();
	private static readonly Address TestAddress = Address.Create("Sample Street", "1", "12345", "Berlin").Value;
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly CreateVolunteerOpportunityCommandHandler _sut;

	public CreateVolunteerOpportunityCommandHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new CreateVolunteerOpportunityCommandHandler(
			_dbContext,
			_pinGenerator);
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
			ParticipationType.ScheduledSlots,
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
		result.ParticipationType.Should().Be(ParticipationType.ScheduledSlots);
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
			ParticipationType.ScheduledSlots,
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
			ParticipationType.ScheduledSlots,
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
			DefaultRequestingUserId,
			ValidUntil: DateTimeOffset.UtcNow.AddDays(30));

		// Act
		await _sut.Handle(command, cancellationToken);

		// Assert
		await _dbContext
			.VolunteerOpportunities
			.Received(1)
			.AddAsync(Arg.Any<VolunteerOpportunity>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenPublishingScheduledSlotsDirectlyWithNoTimeSlots(
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
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Published,
			DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Scheduled slots opportunity*");
		await _dbContext
			.VolunteerOpportunities
			.DidNotReceive()
			.AddAsync(Arg.Any<VolunteerOpportunity>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldPersistValidUntil_ForIndividualContact(
		CancellationToken cancellationToken)
	{
		// Arrange
		var validUntil = DateTimeOffset.UtcNow.AddDays(14);
		var command = new CreateVolunteerOpportunityCommand(
			"Title",
			"Description",
			TestOrganizationId,
			false,
			TestAddress,
			Occurrence.OneTime,
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Draft,
			DefaultRequestingUserId,
			ValidUntil: validUntil);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.ValidUntil.Should().Be(validUntil);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenPublishingIndividualContactDirectlyWithNoValidUntil(
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
			ParticipationType.IndividualContact,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Published,
			DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*Individual contact opportunity must have a deadline*");
		await _dbContext
			.VolunteerOpportunities
			.DidNotReceive()
			.AddAsync(Arg.Any<VolunteerOpportunity>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenValidUntilGiven_ForScheduledSlots(
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
			ParticipationType.ScheduledSlots,
			CheckInMethod.None,
			null,
			[],
			OpportunityStatus.Draft,
			DefaultRequestingUserId,
			ValidUntil: DateTimeOffset.UtcNow.AddDays(14));

		// Act
		Func<Task> act = async () => await _sut.Handle(command, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<ResultFailureException>()
			.WithMessage("*deadline can only be set for Individual contact*");
	}

	[Test]
	public async Task Handle_ShouldSaveWithNullCoordinates_AndRaiseGeocodingRequestedEvent_ForNonRemoteAddress(
		CancellationToken cancellationToken)
	{
		// Arrange: geocoding itself now happens out of band (see
		// GeocodeVolunteerOpportunityAddressHandler) - Create only needs to
		// persist with null coordinates and raise the event that triggers it.
		var command = new CreateVolunteerOpportunityCommand(
			"Title", "Description", TestOrganizationId, false, TestAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, null, [], OpportunityStatus.Draft, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Address!.Latitude.Should().BeNull();
		result.Address!.Longitude.Should().BeNull();
		result.Events.OfType<VolunteerOpportunityGeocodingRequestedDomainEvent>()
			.Should().ContainSingle()
			.Which.OpportunityId.Should().Be(result.Id);
	}

	[Test]
	public async Task Handle_ShouldNotRaiseGeocodingRequestedEvent_WhenRemote(
		CancellationToken cancellationToken)
	{
		// Arrange
		var command = new CreateVolunteerOpportunityCommand(
			"Title", "Description", TestOrganizationId, true, null, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, null, [], OpportunityStatus.Draft, DefaultRequestingUserId);

		// Act
		var result = await _sut.Handle(command, cancellationToken);

		// Assert
		result.Events.Should().NotContain(e => e is VolunteerOpportunityGeocodingRequestedDomainEvent);
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
			ParticipationType.ScheduledSlots,
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
