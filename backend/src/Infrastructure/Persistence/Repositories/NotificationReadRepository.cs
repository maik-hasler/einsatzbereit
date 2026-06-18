using Application.Notifications;
using Domain.Notifications;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class NotificationReadRepository(
	ApplicationDbContext dbContext)
	: INotificationReadRepository
{
	private static readonly NotificationKind[] EngagementKinds =
	[
		NotificationKind.EngagementCreated,
		NotificationKind.EngagementConfirmed,
		NotificationKind.EngagementCancelled,
		NotificationKind.EngagementWithdrawn,
	];

	public async ValueTask<List<NotificationSummary>> GetByRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default)
	{
		var notifications = await dbContext.NotificationsQuery
			.Where(n => n.RecipientId == recipientId)
			.OrderByDescending(n => n.CreatedOn)
			.ToListAsync(cancellationToken);

		// Collect entity IDs by type
		var engagementIds = notifications
			.Where(n => EngagementKinds.Contains(n.Kind))
			.Select(n => n.RelatedEntityId)
			.ToHashSet();

		var directOpportunityIds = notifications
			.Where(n => !EngagementKinds.Contains(n.Kind))
			.Select(n => n.RelatedEntityId)
			.ToHashSet();

		// Batch-fetch engagements and compute opportunity IDs from them
		Dictionary<Guid, Guid> engagementToOpportunity = [];
		if (engagementIds.Count > 0)
		{
			engagementToOpportunity = await dbContext.EngagementsQuery
				.Where(e => engagementIds.Contains(e.Id.Value))
				.Select(e => new { EngId = e.Id.Value, OppId = e.OpportunityId.Value })
				.ToDictionaryAsync(x => x.EngId, x => x.OppId, cancellationToken);
		}

		var opportunityIdsFromEngagements = engagementToOpportunity.Values.ToHashSet();
		var allOpportunityIds = opportunityIdsFromEngagements.Union(directOpportunityIds).ToHashSet();

		// Batch-fetch opportunity titles
		Dictionary<Guid, string> opportunityTitles = [];
		if (allOpportunityIds.Count > 0)
		{
			opportunityTitles = await dbContext.VolunteerOpportunitiesQuery
				.Where(o => allOpportunityIds.Contains(o.Id.Value))
				.Select(o => new { Id = o.Id.Value, o.Title })
				.ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);
		}

		return notifications.Select(n =>
		{
			string? relatedTitle = null;
			string? actionUrl = null;

			if (EngagementKinds.Contains(n.Kind) &&
				engagementToOpportunity.TryGetValue(n.RelatedEntityId, out var opportunityId))
			{
				opportunityTitles.TryGetValue(opportunityId, out relatedTitle);

				actionUrl = n.Kind is NotificationKind.EngagementCreated or NotificationKind.EngagementWithdrawn
					? $"/volunteer-opportunities/{opportunityId}/engagements"
					: "/my-engagements";
			}
			else if (!EngagementKinds.Contains(n.Kind))
			{
				opportunityTitles.TryGetValue(n.RelatedEntityId, out relatedTitle);

				actionUrl = n.Kind == NotificationKind.OpportunityUpdated
					? $"/volunteer-opportunities/{n.RelatedEntityId}"
					: "/my-engagements";
			}

			return new NotificationSummary(
				n.Id.Value,
				n.Kind.ToString(),
				n.RelatedEntityId,
				relatedTitle,
				actionUrl,
				n.IsRead,
				n.CreatedOn);
		}).ToList();
	}
}
