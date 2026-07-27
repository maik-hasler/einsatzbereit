using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Organizations.DeleteOrganization.v1;
using Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;
using Domain.Primitives;
using Domain.Reports;

namespace Application.Reports.ResolveReport.v1;

internal sealed class ResolveReportCommandHandler(
	IApplicationDbContext dbContext,
	ISender sender)
	: ICommandHandler<ResolveReportCommand, bool>
{
	public async ValueTask<bool> Handle(
		ResolveReportCommand request,
		CancellationToken cancellationToken = default)
	{
		var reportId = ReportId.Create(request.ReportId).GetValueOrThrow();

		var report = await dbContext.Reports.FindAsync(reportId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Report.NotFound", $"Report '{request.ReportId}' not found."));

		try
		{
			switch (report.ContentType)
			{
				case ReportedContentType.VolunteerOpportunity:
					await sender.Send(
						new DeleteVolunteerOpportunityCommand(report.ContentId, request.ActingUserId, IsAdmin: true),
						cancellationToken);
					break;
				case ReportedContentType.Organization:
					await sender.Send(
						new DeleteOrganizationCommand(report.ContentId, request.ActingUserId, IsAdmin: true),
						cancellationToken);
					break;
			}
		}
		catch (ResultFailureException ex) when (ex.Error.Type == ErrorType.NotFound)
		{
			// The content was already removed some other way (e.g. the organizer
			// deleted it themselves before an admin acted on the report) - the
			// report's goal is achieved either way, so still resolve it.
		}

		report.Resolve().ThrowIfFailure();

		return true;
	}
}
