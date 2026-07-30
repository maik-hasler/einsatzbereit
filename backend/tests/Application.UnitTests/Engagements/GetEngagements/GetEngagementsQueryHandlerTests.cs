using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Pagination;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Engagements.GetEngagements.v1;
using AwesomeAssertions;
using Domain.Common;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.Engagements.GetEngagements;

public class GetEngagementsQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId> _opportunityRepo =
		Substitute.For<IAggregateRepository<VolunteerOpportunity, VolunteerOpportunityId>>();
	private readonly IEngagementReadRepository _readRepository = Substitute.For<IEngagementReadRepository>();
	private readonly IKeycloakUserService _keycloakUserService = Substitute.For<IKeycloakUserService>();
	private readonly IKeycloakOrganizationService _keycloakOrganizationService = Substitute.For<IKeycloakOrganizationService>();
	private readonly GetEngagementsQueryHandler _sut;

	private static readonly UserId DefaultRequestingUserId = UserId.New();
	private static readonly OrganizationId DefaultOrgId = OrganizationId.New();
	private static readonly Address DefaultAddress = Address.Create("Teststraße", "1", "12345", "Berlin").Value;
	private static readonly VolunteerOpportunityId DefaultOpportunityId = VolunteerOpportunityId.New();

	public GetEngagementsQueryHandlerTests()
	{
		_dbContext.VolunteerOpportunities.Returns(_opportunityRepo);
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns(CreateDefaultOpportunity());
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 0, 1, 10));
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>());
		_sut = new GetEngagementsQueryHandler(_readRepository, _dbContext, _keycloakUserService, _keycloakOrganizationService);
	}

	private async Task<(int PageNumber, int PageSize)> CapturedArgsAsync(int pageNumber, int pageSize)
	{
		var capturedPageNumber = 0;
		var capturedPageSize = 0;
		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Do<int>(p => capturedPageNumber = p),
				Arg.Do<int>(s => capturedPageSize = s),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 0, 1, 10));

		await _sut.Handle(new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, pageNumber, pageSize), CancellationToken.None);

		return (capturedPageNumber, capturedPageSize);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageNumber_ToOne()
	{
		var (pageNumber, _) = await CapturedArgsAsync(pageNumber: 0, pageSize: 10);
		pageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldClampNegativePageNumber_ToOne()
	{
		var (pageNumber, _) = await CapturedArgsAsync(pageNumber: -5, pageSize: 10);
		pageNumber.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldClampZeroPageSize_ToOne()
	{
		var (_, pageSize) = await CapturedArgsAsync(pageNumber: 1, pageSize: 0);
		pageSize.Should().Be(1);
	}

	[Test]
	public async Task Handle_ShouldCapExcessivePageSize_ToHundred()
	{
		var (_, pageSize) = await CapturedArgsAsync(pageNumber: 1, pageSize: 5000);
		pageSize.Should().Be(100);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenOpportunityNotFound(
		CancellationToken cancellationToken)
	{
		_opportunityRepo
			.FindAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<CancellationToken>())
			.Returns((VolunteerOpportunity?)null);

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 1, 10);

		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.NotFound);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 1, 10);

		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
	}

	[Test]
	public async Task Handle_ShouldEnrichVolunteerNameAndEmail_FromKeycloakProfile(
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			volunteerId,
			null,
			"Ready to help",
			"Pending",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow,
			VolunteerPhone: "+49 30 1234567");

		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([engagement], 1, 1, 10));
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>
			{
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", "Vera", "Volunteer", "vera@example.com"),
			});

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 1, 10);

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().ContainSingle(e =>
			e.VolunteerName == "Vera Volunteer"
			&& e.VolunteerEmail == "vera@example.com"
			&& e.VolunteerPhone == "+49 30 1234567");
	}

	[Test]
	public async Task Handle_ShouldFallBackToUsername_WhenKeycloakProfileHasNoName(
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			volunteerId,
			null,
			"Ready to help",
			"Pending",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow);

		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([engagement], 1, 1, 10));
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>
			{
				[volunteerId] = new KeycloakUserProfile(volunteerId, "vera", null, null, "vera@example.com"),
			});

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 1, 10);

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().ContainSingle(e => e.VolunteerName == "vera");
	}

	[Test]
	public async Task Handle_ShouldLeaveVolunteerNameAndEmailNull_WhenKeycloakLookupFailsForVolunteer(
		CancellationToken cancellationToken)
	{
		var volunteerId = Guid.NewGuid();
		var engagement = new EngagementSummary(
			Guid.NewGuid(),
			DefaultOpportunityId.Value,
			"Test Opportunity",
			DefaultOrgId.Value,
			"Test Org",
			volunteerId,
			null,
			"Ready to help",
			"Pending",
			IsCheckedIn: false,
			HasFeedback: false,
			DateTimeOffset.UtcNow,
			VolunteerPhone: "+49 30 1234567");

		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([engagement], 1, 1, 10));
		// GetUserProfilesAsync swallows individual Keycloak lookup failures (deleted user,
		// transient error) and simply omits that id from the map - this volunteer's entry
		// must fall back to whatever the repository already returned (VolunteerPhone stays
		// intact; VolunteerName/VolunteerEmail stay null) rather than throwing.
		_keycloakUserService
			.GetUserProfilesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, KeycloakUserProfile>());

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 1, 10);

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().ContainSingle(e =>
			e.VolunteerName == null
			&& e.VolunteerEmail == null
			&& e.VolunteerPhone == "+49 30 1234567");
	}

	[Test]
	public async Task Handle_ShouldPreserveTotalItemsAndPageCount_FromRepository(
		CancellationToken cancellationToken)
	{
		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 25, 2, 10));

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 2, 10);

		var result = await _sut.Handle(query, cancellationToken);

		result.TotalItems.Should().Be(25);
		result.CurrentPage.Should().Be(2);
		result.PageCount.Should().Be(3);
	}

	[Test]
	public async Task Handle_ShouldPassStatusAndTimeSlotFilters_ToRepository(
		CancellationToken cancellationToken)
	{
		var timeSlotId = TimeSlotId.New();
		EngagementStatus? capturedStatus = null;
		TimeSlotId? capturedTimeSlotId = null;
		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Do<EngagementStatus?>(s => capturedStatus = s),
				Arg.Do<TimeSlotId?>(t => capturedTimeSlotId = t),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 0, 1, 10));

		var query = new GetEngagementsQuery(
			DefaultOpportunityId, DefaultRequestingUserId, 1, 10, EngagementStatus.Confirmed, timeSlotId);

		await _sut.Handle(query, cancellationToken);

		capturedStatus.Should().Be(EngagementStatus.Confirmed);
		capturedTimeSlotId.Should().Be(timeSlotId);
	}

	[Test]
	public async Task Handle_ShouldResolveSearch_ToMatchingVolunteerIds_ViaKeycloak(
		CancellationToken cancellationToken)
	{
		var matchedUserId = Guid.NewGuid();
		_keycloakOrganizationService
			.SearchUsersAsync("Vera", Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([new KeycloakOrganizationMember(matchedUserId, "vera", "Vera", "Volunteer", "vera@example.com", false)]);

		IReadOnlyList<Guid>? capturedVolunteerIds = null;
		_readRepository
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Do<IReadOnlyList<Guid>?>(ids => capturedVolunteerIds = ids),
				Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 0, 1, 10));

		var query = new GetEngagementsQuery(
			DefaultOpportunityId, DefaultRequestingUserId, 1, 10, Search: "Vera");

		await _sut.Handle(query, cancellationToken);

		capturedVolunteerIds.Should().ContainSingle(id => id == matchedUserId);
	}

	[Test]
	public async Task Handle_ShouldReturnEmptyPage_WithoutQueryingRepository_WhenSearchMatchesNoVolunteer(
		CancellationToken cancellationToken)
	{
		_keycloakOrganizationService
			.SearchUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns([]);

		var query = new GetEngagementsQuery(
			DefaultOpportunityId, DefaultRequestingUserId, 1, 10, Search: "nobody-matches-this");

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().BeEmpty();
		result.TotalItems.Should().Be(0);
		await _readRepository
			.DidNotReceive()
			.GetPagedByOpportunityAsync(
				Arg.Any<VolunteerOpportunityId>(),
				Arg.Any<int>(),
				Arg.Any<int>(),
				Arg.Any<EngagementStatus?>(),
				Arg.Any<TimeSlotId?>(),
				Arg.Any<IReadOnlyList<Guid>?>(),
				Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotCallKeycloakSearch_WhenSearchIsNullOrWhitespace(
		CancellationToken cancellationToken)
	{
		var query = new GetEngagementsQuery(
			DefaultOpportunityId, DefaultRequestingUserId, 1, 10, Search: "   ");

		await _sut.Handle(query, cancellationToken);

		await _keycloakOrganizationService
			.DidNotReceive()
			.SearchUsersAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
	}

	private static VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.ScheduledSlots, CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Draft).Value;
}
