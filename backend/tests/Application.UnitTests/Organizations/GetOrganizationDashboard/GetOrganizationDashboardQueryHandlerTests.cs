using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.Organizations;
using Application.Organizations.GetOrganizationDashboard.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Users;
using NSubstitute;

namespace Application.UnitTests.Organizations.GetOrganizationDashboard;

public class GetOrganizationDashboardQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IOrganizationDashboardReadRepository _readRepository = Substitute.For<IOrganizationDashboardReadRepository>();
	private readonly IAggregateRepository<Organization, OrganizationId> _orgRepo =
		Substitute.For<IAggregateRepository<Organization, OrganizationId>>();
	private readonly GetOrganizationDashboardQueryHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOrganizationDashboardQueryHandlerTests()
	{
		_dbContext.Organizations.Returns(_orgRepo);
		_orgRepo
			.FindAsync(Arg.Any<OrganizationId>(), Arg.Any<CancellationToken>())
			.Returns(Organization.Create(OrganizationId.Create(DefaultOrgId).GetValueOrThrow(), "Sample Fire Department").Value);
		_dbContext
			.IsMemberAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_sut = new GetOrganizationDashboardQueryHandler(_dbContext, _readRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnNull_WhenOrganizationNotFound(
		CancellationToken cancellationToken)
	{
		_orgRepo.FindAsync(OrganizationId.Create(DefaultOrgId).GetValueOrThrow(), cancellationToken).Returns((Organization?)null);
		var query = new GetOrganizationDashboardQuery(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(query, cancellationToken);

		result.Should().BeNull();
		await _readRepository.DidNotReceive().GetKpisAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotAMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		_dbContext
			.IsMemberAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), cancellationToken)
			.Returns(false);
		var query = new GetOrganizationDashboardQuery(DefaultOrgId, DefaultRequestingUserId);

		var act = async () => await _sut.Handle(query, cancellationToken);

		await act.Should().ThrowAsync<ResultFailureException>().WithMessage("*permission*");
		await _readRepository.DidNotReceive().GetKpisAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldReturnKpis_WhenRequestingUserIsOrganizer(
		CancellationToken cancellationToken)
	{
		var kpis = new OrganizationDashboardResponse(
			PendingEngagements: 2,
			ConfirmedEngagementsTotal: 5);
		_readRepository.GetKpisAsync(DefaultOrgId, cancellationToken).Returns(kpis);
		var query = new GetOrganizationDashboardQuery(DefaultOrgId, DefaultRequestingUserId);

		var result = await _sut.Handle(query, cancellationToken);

		result.Should().Be(kpis);
		result!.ConfirmedEngagementsTotal.Should().Be(5);
	}
}
