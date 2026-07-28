using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Pagination;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Engagements.GetEngagements.v1;
using AwesomeAssertions;
using Domain.Common;
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
			.GetPagedByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 0, 1, 10));
		_keycloakUserService
			.GetDisplayNamesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, string>());
		_sut = new GetEngagementsQueryHandler(_readRepository, _dbContext, _keycloakUserService);
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
	public async Task Handle_ShouldEnrichVolunteerName_FromDisplayNameMap(
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
			.GetPagedByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([engagement], 1, 1, 10));
		_keycloakUserService
			.GetDisplayNamesAsync(Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
			.Returns(new Dictionary<Guid, string> { [volunteerId] = "Vera Volunteer" });

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 1, 10);

		var result = await _sut.Handle(query, cancellationToken);

		result.Items.Should().ContainSingle(e => e.VolunteerName == "Vera Volunteer");
	}

	[Test]
	public async Task Handle_ShouldPreserveTotalItemsAndPageCount_FromRepository(
		CancellationToken cancellationToken)
	{
		_readRepository
			.GetPagedByOpportunityAsync(Arg.Any<VolunteerOpportunityId>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
			.Returns(new PagedList<EngagementSummary>([], 25, 2, 10));

		var query = new GetEngagementsQuery(DefaultOpportunityId, DefaultRequestingUserId, 2, 10);

		var result = await _sut.Handle(query, cancellationToken);

		result.TotalItems.Should().Be(25);
		result.CurrentPage.Should().Be(2);
		result.PageCount.Should().Be(3);
	}

	private static VolunteerOpportunity CreateDefaultOpportunity() =>
		VolunteerOpportunity.Create(DefaultOrgId, "Test", "Test", false, DefaultAddress, Occurrence.OneTime, ParticipationType.Waitlist, CheckInMethod.None, Substitute.For<IPinGenerator>(), status: OpportunityStatus.Draft).Value;
}
