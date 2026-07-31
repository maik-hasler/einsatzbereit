using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
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
	IEngagementReadRepository engagementReadRepository,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer)
	: ICommandHandler<UpdateTimeSlotCommand, UpdateTimeSlotResult>
{
	public async ValueTask<UpdateTimeSlotResult> Handle(
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
		var targetSlot = opportunity.TimeSlots.FirstOrDefault(ts => ts.Id == timeSlotId)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.TimeSlotNotFound", $"Time slot with id '{request.TimeSlotId}' not found."));

		if (request.Scope == SeriesEditScope.Only)
			return await UpdateOnlyAsync(opportunity, opportunityId, timeSlotId, request, cancellationToken);

		return await UpdateSeriesCapacityAsync(opportunity, opportunityId, targetSlot, request, cancellationToken);
	}

	private async ValueTask<UpdateTimeSlotResult> UpdateOnlyAsync(
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		TimeSlotId timeSlotId,
		UpdateTimeSlotCommand request,
		CancellationToken cancellationToken)
	{
		if (request.StartDateTime is null || request.EndDateTime is null)
			throw new ResultFailureException(Error.Validation(
				"VolunteerOpportunity.TimeSlotDatesRequired",
				"StartDateTime and EndDateTime are required when Scope is Only."));

		var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(timeSlotId, cancellationToken);
		if (request.MaxParticipants is int max && max < activeCount)
			throw new ResultFailureException(Error.Validation(
				"VolunteerOpportunity.TimeSlotCapacityBelowActive",
				$"Cannot reduce capacity below the current number of active sign-ups ({activeCount})."));

		opportunity.UpdateTimeSlot(
			timeSlotId,
			request.StartDateTime.Value,
			request.EndDateTime.Value,
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
			timeSlotId,
			keycloakUserService,
			emailService,
			emailTemplateRenderer,
			opportunity.Title);

		return new UpdateTimeSlotResult(1, []);
	}

	/// <summary>
	/// "This and following"/"entire series": capacity-only, deliberately not
	/// letting a single edit reschedule sibling occurrences (einsatzbereit#1058).
	/// An occurrence whose active sign-up count already exceeds the requested
	/// capacity is skipped rather than failing the whole batch, mirroring the
	/// single-slot guard below applied per-occurrence.
	/// </summary>
	private async ValueTask<UpdateTimeSlotResult> UpdateSeriesCapacityAsync(
		VolunteerOpportunity opportunity,
		VolunteerOpportunityId opportunityId,
		TimeSlot targetSlot,
		UpdateTimeSlotCommand request,
		CancellationToken cancellationToken)
	{
		if (targetSlot.SeriesId is null)
			throw new ResultFailureException(Error.Validation(
				"VolunteerOpportunity.TimeSlotNotPartOfSeries",
				"This time slot is not part of a recurring series."));

		var now = DateTimeOffset.UtcNow;
		var affectedSlots = opportunity.TimeSlots
			.Where(ts => ts.SeriesId == targetSlot.SeriesId && ts.StartDateTime > now)
			.Where(ts => request.Scope == SeriesEditScope.EntireSeries || ts.StartDateTime >= targetSlot.StartDateTime)
			.OrderBy(ts => ts.StartDateTime)
			.ToList();

		var skipped = new List<Guid>();
		var updatedCount = 0;

		foreach (var slot in affectedSlots)
		{
			var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(slot.Id, cancellationToken);
			if (request.MaxParticipants < activeCount)
			{
				skipped.Add(slot.Id.Value);
				continue;
			}

			opportunity.UpdateTimeSlotCapacity(slot.Id, request.MaxParticipants).ThrowIfFailure();
			updatedCount++;

			await OpportunityNotificationHelper.NotifyActiveVolunteersAsync(
				dbContext,
				engagementReadRepository,
				opportunityId,
				NotificationKind.OpportunityUpdated,
				cancellationToken,
				slot.Id);
		}

		return new UpdateTimeSlotResult(updatedCount, skipped);
	}
}
