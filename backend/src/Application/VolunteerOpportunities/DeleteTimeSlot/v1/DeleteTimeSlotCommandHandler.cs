using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Engagements.Common;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteTimeSlot.v1;

internal sealed class DeleteTimeSlotCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<DeleteTimeSlotCommand, DeleteTimeSlotResult>
{
	public async ValueTask<DeleteTimeSlotResult> Handle(
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
		var targetSlot = opportunity.TimeSlots.FirstOrDefault(ts => ts.Id == timeSlotId)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.TimeSlotNotFound", $"Time slot with id '{request.TimeSlotId}' not found."));

		if (request.Scope == SeriesEditScope.Only)
		{
			var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(timeSlotId, cancellationToken);
			if (activeCount > 0)
				throw new ResultFailureException(Error.Conflict(
					"VolunteerOpportunity.TimeSlotHasActiveEngagements",
					$"Cannot delete a time slot that has {activeCount} active sign-up(s). Cancel the affected engagements first."));

			opportunity.RemoveTimeSlot(timeSlotId).ThrowIfFailure();
			return new DeleteTimeSlotResult([request.TimeSlotId]);
		}

		return await DeleteSeriesAsync(opportunity, targetSlot, request.Scope, cancellationToken);
	}

	/// <summary>
	/// "This and following"/"entire series": unlike the single-slot delete
	/// above, active engagements don't block the delete - they're force-
	/// cancelled and their volunteers notified instead, so cancelling a whole
	/// recurring series doesn't require the organizer to first hunt down and
	/// individually cancel every affected sign-up (einsatzbereit#1058). Past
	/// occurrences are left alone regardless of scope, preserving history.
	/// </summary>
	private async ValueTask<DeleteTimeSlotResult> DeleteSeriesAsync(
		VolunteerOpportunity opportunity,
		TimeSlot targetSlot,
		SeriesEditScope scope,
		CancellationToken cancellationToken)
	{
		if (targetSlot.SeriesId is null)
			throw new ResultFailureException(Error.Validation(
				"VolunteerOpportunity.TimeSlotNotPartOfSeries",
				"This time slot is not part of a recurring series."));

		var now = DateTimeOffset.UtcNow;
		var affectedSlots = opportunity.TimeSlots
			.Where(ts => ts.SeriesId == targetSlot.SeriesId && ts.StartDateTime > now)
			.Where(ts => scope == SeriesEditScope.EntireSeries || ts.StartDateTime >= targetSlot.StartDateTime)
			.OrderBy(ts => ts.StartDateTime)
			.ToList();

		var affectedIds = affectedSlots.Select(ts => ts.Id).ToList();

		var engagementsToCancel = await dbContext.GetActiveEngagementsForTimeSlotsAsync(affectedIds, cancellationToken);
		foreach (var engagement in engagementsToCancel)
		{
			await EngagementCancellationHelper.CancelAndNotifyAsync(
				dbContext,
				engagement,
				"The recurring time slot series was cancelled.",
				opportunity.Title,
				cancellationToken);
		}

		foreach (var slot in affectedSlots)
		{
			opportunity.RemoveTimeSlot(slot.Id).ThrowIfFailure();
		}

		return new DeleteTimeSlotResult(affectedIds.Select(id => id.Value).ToList());
	}
}
