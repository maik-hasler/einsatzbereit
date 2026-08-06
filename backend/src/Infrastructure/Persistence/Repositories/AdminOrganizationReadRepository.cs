using Application.Common.Exceptions;
using Application.Common.Pagination;
using Application.Organizations;
using Application.Organizations.ListOrganizations.v1;
using Domain.Organizations;
using Domain.Reports;
using Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AdminOrganizationReadRepository(
	ApplicationDbContext dbContext)
	: IAdminOrganizationReadRepository
{
	public async ValueTask<PagedList<AdminOrganizationSummary>> GetPagedAsync(
		int pageNumber,
		int pageSize,
		string? search,
		bool? deleted,
		bool? flagged,
		CancellationToken cancellationToken = default)
	{
		var query = deleted == true
			? dbContext.OrganizationsQuery.IgnoreQueryFilters().Where(o => o.IsDeleted)
			: dbContext.OrganizationsQuery;

		if (!string.IsNullOrWhiteSpace(search))
		{
			var normalizedSearch = search.ToLower();
			query = query.Where(o => o.Name.ToLower().Contains(normalizedSearch));
		}

		if (flagged == true)
		{
			var flaggedOrganizationIds = await dbContext.ReportsQuery
				.Where(r => r.TargetType == ReportTargetType.Organization && r.Status == ReportStatus.Open)
				.Select(r => r.TargetId)
				.Distinct()
				.ToListAsync(cancellationToken);

			var flaggedOrganizationIdVOs = flaggedOrganizationIds
				.Select(id => OrganizationId.Create(id).GetValueOrThrow())
				.ToList();

			query = query.Where(o => flaggedOrganizationIdVOs.Contains(o.Id));
		}

		var paged = await query
			.OrderBy(o => o.Name)
			.ThenBy(o => o.Id)
			.ToPagedListAsync(pageNumber, pageSize, cancellationToken);

		var organizationIds = paged.Items.Select(o => o.Id).ToList();
		var organizationGuidIds = organizationIds.Select(id => id.Value).ToList();

		var openReportCounts = await dbContext.ReportsQuery
			.Where(r => r.TargetType == ReportTargetType.Organization
				&& r.Status == ReportStatus.Open
				&& organizationGuidIds.Contains(r.TargetId))
			.GroupBy(r => r.TargetId)
			.Select(g => new { OrganizationId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.OrganizationId, x => x.Count, cancellationToken);

		var memberCounts = await dbContext.OrganizationMembershipsQuery
			.Where(m => organizationIds.Contains(m.OrganizationId))
			.GroupBy(m => m.OrganizationId)
			.Select(g => new { OrganizationId = g.Key, Count = g.Count() })
			.ToDictionaryAsync(x => x.OrganizationId, x => x.Count, cancellationToken);

		return paged.Map(o => new AdminOrganizationSummary(
			o.Id.Value,
			o.Name,
			o.LogoUrl,
			o.IsDeleted,
			openReportCounts.GetValueOrDefault(o.Id.Value, 0),
			memberCounts.GetValueOrDefault(o.Id, 0),
			o.CreatedOn));
	}
}
