using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteTimeSlot.v1;

internal sealed class DeleteTimeSlotCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<DeleteTimeSlotCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteTimeSlotCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var timeSlotId = TimeSlotId.Create(request.TimeSlotId).GetValueOrThrow();

		var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(timeSlotId, cancellationToken);
		if (activeCount > 0)
			throw new ResultFailureException(Error.Conflict(
				"VolunteerOpportunity.TimeSlotHasActiveEngagements",
				$"Cannot delete a time slot that has {activeCount} active sign-up(s). Cancel the affected engagements first."));

		opportunity.RemoveTimeSlot(timeSlotId).ThrowIfFailure();

		return true;
	}
}
