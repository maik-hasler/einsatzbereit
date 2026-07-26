using Application.Common.Exceptions;
using Application.Common.Persistence;
using Application.VolunteerOpportunities;
using Application.VolunteerOpportunities.GetOrganizationOpportunities.v1;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.GetOrganizationOpportunities;

public class GetOrganizationOpportunitiesQueryHandlerTests
{
	private readonly IApplicationDbContext _dbContext = Substitute.For<IApplicationDbContext>();
	private readonly IVolunteerOpportunityReadRepository _readRepository = Substitute.For<IVolunteerOpportunityReadRepository>();
	private readonly GetOrganizationOpportunitiesQueryHandler _sut;

	private static readonly Guid DefaultOrgId = Guid.NewGuid();
	private static readonly UserId DefaultRequestingUserId = UserId.New();

	public GetOrganizationOpportunitiesQueryHandlerTests()
	{
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(true);
		_readRepository
			.GetSummariesByOrganizationAsync(Arg.Any<Guid>(), Arg.Any<OpportunityStatus?>(), Arg.Any<CancellationToken>())
			.Returns((IReadOnlyList<VolunteerOpportunitySummary>)[]);
		_sut = new GetOrganizationOpportunitiesQueryHandler(_readRepository, _dbContext);
	}

	[Test]
	public async Task Handle_ShouldReturnAllStatuses_WhenOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange
		var query = new GetOrganizationOpportunitiesQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		await _sut.Handle(query, cancellationToken);

		// Assert - the organizer's management view returns every status, not just Published.
		await _readRepository.Received(1).GetSummariesByOrganizationAsync(DefaultOrgId, null, cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldThrow_WhenRequestingUserIsNotOrganizer(
		CancellationToken cancellationToken)
	{
		// Arrange: caller is not an organizer of the target organization.
		_dbContext
			.IsOrganizerAsync(Arg.Any<OrganizationId>(), Arg.Any<UserId>(), Arg.Any<CancellationToken>())
			.Returns(false);

		var query = new GetOrganizationOpportunitiesQuery(DefaultOrgId, DefaultRequestingUserId);

		// Act
		Func<Task> act = async () => await _sut.Handle(query, cancellationToken);

		// Assert
		(await act.Should().ThrowAsync<ResultFailureException>())
			.Which.Error.Type.Should().Be(ErrorType.Forbidden);
		await _readRepository.DidNotReceive().GetSummariesByOrganizationAsync(
			Arg.Any<Guid>(), Arg.Any<OpportunityStatus?>(), Arg.Any<CancellationToken>());
	}
}
