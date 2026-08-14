using Application.AuditLogs;
using Application.AuditLogs.ListAuditLogs.v1;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Pagination;
using Domain.AuditLogs;
using Domain.Engagements;
using Domain.Organizations;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AdminAuditLogReadRepository(
	ApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IAdminAuditLogReadRepository
{
	public async ValueTask<PagedList<AuditLogEntry>> GetAuditLogsPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var totalItems = await dbContext.AuditLogsQuery.CountAsync(cancellationToken);

		var page = await dbContext.AuditLogsQuery
			.OrderByDescending(a => a.CreatedOn)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		// An Engagement subject has no name of its own (#1837) - the closest
		// recognizable label for an admin is the opportunity it was a sign-up
		// for, so its opportunity id is resolved here and folded into the
		// VolunteerOpportunity lookup below.
		var engagementIds = page
			.Where(a => a.SubjectType == AuditSubjectType.Engagement)
			.Select(a => a.SubjectId)
			.Distinct()
			.Select(id => EngagementId.Create(id).GetValueOrThrow())
			.ToList();
		var engagementOpportunityIds = engagementIds.Count > 0
			? await dbContext.EngagementsQuery
				.Where(e => engagementIds.Contains(e.Id))
				.ToDictionaryAsync(e => e.Id.Value, e => e.OpportunityId.Value, cancellationToken)
			: new Dictionary<Guid, Guid>();

		var opportunityIds = page
			.Where(a => a.SubjectType == AuditSubjectType.VolunteerOpportunity)
			.Select(a => a.SubjectId)
			.Concat(engagementOpportunityIds.Values)
			.Distinct()
			.Select(id => VolunteerOpportunityId.Create(id).GetValueOrThrow())
			.ToList();
		var opportunityTitles = opportunityIds.Count > 0
			// IgnoreQueryFilters: a shadow-deleted opportunity's audit trail
			// (e.g. its own AdminShadowDeleted entry) should still resolve a name.
			? await dbContext.VolunteerOpportunitiesQuery
				.IgnoreQueryFilters()
				.Where(vo => opportunityIds.Contains(vo.Id))
				.ToDictionaryAsync(vo => vo.Id.Value, vo => vo.Title, cancellationToken)
			: new Dictionary<Guid, string>();

		var organizationIds = page
			.Where(a => a.SubjectType == AuditSubjectType.Organization)
			.Select(a => a.SubjectId)
			.Distinct()
			.Select(id => OrganizationId.Create(id).GetValueOrThrow())
			.ToList();
		var organizationNames = organizationIds.Count > 0
			? await dbContext.OrganizationsQuery
				.IgnoreQueryFilters()
				.Where(o => organizationIds.Contains(o.Id))
				.ToDictionaryAsync(o => o.Id.Value, o => o.Name, cancellationToken)
			: new Dictionary<Guid, string>();

		// Actor and User-subject ids share one Keycloak lookup - the two sets
		// often overlap (e.g. paging through several actions by the same admin).
		var userIds = page
			.Select(a => a.ActorUserId.Value)
			.Concat(page.Where(a => a.SubjectType == AuditSubjectType.User).Select(a => a.SubjectId))
			.Distinct()
			.ToList();
		var userDisplayNames = userIds.Count > 0
			? await keycloakUserService.GetDisplayNamesAsync(userIds, cancellationToken)
			: new Dictionary<Guid, string>();

		var items = page
			.Select(a => new AuditLogEntry(
				a.Id.Value,
				a.ActorUserId.Value,
				userDisplayNames.GetValueOrDefault(a.ActorUserId.Value, string.Empty),
				a.ActionType.ToString(),
				a.SubjectType.ToString(),
				a.SubjectId,
				a.SubjectType switch
				{
					AuditSubjectType.User => userDisplayNames.GetValueOrDefault(a.SubjectId, string.Empty),
					AuditSubjectType.Organization => organizationNames.GetValueOrDefault(a.SubjectId, string.Empty),
					AuditSubjectType.VolunteerOpportunity => opportunityTitles.GetValueOrDefault(a.SubjectId, string.Empty),
					AuditSubjectType.Engagement => engagementOpportunityIds.TryGetValue(a.SubjectId, out var opportunityId)
						? opportunityTitles.GetValueOrDefault(opportunityId, string.Empty)
						: string.Empty,
					_ => string.Empty,
				},
				a.Reason,
				a.CreatedOn))
			.ToList();

		return new PagedList<AuditLogEntry>(items, totalItems, pageNumber, pageSize);
	}
}
