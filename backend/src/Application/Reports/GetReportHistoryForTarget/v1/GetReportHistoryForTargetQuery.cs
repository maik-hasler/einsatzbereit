using Application.Common.Messaging;
using Domain.Reports;

namespace Application.Reports.GetReportHistoryForTarget.v1;

public sealed record GetReportHistoryForTargetQuery(
	ReportTargetType TargetType,
	Guid TargetId)
	: IQuery<List<ReportHistoryEntry>>;
