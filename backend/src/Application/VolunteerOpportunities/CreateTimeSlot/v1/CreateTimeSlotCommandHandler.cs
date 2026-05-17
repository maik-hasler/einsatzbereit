using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.CreateTimeSlot.v1;

internal sealed class CreateTimeSlotCommandHandler(IApplicationDbContext dbContext)
	: ICommandHandler<CreateTimeSlotCommand, Guid>
{
	public async ValueTask<Guid> Handle(
		CreateTimeSlotCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		var timeSlot = opportunity.AddTimeSlot(request.StartDateTime, request.EndDateTime, request.MaxParticipants);

		return timeSlot.Id.Value;
	}
}
