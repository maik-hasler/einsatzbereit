using Application.Common.Messaging;
using Application.Common.Pagination;

namespace Application.Reports.ListOpenReports.v1;

public sealed record ListOpenReportsQuery(
	int PageNumber,
	int PageSize)
	: IQuery<PagedList<AdminReportSummary>>;
