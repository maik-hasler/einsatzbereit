using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements;
using Application.Notifications;
using Domain.Notifications;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UpdateTimeSlot.v1;

internal sealed class UpdateTimeSlotCommandHandler(
	IApplicationDbContext dbContext,
	IEngagementReadRepository engagementReadRepository)
	: ICommandHandler<UpdateTimeSlotCommand, bool>
{
	public async ValueTask<bool> Handle(
		UpdateTimeSlotCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = new VolunteerOpportunityId(request.OpportunityId);

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		opportunity.UpdateTimeSlot(
			new TimeSlotId(request.TimeSlotId),
			request.StartDateTime,
			request.EndDateTime,
			request.MaxParticipants);

		// Notify volunteers with an active engagement that a time slot changed (#406).
		await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			NotificationKind.OpportunityUpdated,
			cancellationToken);

		return true;
	}
}
