using Application.Common.Messaging;
using Application.Common.Pagination;
using Domain.Reports;

namespace Application.Reports.ListReports.v1;

public sealed record ListReportsQuery(
	ReportStatus? Status,
	int PageNumber,
	int PageSize)
	: IQuery<PagedList<AdminReportSummary>>;
