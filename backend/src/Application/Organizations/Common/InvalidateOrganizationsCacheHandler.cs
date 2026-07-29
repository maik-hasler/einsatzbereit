using Application.Common.Caching;
using Application.Common.Messaging;
using Domain.Organizations;

namespace Application.Organizations.Common;

internal sealed class InvalidateOrganizationsCacheHandler(ICacheInvalidator cacheInvalidator)
	: INotificationHandler<OrganizationCreatedDomainEvent>,
		INotificationHandler<OrganizationDeletedDomainEvent>,
		INotificationHandler<OrganizationRestoredDomainEvent>,
		INotificationHandler<OrganizationVerifiedDomainEvent>,
		INotificationHandler<OrganizationVerificationRevokedDomainEvent>
{
	public Task Handle(OrganizationCreatedDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	public Task Handle(OrganizationDeletedDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	public Task Handle(OrganizationRestoredDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	public Task Handle(OrganizationVerifiedDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	public Task Handle(OrganizationVerificationRevokedDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	private Task Invalidate()
	{
		cacheInvalidator.Invalidate(CacheCategory.Organizations);
		return Task.CompletedTask;
	}
}
