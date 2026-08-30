using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Pagination;
using Application.Reports;
using Application.Reports.GetReportHistoryForTarget.v1;
using Application.Reports.ListFlaggedTargets.v1;
using Domain.Organizations;
using Domain.Reports;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AdminReportReadRepository(
	ApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IAdminReportReadRepository
{
	public async ValueTask<PagedList<FlaggedTargetSummary>> GetFlaggedTargetsPagedAsync(
		int pageNumber,
		int pageSize,
		bool includeResolved,
		CancellationToken cancellationToken = default)
	{
		// Grouped first, then filtered on the aggregate: a target belongs in the queue while
		// any of its reports is still Open. Without this the queue only ever grows - a target
		// whose every report was dismissed or actioned stayed listed at "0 open flags" forever,
		// and the "all caught up" empty state became unreachable after the first ever report
		// (#2326). Resolved targets stay retrievable via includeResolved.
		var groupedReports = dbContext.ReportsQuery
			.GroupBy(r => new { r.TargetType, r.TargetId })
			.Select(g => new
			{
				g.Key.TargetType,
				g.Key.TargetId,
				OpenCount = g.Count(r => r.Status == ReportStatus.Open),
				TotalCount = g.Count(),
				LastReportedOn = g.Max(r => r.CreatedOn),
			});

		if (!includeResolved)
			groupedReports = groupedReports.Where(g => g.OpenCount > 0);

		var totalItems = await groupedReports.CountAsync(cancellationToken);

		var page = await groupedReports
			.OrderByDescending(g => g.LastReportedOn)
			.ThenBy(g => g.TargetId)
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		var opportunityIds = page.Where(g => g.TargetType == ReportTargetType.VolunteerOpportunity).Select(g => g.TargetId).ToList();
		var organizationIds = page.Where(g => g.TargetType == ReportTargetType.Organization).Select(g => g.TargetId).ToList();
		var userIds = page.Where(g => g.TargetType == ReportTargetType.User).Select(g => g.TargetId).ToList();

		var opportunityIdVOs = opportunityIds.Select(id => VolunteerOpportunityId.Create(id).GetValueOrThrow()).ToList();
		var organizationIdVOs = organizationIds.Select(id => OrganizationId.Create(id).GetValueOrThrow()).ToList();
		var userIdVOs = userIds.Select(id => UserId.Create(id).GetValueOrThrow()).ToList();

		var opportunities = await dbContext.VolunteerOpportunitiesQuery
			.IgnoreQueryFilters()
			.Where(vo => opportunityIdVOs.Contains(vo.Id))
			.ToDictionaryAsync(vo => vo.Id.Value, vo => new { vo.TitleDe, vo.TitleEn, vo.IsDeleted }, cancellationToken);

		var organizations = await dbContext.OrganizationsQuery
			.IgnoreQueryFilters()
			.Where(o => organizationIdVOs.Contains(o.Id))
			.ToDictionaryAsync(o => o.Id.Value, o => new { o.Name, o.IsDeleted }, cancellationToken);

		var deletedUserIds = await dbContext.UsersQuery
			.IgnoreQueryFilters()
			.Where(u => userIdVOs.Contains(u.Id) && u.IsDeleted)
			.Select(u => u.Id)
			.ToListAsync(cancellationToken);
		var deletedUserIdSet = deletedUserIds.Select(id => id.Value).ToHashSet();
		var userDisplayNames = userIds.Count > 0
			? await keycloakUserService.GetDisplayNamesAsync(userIds, cancellationToken)
			: new Dictionary<Guid, string>();

		var items = page
			.Select(g => new FlaggedTargetSummary(
				g.TargetType.ToString(),
				g.TargetId,
				g.TargetType switch
				{
					ReportTargetType.VolunteerOpportunity => opportunities.GetValueOrDefault(g.TargetId)?.TitleDe ?? string.Empty,
					ReportTargetType.Organization => organizations.GetValueOrDefault(g.TargetId)?.Name ?? string.Empty,
					ReportTargetType.User => userDisplayNames.GetValueOrDefault(g.TargetId, string.Empty),
					_ => string.Empty,
				},
				// Opportunity titles are authored per language; only the German one is required.
				// Both travel so the console can render the admin's own language and mark the
				// row lang="de" when it has to fall back (#2326). Organization names and user
				// names are single-language by nature, so they carry no English variant.
				g.TargetType == ReportTargetType.VolunteerOpportunity
					? opportunities.GetValueOrDefault(g.TargetId)?.TitleEn
					: null,
				g.OpenCount,
				g.TotalCount,
				g.LastReportedOn,
				g.TargetType switch
				{
					ReportTargetType.VolunteerOpportunity => opportunities.GetValueOrDefault(g.TargetId)?.IsDeleted ?? false,
					ReportTargetType.Organization => organizations.GetValueOrDefault(g.TargetId)?.IsDeleted ?? false,
					ReportTargetType.User => deletedUserIdSet.Contains(g.TargetId),
					_ => false,
				}))
			.ToList();

		return new PagedList<FlaggedTargetSummary>(items, totalItems, pageNumber, pageSize);
	}

	public async Task<List<ReportHistoryEntry>> GetHistoryForTargetAsync(
		ReportTargetType targetType,
		Guid targetId,
		CancellationToken cancellationToken = default)
	{
		var reports = await dbContext.ReportsQuery
			.Where(r => r.TargetType == targetType && r.TargetId == targetId)
			.OrderByDescending(r => r.CreatedOn)
			.ToListAsync(cancellationToken);

		return reports
			.Select(r => new ReportHistoryEntry(
				r.Id.Value,
				r.ReporterId.Value,
				r.Reason.ToString(),
				r.Details,
				r.Status.ToString(),
				r.CreatedOn,
				r.ResolvedByUserId?.Value,
				r.ResolvedOn))
			.ToList();
	}
}
