using Application.Common.Exceptions;
using Application.Common.Geocoding;
using Application.Common.Persistence;
using Application.VolunteerOpportunities.GeocodeVolunteerOpportunityAddress.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.GeocodeVolunteerOpportunityAddress;

public sealed class GeocodeVolunteerOpportunityAddressHandlerTests : IDisposable
{
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Hauptstraße", "1", "12345", "Berlin").Value;

	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
	private readonly IGeocodingService _geocodingService = Substitute.For<IGeocodingService>();
	private readonly IPinGenerator _pinGenerator = Substitute.For<IPinGenerator>();
	private readonly MemoryCache _cache = new(new MemoryCacheOptions());
	private readonly GeocodeVolunteerOpportunityAddressHandler _sut;

	public GeocodeVolunteerOpportunityAddressHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_sut = new GeocodeVolunteerOpportunityAddressHandler(
			_dbContext, _unitOfWork, _geocodingService, _cache, NullLogger<GeocodeVolunteerOpportunityAddressHandler>.Instance);
	}

	private VolunteerOpportunity CreateNonRemoteOpportunity(Address? address = null) =>
		VolunteerOpportunity.Create(
			DefaultOrgId, "Titel", null, "Beschreibung", null, false, address ?? DefaultAddress, Occurrence.OneTime,
			ParticipationType.IndividualContact, CheckInMethod.None, _pinGenerator,
			validUntil: DateTimeOffset.UtcNow.AddDays(30)).Value;

	private void SetupOpportunity(VolunteerOpportunityId id, VolunteerOpportunity opportunity) =>
		_opportunityRepo.FindAsync(id, Arg.Any<CancellationToken>()).Returns(opportunity);

	[Test]
	public async Task Handle_ShouldApplyCoordinatesAndSave_WhenGeocodingFindsMatch(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateNonRemoteOpportunity();
		SetupOpportunity(opportunity.Id, opportunity);
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.Found(new GeoCoordinates(52.52, 13.405)));

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id), cancellationToken);

		opportunity.Address!.Latitude.Should().Be(52.52);
		opportunity.Address!.Longitude.Should().Be(13.405);
		opportunity.AddressGeocodingFailed.Should().BeFalse();
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldMarkAddressGeocodingFailedAndSave_WhenGeocodingReturnsNotFound(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateNonRemoteOpportunity();
		SetupOpportunity(opportunity.Id, opportunity);
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.NotFound);

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id), cancellationToken);

		opportunity.AddressGeocodingFailed.Should().BeTrue();
		opportunity.Address!.Latitude.Should().BeNull();
		await _unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldLeaveCoordinatesNullAndNotSave_WhenGeocodingIsTransientFailure(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateNonRemoteOpportunity();
		SetupOpportunity(opportunity.Id, opportunity);
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.TransientFailure);

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id), cancellationToken);

		// Assert: left for GeocodingRetryJob to backstop later - no save needed.
		opportunity.Address!.Latitude.Should().BeNull();
		opportunity.AddressGeocodingFailed.Should().BeFalse();
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotThrow_WhenGeocodingServiceThrows(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateNonRemoteOpportunity();
		SetupOpportunity(opportunity.Id, opportunity);
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromException<GeocodingResult>(new HttpRequestException("boom")));

		Func<Task> act = async () =>
			await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id), cancellationToken);

		// Assert: an exception here means we genuinely don't know the outcome -
		// never treat it as NotFound, and let GeocodingRetryJob retry later.
		await act.Should().NotThrowAsync();
		opportunity.Address!.Latitude.Should().BeNull();
		opportunity.AddressGeocodingFailed.Should().BeFalse();
		await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDoNothing_WhenOpportunityNoLongerExists(
		CancellationToken cancellationToken)
	{
		var missingId = VolunteerOpportunityId.New();
		_opportunityRepo.FindAsync(missingId, Arg.Any<CancellationToken>()).Returns((VolunteerOpportunity?)null);

		Func<Task> act = async () =>
			await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(missingId), cancellationToken);

		await act.Should().NotThrowAsync();
		await _geocodingService.DidNotReceive().GeocodeAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDoNothing_WhenOpportunityHasSinceGoneRemote(
		CancellationToken cancellationToken)
	{
		// Arrange: the address changed again (or went remote) before this event
		// was dispatched - a newer event supersedes this stale one.
		var opportunity = CreateNonRemoteOpportunity();
		opportunity.Relocate(true, null).ThrowIfFailure();
		SetupOpportunity(opportunity.Id, opportunity);

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id), cancellationToken);

		await _geocodingService.DidNotReceive().GeocodeAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDoNothing_WhenCoordinatesAlreadyResolved(
		CancellationToken cancellationToken)
	{
		// Arrange: an earlier attempt (or GeocodingRetryJob) already resolved this
		// opportunity before this event dispatched.
		var opportunity = CreateNonRemoteOpportunity();
		opportunity.ApplyGeocodingResult(DefaultAddress.WithCoordinates(1, 1).GetValueOrThrow());
		SetupOpportunity(opportunity.Id, opportunity);

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id), cancellationToken);

		await _geocodingService.DidNotReceive().GeocodeAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldDoNothing_WhenAddressAlreadyMarkedUnresolvable(
		CancellationToken cancellationToken)
	{
		var opportunity = CreateNonRemoteOpportunity();
		opportunity.MarkAddressGeocodingFailed();
		SetupOpportunity(opportunity.Id, opportunity);

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(opportunity.Id), cancellationToken);

		await _geocodingService.DidNotReceive().GeocodeAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotCallGeocodingServiceTwice_ForTwoOpportunitiesAtTheSameAddress(
		CancellationToken cancellationToken)
	{
		var first = CreateNonRemoteOpportunity();
		var second = CreateNonRemoteOpportunity();
		SetupOpportunity(first.Id, first);
		SetupOpportunity(second.Id, second);
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.Found(new GeoCoordinates(52.52, 13.405)));

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(first.Id), cancellationToken);
		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(second.Id), cancellationToken);

		second.Address!.Latitude.Should().Be(52.52);
		await _geocodingService.Received(1).GeocodeAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotCacheTransientFailure_SoARetryCanStillResolve(
		CancellationToken cancellationToken)
	{
		var first = CreateNonRemoteOpportunity();
		var second = CreateNonRemoteOpportunity();
		SetupOpportunity(first.Id, first);
		SetupOpportunity(second.Id, second);
		_geocodingService
			.GeocodeAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(GeocodingResult.TransientFailure);

		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(first.Id), cancellationToken);
		await _sut.Handle(new VolunteerOpportunityGeocodingRequestedDomainEvent(second.Id), cancellationToken);

		await _geocodingService.Received(2).GeocodeAsync(
			Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
	}

	public void Dispose() => _cache.Dispose();
}
