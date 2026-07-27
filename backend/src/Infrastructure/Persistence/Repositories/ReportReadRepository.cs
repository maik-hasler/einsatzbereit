using Application.Common.Exceptions;
using Application.Common.Pagination;
using Application.Reports;
using Application.Reports.ListReports.v1;
using Domain.Organizations;
using Domain.Reports;
using Domain.VolunteerOpportunities;
using Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal sealed class ReportReadRepository(
	ApplicationDbContext dbContext)
	: IReportReadRepository
{
	public async ValueTask<PagedList<AdminReportSummary>> GetPagedAsync(
		ReportStatus? status,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default)
	{
		var query = dbContext.ReportsQuery;
		if (status.HasValue)
			query = query.Where(r => r.Status == status.Value);

		var paged = await query
			.OrderByDescending(r => r.CreatedOn)
			.ToPagedListAsync(pageNumber, pageSize, cancellationToken);

		var opportunityIds = paged.Items
			.Where(r => r.ContentType == ReportedContentType.VolunteerOpportunity)
			.Select(r => VolunteerOpportunityId.Create(r.ContentId).GetValueOrThrow())
			.Distinct()
			.ToList();

		var organizationIds = paged.Items
			.Where(r => r.ContentType == ReportedContentType.Organization)
			.Select(r => OrganizationId.Create(r.ContentId).GetValueOrThrow())
			.Distinct()
			.ToList();

		var opportunityTitles = await dbContext.VolunteerOpportunitiesQuery
			.Where(o => opportunityIds.Contains(o.Id))
			.ToDictionaryAsync(o => o.Id, o => o.Title, cancellationToken);

		var organizationNames = await dbContext.OrganizationsQuery
			.Where(o => organizationIds.Contains(o.Id))
			.ToDictionaryAsync(o => o.Id, o => o.Name, cancellationToken);

		return paged.Map(r => new AdminReportSummary(
			r.Id.Value,
			r.ContentType.ToString(),
			r.ContentId,
			r.ContentType == ReportedContentType.VolunteerOpportunity
				? opportunityTitles.GetValueOrDefault(VolunteerOpportunityId.Create(r.ContentId).GetValueOrThrow())
				: organizationNames.GetValueOrDefault(OrganizationId.Create(r.ContentId).GetValueOrThrow()),
			r.ReporterId.Value,
			r.Reason.ToString(),
			r.Detail,
			r.Status.ToString(),
			r.CreatedOn));
	}
}
