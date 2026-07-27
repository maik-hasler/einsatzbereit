using Application.Common.Pagination;
using Application.Reports.GetReportHistoryForTarget.v1;
using Application.Reports.ListFlaggedTargets.v1;
using Domain.Reports;

namespace Application.Reports;

public interface IAdminReportReadRepository
{
	ValueTask<PagedList<FlaggedTargetSummary>> GetFlaggedTargetsPagedAsync(
		int pageNumber,
		int pageSize,
		CancellationToken cancellationToken = default);

	Task<List<ReportHistoryEntry>> GetHistoryForTargetAsync(
		ReportTargetType targetType,
		Guid targetId,
		CancellationToken cancellationToken = default);
}
