using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CreateEngagement.v1;

internal sealed class CreateEngagementCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<CreateEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CreateEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			request.OpportunityId, cancellationToken);

		if (opportunity is null)
			throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity with id '{request.OpportunityId.Value}' was not found."));

		var alreadySignedUp = await dbContext.HasEngagementAsync(
			request.VolunteerId, request.OpportunityId, request.TimeSlotId, cancellationToken);

		if (alreadySignedUp)
			throw new ResultFailureException(Error.Conflict("Engagement.AlreadySignedUp", "Conflict: you are already signed up for this opportunity."));

		if (request.TimeSlotId is not null)
		{
			var timeSlot = opportunity.TimeSlots.FirstOrDefault(ts => ts.Id == request.TimeSlotId);
			if (timeSlot is null)
				throw new ResultFailureException(Error.Validation("Engagement.TimeSlotNotInOpportunity", "The selected time slot does not belong to this opportunity."));

			var activeCount = await dbContext.CountActiveEngagementsForTimeSlotAsync(
				request.TimeSlotId.Value, cancellationToken);
			if (timeSlot.MaxParticipants is int max && activeCount >= max)
				throw new ResultFailureException(Error.Conflict("Engagement.TimeSlotFull", "Conflict: this time slot has reached its capacity and cannot accept more sign-ups."));
		}

		var existingTerminal = await dbContext.GetTerminalEngagementAsync(
			request.VolunteerId, request.OpportunityId, request.TimeSlotId, cancellationToken);

		Engagement engagement;
		if (existingTerminal is not null)
		{
			existingTerminal.Reactivate(request.TimeSlotId, request.Message).ThrowIfFailure();
			engagement = existingTerminal;
		}
		else
		{
			engagement = request.TimeSlotId is not null
				? Engagement.CreateSlotSignUp(request.OpportunityId, request.VolunteerId, request.TimeSlotId.Value)
				: Engagement.CreateIndividualContact(request.OpportunityId, request.VolunteerId, request.Message
					?? throw new ResultFailureException(Error.Validation("Engagement.MessageRequired", "Message is required for individual contact."))).GetValueOrThrow();

			await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		}

		return engagement;
	}
}
