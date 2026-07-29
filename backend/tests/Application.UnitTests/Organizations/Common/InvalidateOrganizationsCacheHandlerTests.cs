using Application.Common.Caching;
using Application.Organizations.Common;
using AwesomeAssertions;
using Domain.Organizations;
using NSubstitute;

namespace Application.UnitTests.Organizations.Common;

public class InvalidateOrganizationsCacheHandlerTests
{
	private readonly ICacheInvalidator _cacheInvalidator = Substitute.For<ICacheInvalidator>();
	private readonly InvalidateOrganizationsCacheHandler _sut;

	public InvalidateOrganizationsCacheHandlerTests()
	{
		_sut = new InvalidateOrganizationsCacheHandler(_cacheInvalidator);
	}

	[Test]
	public async Task Handle_ShouldInvalidateOrganizationsCategory_WhenOrganizationCreated(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new OrganizationCreatedDomainEvent(OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.Organizations);
	}

	[Test]
	public async Task Handle_ShouldInvalidateOrganizationsCategory_WhenOrganizationDeleted(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new OrganizationDeletedDomainEvent(OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.Organizations);
	}

	[Test]
	public async Task Handle_ShouldInvalidateOrganizationsCategory_WhenOrganizationRestored(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new OrganizationRestoredDomainEvent(OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.Organizations);
	}

	[Test]
	public async Task Handle_ShouldInvalidateOrganizationsCategory_WhenOrganizationVerified(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new OrganizationVerifiedDomainEvent(OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.Organizations);
	}

	[Test]
	public async Task Handle_ShouldInvalidateOrganizationsCategory_WhenOrganizationVerificationRevoked(
		CancellationToken cancellationToken)
	{
		await _sut.Handle(new OrganizationVerificationRevokedDomainEvent(OrganizationId.New()), cancellationToken);

		_cacheInvalidator.Received(1).Invalidate(CacheCategory.Organizations);
	}
}
