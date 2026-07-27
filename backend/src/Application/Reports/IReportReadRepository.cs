using Application.Common.Pagination;
using Application.Reports.ListReports.v1;
using Domain.Reports;

namespace Application.Reports;

public interface IReportReadRepository
{
	ValueTask<PagedList<AdminReportSummary>> GetPagedAsync(
		ReportStatus? status,
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);
}
