using Application.Common.Keycloak;
using Application.Common.Pagination;
using Application.Reports;
using Application.Reports.GetReportHistoryForTarget.v1;
using Application.Reports.ListFlaggedTargets.v1;
using Domain.Reports;
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
		CancellationToken cancellationToken = default)
	{
		var allReports = await dbContext.ReportsQuery.ToListAsync(cancellationToken);

		var groups = allReports
			.GroupBy(r => (r.TargetType, r.TargetId))
			.Select(g => new
			{
				g.Key.TargetType,
				g.Key.TargetId,
				OpenCount = g.Count(r => r.Status == ReportStatus.Open),
				TotalCount = g.Count(),
				LastReportedOn = g.Max(r => r.CreatedOn),
			})
			.OrderByDescending(g => g.LastReportedOn)
			.ToList();

		var totalItems = groups.Count;

		var page = groups
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.ToList();

		var opportunityIds = page.Where(g => g.TargetType == ReportTargetType.VolunteerOpportunity).Select(g => g.TargetId).ToList();
		var organizationIds = page.Where(g => g.TargetType == ReportTargetType.Organization).Select(g => g.TargetId).ToList();
		var userIds = page.Where(g => g.TargetType == ReportTargetType.User).Select(g => g.TargetId).ToList();

		var opportunities = await dbContext.VolunteerOpportunitiesQuery
			.IgnoreQueryFilters()
			.Where(vo => opportunityIds.Contains(vo.Id.Value))
			.ToDictionaryAsync(vo => vo.Id.Value, vo => new { vo.Title, vo.IsDeleted }, cancellationToken);

		var organizations = await dbContext.OrganizationsQuery
			.IgnoreQueryFilters()
			.Where(o => organizationIds.Contains(o.Id.Value))
			.ToDictionaryAsync(o => o.Id.Value, o => new { o.Name, o.IsDeleted }, cancellationToken);

		var deletedUserIds = await dbContext.UsersQuery
			.IgnoreQueryFilters()
			.Where(u => userIds.Contains(u.Id.Value) && u.IsDeleted)
			.Select(u => u.Id.Value)
			.ToListAsync(cancellationToken);
		var userDisplayNames = userIds.Count > 0
			? await keycloakUserService.GetDisplayNamesAsync(userIds, cancellationToken)
			: new Dictionary<Guid, string>();

		var items = page
			.Select(g => new FlaggedTargetSummary(
				g.TargetType.ToString(),
				g.TargetId,
				g.TargetType switch
				{
					ReportTargetType.VolunteerOpportunity => opportunities.GetValueOrDefault(g.TargetId)?.Title ?? string.Empty,
					ReportTargetType.Organization => organizations.GetValueOrDefault(g.TargetId)?.Name ?? string.Empty,
					ReportTargetType.User => userDisplayNames.GetValueOrDefault(g.TargetId, string.Empty),
					_ => string.Empty,
				},
				g.OpenCount,
				g.TotalCount,
				g.LastReportedOn,
				g.TargetType switch
				{
					ReportTargetType.VolunteerOpportunity => opportunities.GetValueOrDefault(g.TargetId)?.IsDeleted ?? false,
					ReportTargetType.Organization => organizations.GetValueOrDefault(g.TargetId)?.IsDeleted ?? false,
					ReportTargetType.User => deletedUserIds.Contains(g.TargetId),
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
