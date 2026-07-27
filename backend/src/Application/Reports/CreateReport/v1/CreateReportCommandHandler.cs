using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Reports;
using Domain.VolunteerOpportunities;

namespace Application.Reports.CreateReport.v1;

internal sealed class CreateReportCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<CreateReportCommand, Guid>
{
	public async ValueTask<Guid> Handle(
		CreateReportCommand request,
		CancellationToken cancellationToken = default)
	{
		var contentExists = request.ContentType switch
		{
			ReportedContentType.VolunteerOpportunity => await dbContext.VolunteerOpportunities.FindAsync(
				VolunteerOpportunityId.Create(request.ContentId).GetValueOrThrow(), cancellationToken) is not null,
			ReportedContentType.Organization => await dbContext.Organizations.FindAsync(
				OrganizationId.Create(request.ContentId).GetValueOrThrow(), cancellationToken) is not null,
			_ => false
		};

		if (!contentExists)
			throw new ResultFailureException(Error.NotFound(
				"Report.ContentNotFound",
				$"The reported content '{request.ContentId}' was not found."));

		var report = Report.Create(
			request.ContentType,
			request.ContentId,
			request.ReporterId,
			request.Reason,
			request.Detail).GetValueOrThrow();

		await dbContext.Reports.AddAsync(report, cancellationToken);

		return report.Id.Value;
	}
}
