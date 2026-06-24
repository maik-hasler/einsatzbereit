using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CheckInWithPin.v1;

internal sealed class CheckInWithPinCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<CheckInWithPinCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CheckInWithPinCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(engagement.OpportunityId, cancellationToken)
			?? throw new DomainException("Opportunity not found.");

		if (opportunity.CheckInPin != request.Pin)
			throw new DomainException("Invalid PIN.");

		if (engagement.VolunteerId.Value != request.RequestingUserId.Value)
			throw new DomainException("You can only check in your own engagement.");

		engagement.CheckIn();

		return engagement;
	}
}
