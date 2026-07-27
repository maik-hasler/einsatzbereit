using Application.Common.Pagination;
using Application.Reports;
using Application.Reports.ListOpenReports.v1;
using Domain.Reports;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class AdminReportReadRepository(
	ApplicationDbContext dbContext)
	: IAdminReportReadRepository
{
	public async ValueTask<PagedList<AdminReportSummary>> GetOpenPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var openReports = await dbContext.ReportsQuery
			.Where(r => r.Status == ReportStatus.Open)
			.OrderByDescending(r => r.CreatedOn)
			.ToListAsync(cancellationToken);

		var opportunityIds = openReports
			.Where(r => r.TargetType == ReportTargetType.VolunteerOpportunity)
			.Select(r => r.TargetId)
			.ToList();

		var organizationIds = openReports
			.Where(r => r.TargetType == ReportTargetType.Organization)
			.Select(r => r.TargetId)
			.ToList();

		var opportunityTitles = await dbContext.VolunteerOpportunitiesQuery
			.Where(vo => opportunityIds.Contains(vo.Id.Value))
			.ToDictionaryAsync(vo => vo.Id.Value, vo => vo.Title, cancellationToken);

		var organizationNames = await dbContext.OrganizationsQuery
			.Where(o => organizationIds.Contains(o.Id.Value))
			.ToDictionaryAsync(o => o.Id.Value, o => o.Name, cancellationToken);

		var page = openReports
			.Skip((pageNumber - 1) * pageSize)
			.Take(pageSize)
			.Select(r => new AdminReportSummary(
				r.Id.Value,
				r.TargetType.ToString(),
				r.TargetId,
				r.TargetType == ReportTargetType.VolunteerOpportunity
					? opportunityTitles.GetValueOrDefault(r.TargetId, string.Empty)
					: organizationNames.GetValueOrDefault(r.TargetId, string.Empty),
				r.Reason.ToString(),
				r.Details,
				r.CreatedOn))
			.ToList();

		return new PagedList<AdminReportSummary>(page, openReports.Count, pageNumber, pageSize);
	}
}
