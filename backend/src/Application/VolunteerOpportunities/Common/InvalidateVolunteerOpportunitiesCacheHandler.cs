using Application.Common.Caching;
using Application.Common.Messaging;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.Common;

internal sealed class InvalidateVolunteerOpportunitiesCacheHandler(ICacheInvalidator cacheInvalidator)
	: INotificationHandler<VolunteerOpportunityCreatedDomainEvent>,
		INotificationHandler<VolunteerOpportunityPublishedDomainEvent>,
		INotificationHandler<VolunteerOpportunityDeletedDomainEvent>,
		INotificationHandler<VolunteerOpportunityRestoredDomainEvent>
{
	public Task Handle(VolunteerOpportunityCreatedDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	public Task Handle(VolunteerOpportunityPublishedDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	public Task Handle(VolunteerOpportunityDeletedDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	public Task Handle(VolunteerOpportunityRestoredDomainEvent notification, CancellationToken cancellationToken) => Invalidate();

	private Task Invalidate()
	{
		cacheInvalidator.Invalidate(CacheCategory.VolunteerOpportunities);
		return Task.CompletedTask;
	}
}
