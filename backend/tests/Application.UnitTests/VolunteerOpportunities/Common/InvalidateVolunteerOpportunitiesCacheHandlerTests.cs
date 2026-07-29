using Application.Common.Caching;
using Application.VolunteerOpportunities.Common;
using AwesomeAssertions;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using NSubstitute;

namespace Application.UnitTests.VolunteerOpportunities.Common;

public class InvalidateVolunteerOpportunitiesCacheHandlerTests
{
	private readonly ICacheInvalidator _cacheInvalidator = Substitute.For<ICacheInvalidator>();
	private readonly InvalidateVolunteerOpportunitiesCacheHandler _sut;

	public InvalidateVolunteerOpportunitiesCacheHandlerTests()
	{
		_sut = new InvalidateVolunteerOpportunitiesCacheHandler(_cacheInvalidator);
	}

	[Test]
	public async Task Handle_ShouldInvalidateVolunteerOpportunitiesCategory_WhenOpportunityCreated(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new VolunteerOpportunityCreatedDomainEvent(VolunteerOpportunityId.New(), OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.VolunteerOpportunities);
	}

	[Test]
	public async Task Handle_ShouldInvalidateVolunteerOpportunitiesCategory_WhenOpportunityPublished(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new VolunteerOpportunityPublishedDomainEvent(VolunteerOpportunityId.New(), OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.VolunteerOpportunities);
	}

	[Test]
	public async Task Handle_ShouldInvalidateVolunteerOpportunitiesCategory_WhenOpportunityDeleted(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new VolunteerOpportunityDeletedDomainEvent(VolunteerOpportunityId.New(), OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.VolunteerOpportunities);
	}

	[Test]
	public async Task Handle_ShouldInvalidateVolunteerOpportunitiesCategory_WhenOpportunityRestored(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new VolunteerOpportunityRestoredDomainEvent(VolunteerOpportunityId.New(), OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.VolunteerOpportunities);
	}
}
