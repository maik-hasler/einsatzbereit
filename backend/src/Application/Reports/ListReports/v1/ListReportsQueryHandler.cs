using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Reports.ListReports.v1;

internal sealed class ListReportsQueryHandler(
	IReportReadRepository readRepository)
	: IQueryHandler<ListReportsQuery, PagedList<AdminReportSummary>>
{
	private const int MaxPageSize = 100;

	public async ValueTask<PagedList<AdminReportSummary>> Handle(
		ListReportsQuery request,
		CancellationToken cancellationToken = default)
	{
		var pageNumber = Math.Max(1, request.PageNumber);
		var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

		return await readRepository.GetPagedAsync(request.Status, pageNumber, pageSize, cancellationToken);
	}
}
