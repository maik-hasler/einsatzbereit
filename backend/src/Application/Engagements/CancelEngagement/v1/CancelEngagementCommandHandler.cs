using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.AuditLogs;
using Domain.Engagements;
using Domain.Primitives;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CancelEngagement.v1;

internal sealed class CancelEngagementCommandHandler(
	IApplicationDbContext dbContext,
	ILogger<CancelEngagementCommandHandler> logger)
	: ICommandHandler<CancelEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CancelEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(engagement.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{engagement.OpportunityId.Value}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var cancelled = await EngagementCancellationHelper.CancelAsync(
			dbContext,
			engagement,
			request.Reason,
			opportunity.TitleDe,

			notifyVolunteer: true,
			logger,
			cancellationToken);

		if (cancelled)
		{
			var auditLog = AuditLog.Create(
				request.RequestingUserId,
				AuditActionType.EngagementCancelled,
				AuditSubjectType.Engagement,
				engagement.Id.Value,
				request.Reason);
			await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
		}

		return engagement;
	}
}
