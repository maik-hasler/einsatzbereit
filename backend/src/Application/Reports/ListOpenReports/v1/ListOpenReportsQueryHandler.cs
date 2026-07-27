using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Reports.ListOpenReports.v1;

internal sealed class ListOpenReportsQueryHandler(
	IAdminReportReadRepository readRepository)
	: IQueryHandler<ListOpenReportsQuery, PagedList<AdminReportSummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<AdminReportSummary>> Handle(
		ListOpenReportsQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await readRepository.GetOpenPagedAsync(pageNumber, pageSize, cancellationToken);
	}
}
