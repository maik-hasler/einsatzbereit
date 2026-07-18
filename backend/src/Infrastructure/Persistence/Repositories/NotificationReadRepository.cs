using Application.Common.Exceptions;
using Application.Notifications;
using Domain.Engagements;
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
			var engagementIdVOs = engagementIds.Select(id => EngagementId.Create(id).GetValueOrThrow()).ToList();
			var engagementRows = await dbContext.EngagementsQuery
				.Where(e => engagementIdVOs.Contains(e.Id))
				.Select(e => new { e.Id, e.OpportunityId })
				.ToListAsync(cancellationToken);
			engagementToOpportunity = engagementRows.ToDictionary(x => x.Id.Value, x => x.OpportunityId.Value);
		}

		var opportunityIdsFromEngagements = engagementToOpportunity.Values.ToHashSet();
		var allOpportunityIds = opportunityIdsFromEngagements.Union(directOpportunityIds).ToHashSet();

		// Batch-fetch opportunity titles and their owning organization (the
		// latter is needed to build the org-app deep link below; both are
		// null for a since-deleted opportunity).
		Dictionary<Guid, string> opportunityTitles = [];
		Dictionary<Guid, Guid> opportunityOrganizations = [];
		if (allOpportunityIds.Count > 0)
		{
			var opportunityIdVOs = allOpportunityIds.Select(id => VolunteerOpportunityId.Create(id).GetValueOrThrow()).ToList();
			var opportunityRows = await dbContext.VolunteerOpportunitiesQuery
				.Where(o => opportunityIdVOs.Contains(o.Id))
				.Select(o => new { o.Id, o.Title, o.OrganizationId })
				.ToListAsync(cancellationToken);
			opportunityTitles = opportunityRows.ToDictionary(x => x.Id.Value, x => x.Title);
			opportunityOrganizations = opportunityRows.ToDictionary(x => x.Id.Value, x => x.OrganizationId.Value);
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
					? (opportunityOrganizations.TryGetValue(opportunityId, out var organizationId)
						? $"/app/{organizationId}/opportunities/{opportunityId}/engagements"
						: null)
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
