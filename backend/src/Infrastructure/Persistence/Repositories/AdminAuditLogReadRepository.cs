using Application.AuditLogs;
using Application.AuditLogs.ListAuditLogs.v1;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Pagination;
using Domain.AuditLogs;
using Domain.Engagements;
using Domain.Organizations;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AdminAuditLogReadRepository(
	ApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IAdminAuditLogReadRepository
{
	public async ValueTask<PagedList<AuditLogEntry>> GetAuditLogsPagedAsync(
		AuditLogFilter filter,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var query = dbContext.AuditLogsQuery;

		if (filter.ActionType is { } actionType)
			query = query.Where(a => a.ActionType == actionType);

		if (filter.SubjectType is { } subjectType)
			query = query.Where(a => a.SubjectType == subjectType);

		if (filter.ActorUserId is { } actorUserId)
		{
			var actorId = UserId.Create(actorUserId).GetValueOrThrow();
			query = query.Where(a => a.ActorUserId == actorId);
		}

		if (filter.From is { } from)
			query = query.Where(a => a.CreatedOn >= from);

		if (filter.To is { } to)
			query = query.Where(a => a.CreatedOn < to);

		var totalItems = await query.CountAsync(cancellationToken);

		// Ties are broken by id, which is a v7 GUID and so already time-ordered: without it a
		// page boundary that lands inside a batch of same-instant entries can repeat or drop
		// rows between "Load more" calls.
		var ordered = filter.OldestFirst
			? query.OrderBy(a => a.CreatedOn).ThenBy(a => a.Id)
			: query.OrderByDescending(a => a.CreatedOn).ThenByDescending(a => a.Id);

		var page = await ordered
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

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
			? await dbContext.VolunteerOpportunitiesQuery
				.IgnoreQueryFilters()
				.Where(vo => opportunityIds.Contains(vo.Id))
				.ToDictionaryAsync(vo => vo.Id.Value, vo => new { vo.TitleDe, vo.TitleEn }, cancellationToken)
			: [];

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
					AuditSubjectType.VolunteerOpportunity => opportunityTitles.GetValueOrDefault(a.SubjectId)?.TitleDe ?? string.Empty,
					AuditSubjectType.Engagement => engagementOpportunityIds.TryGetValue(a.SubjectId, out var opportunityId)
						? opportunityTitles.GetValueOrDefault(opportunityId)?.TitleDe ?? string.Empty
						: string.Empty,
					_ => string.Empty,
				},
				// Only opportunity titles are authored twice; see the same note in
				// AdminReportReadRepository for why both languages travel (#2326).
				a.SubjectType switch
				{
					AuditSubjectType.VolunteerOpportunity => opportunityTitles.GetValueOrDefault(a.SubjectId)?.TitleEn,
					AuditSubjectType.Engagement => engagementOpportunityIds.TryGetValue(a.SubjectId, out var engagementOpportunityId)
						? opportunityTitles.GetValueOrDefault(engagementOpportunityId)?.TitleEn
						: null,
					_ => null,
				},
				a.Reason,
				a.CreatedOn))
			.ToList();

		return new PagedList<AuditLogEntry>(items, totalItems, pageNumber, pageSize);
	}
}
