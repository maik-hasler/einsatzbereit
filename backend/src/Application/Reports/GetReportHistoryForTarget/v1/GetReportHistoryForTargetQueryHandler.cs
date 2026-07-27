using Application.Common.Messaging;

namespace Application.Reports.GetReportHistoryForTarget.v1;

internal sealed class GetReportHistoryForTargetQueryHandler(
	IAdminReportReadRepository readRepository)
	: IQueryHandler<GetReportHistoryForTargetQuery, List<ReportHistoryEntry>>
{
	public async ValueTask<List<ReportHistoryEntry>> Handle(
		GetReportHistoryForTargetQuery request,
		CancellationToken cancellationToken = default) =>
		await readRepository.GetHistoryForTargetAsync(request.TargetType, request.TargetId, cancellationToken);
}
