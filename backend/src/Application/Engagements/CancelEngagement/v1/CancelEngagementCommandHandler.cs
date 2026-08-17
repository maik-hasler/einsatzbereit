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
			// The volunteer hears about this only here - no opportunity-level
			// notification accompanies a single engagement cancellation.
			notifyVolunteer: true,
			logger,
			cancellationToken);

		// Only when a cancellation actually happened - CancelAsync leaves an
		// already-anonymized engagement (its volunteer deleted their account) untouched
		// rather than throwing (einsatzbereit#1724), and an audit entry claiming
		// "EngagementCancelled" for a no-op would be misleading.
		if (cancelled)
		{
			// Audited here (not via EngagementCancelledDomainEvent) since that event is
			// also raised for cascade cancellations from an opportunity/organization
			// shadow-delete - those are already audited as their own action and would
			// otherwise double up with a per-engagement entry (#1088).
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
