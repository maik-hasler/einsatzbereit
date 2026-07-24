using Application.Common.Messaging;
using Domain.Engagements;
using Microsoft.Extensions.Logging;

namespace Application.Engagements.CheckInEngagement.v1;

// First real consumer of the domain-event pipeline (#828) - a structured audit
// trail of check-ins, independent of the in-app Notifications feature (which
// has no check-in notification today, so there is no double-notify risk here).
internal sealed class EngagementCheckedInAuditLogHandler(
	ILogger<EngagementCheckedInAuditLogHandler> logger)
	: INotificationHandler<EngagementCheckedInDomainEvent>
{
	public Task Handle(
		EngagementCheckedInDomainEvent notification,
		CancellationToken cancellationToken)
	{
		logger.LogInformation(
			"Volunteer {VolunteerId} checked in for engagement {EngagementId} on opportunity {OpportunityId}",
			notification.VolunteerId.Value,
			notification.EngagementId.Value,
			notification.OpportunityId.Value);

		return Task.CompletedTask;
	}
}
