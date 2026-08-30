using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.AuditLogs;
using Domain.Primitives;
using Domain.Reports;

namespace Application.Reports.DismissReport.v1;

internal sealed class DismissReportCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<DismissReportCommand, bool>
{
	public async ValueTask<bool> Handle(
		DismissReportCommand request,
		CancellationToken cancellationToken = default)
	{
		var reportId = ReportId.Create(request.ReportId).GetValueOrThrow();

		var report = await dbContext.Reports.FindAsync(reportId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Report.NotFound", $"Report '{request.ReportId}' not found."));

		report.Dismiss(request.AdminUserId, DateTimeOffset.UtcNow).ThrowIfFailure();

		// Dismissal is the moderation decision that leaves no other trace: the report simply
		// stops being open, and nothing else in the system records who decided that or when.
		// Its counterpart - hiding the target - has been audited from the start, so auditing
		// only one of the two outcomes made the log a partial account of moderation (#2326).
		// The subject is the reported target, not the report row, so the entry lines up with
		// the shadow-delete and restore entries for that same target.
		var auditLog = AuditLog.Create(
			request.AdminUserId,
			AuditActionType.ReportDismissed,
			ToAuditSubjectType(report.TargetType),
			report.TargetId);
		await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);

		return true;
	}

	private static AuditSubjectType ToAuditSubjectType(ReportTargetType targetType) =>
		targetType switch
		{
			ReportTargetType.VolunteerOpportunity => AuditSubjectType.VolunteerOpportunity,
			ReportTargetType.Organization => AuditSubjectType.Organization,
			ReportTargetType.User => AuditSubjectType.User,
			_ => throw new ResultFailureException(
				Error.Validation("Report.UnknownTargetType", $"Unknown report target type '{targetType}'.")),
		};
}
