using Application.Common.Pagination;
using Application.Reports.ListOpenReports.v1;

namespace Application.Reports;

public interface IAdminReportReadRepository
{
	ValueTask<PagedList<AdminReportSummary>> GetOpenPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);
}
