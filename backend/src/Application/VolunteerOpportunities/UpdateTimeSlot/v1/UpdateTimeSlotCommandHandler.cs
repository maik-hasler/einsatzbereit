using Application.Common.Authorization;
using Application.Common.Exceptions;
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
		var opportunityId = VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow();

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var timeSlotId = TimeSlotId.Create(request.TimeSlotId).GetValueOrThrow();
		var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(timeSlotId, cancellationToken);
		if (request.MaxParticipants is int max && max < activeCount)
			throw new ResultFailureException(Error.Validation(
				"VolunteerOpportunity.TimeSlotCapacityBelowActive",
				$"Cannot reduce capacity below the current number of active sign-ups ({activeCount})."));

		opportunity.UpdateTimeSlot(
			timeSlotId,
			request.StartDateTime,
			request.EndDateTime,
			request.MaxParticipants,
			DateTimeOffset.UtcNow).ThrowIfFailure();

		// Notify volunteers with an active engagement on this time slot that it
		// changed (#406) - scoped to the slot itself, not every volunteer on the
		// opportunity, so editing one slot doesn't spam registrants of others (#811).
		await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
			dbContext,
			engagementReadRepository,
			opportunityId,
			NotificationKind.OpportunityUpdated,
			cancellationToken,
			timeSlotId);

		return true;
	}
}
