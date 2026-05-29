using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Notifications;
using Domain.Notifications;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteVolunteerOpportunity.v1;

internal sealed class DeleteVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository)
	: ICommandHandler<DeleteVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = new VolunteerOpportunityId(request.OpportunityId);

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		// Notify volunteers with an active engagement before the opportunity is
		// removed, so they learn it is no longer available (#405).
		await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			NotificationKind.OpportunityDeleted,
			cancellationToken);

		dbContext.VolunteerOpportunities.Delete(opportunity);

		return true;
	}
}
