using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;

namespace Application.Organizations.ReportOrganization.v1;

internal sealed class ReportOrganizationCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<ReportOrganizationCommand, bool>
{
	public async ValueTask<bool> Handle(
		ReportOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		var organizationId = OrganizationId.Create(request.OrganizationId).GetValueOrThrow();

		_ = await dbContext.Organizations.FindAsync(organizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		var alreadyReported = await dbContext.HasOpenReportAsync(
			ReportTargetType.Organization, request.OrganizationId, request.ReporterId, cancellationToken);
		if (alreadyReported)
			throw new ResultFailureException(Error.Conflict("Report.AlreadyReported", "You have already reported this."));

		var report = Report.Create(
			ReportTargetType.Organization,
			request.OrganizationId,
			request.ReporterId,
			request.Reason,
			request.Details).GetValueOrThrow();

		await dbContext.Reports.AddAsync(report, cancellationToken);

		return true;
	}
}
