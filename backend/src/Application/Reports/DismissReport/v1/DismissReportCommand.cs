using Application.Common.Messaging;

namespace Application.Reports.DismissReport.v1;

public sealed record DismissReportCommand(Guid ReportId)
	: ICommand<bool>;
