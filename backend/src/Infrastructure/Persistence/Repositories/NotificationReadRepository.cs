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
		DateTimeOffset? before,
		Guid? beforeId,
		int limit,
		CancellationToken cancellationToken = default)
	{
		var recipientQuery = dbContext.NotificationsQuery
			.Where(n => n.RecipientId == recipientId);

		// Notifications created in the same batch (e.g. several engagement
		// events processed by the outbox job in one tick) can share an
		// identical CreatedOn - AuditableEntityInterceptor stamps one UtcNow
		// per SaveChanges call, not per entity. CreatedOn alone is therefore
		// not a safe keyset cursor: paging strictly on "< before" would drop
		// same-timestamp siblings that land on the far side of a page
		// boundary. NotificationId's value converter only translates == (not
		// < / >, which isn't defined on the value object at all, and
		// EF.Property<Guid> on a converted property isn't a supported read
		// of the raw column either - it 500'd against real Postgres despite
		// compiling fine), so the tie is broken by Id (a UUIDv7, still
		// roughly time-ordered) in memory instead: fetch the *complete*
		// same-timestamp bucket via a plain equality query (cheap - ties are
		// bounded by how many notifications one SaveChanges call creates for
		// a single recipient, normally a handful) rather than risk an
		// arbitrary SQL tie order silently truncating it via Take().
		List<Notification> tiedWithCursor = [];
		if (before is not null && beforeId is not null)
		{
			var cursorTiedBucket = await recipientQuery
				.Where(n => n.CreatedOn == before.Value)
				.ToListAsync(cancellationToken);

			tiedWithCursor = cursorTiedBucket
				.Where(n => n.Id.Value.CompareTo(beforeId.Value) < 0)
				.OrderByDescending(n => n.Id.Value)
				.ToList();
		}

		var olderQuery = before is not null
			? recipientQuery.Where(n => n.CreatedOn < before.Value)
			: recipientQuery;

		var older = await olderQuery
			.OrderByDescending(n => n.CreatedOn)
			.Take(limit)
			.ToListAsync(cancellationToken);

		// SQL only orders "older" by CreatedOn (see above - no safe way to add
		// Id as a second ORDER BY key without the same translation risk), so
		// same-timestamp siblings within it can come back in an arbitrary
		// sub-order. Re-sort the combined, already-materialized batch in
		// memory (CreatedOn desc, then Id desc) so the array this method
		// returns always has a single, deterministic tie-break convention -
		// the same one `tiedWithCursor` above already used - regardless of
		// what order Postgres happened to hand ties back in.
		var notifications = tiedWithCursor
			.Concat(older)
			.OrderByDescending(n => n.CreatedOn)
			.ThenByDescending(n => n.Id.Value)
			.Take(limit)
			.ToList();

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
						? $"/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements"
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

	public async ValueTask<int> CountUnreadByRecipientAsync(
		UserId recipientId,
		CancellationToken cancellationToken = default) =>
		await dbContext.NotificationsQuery
			.CountAsync(n => n.RecipientId == recipientId && !n.IsRead, cancellationToken);
}
