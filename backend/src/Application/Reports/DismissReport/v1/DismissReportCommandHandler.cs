using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
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

		return true;
	}
}
