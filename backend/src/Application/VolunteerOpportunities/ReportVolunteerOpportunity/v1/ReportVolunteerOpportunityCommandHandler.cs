using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.Reports;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.ReportVolunteerOpportunity.v1;

internal sealed class ReportVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<ReportVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		ReportVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow();

		_ = await dbContext.VolunteerOpportunities.FindAsync(opportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		var alreadyReported = await dbContext.HasDuplicateReportAsync(
			ReportTargetType.VolunteerOpportunity, request.OpportunityId, request.ReporterId, cancellationToken);
		if (alreadyReported)
			throw new ResultFailureException(Error.Conflict("Report.AlreadyReported", "You have already reported this."));

		var report = Report.Create(
			ReportTargetType.VolunteerOpportunity,
			request.OpportunityId,
			request.ReporterId,
			request.Reason,
			request.Details).GetValueOrThrow();

		await dbContext.Reports.AddAsync(report, cancellationToken);

		return true;
	}
}
