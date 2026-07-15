using Application.Common.Authorization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.VolunteerOpportunities.GetOpportunityCheckInPin.v1;

internal sealed class GetOpportunityCheckInPinQueryHandler(
	IApplicationDbContext dbContext)
	: IQueryHandler<GetOpportunityCheckInPinQuery, string?>
{
	public async ValueTask<string?> Handle(
		GetOpportunityCheckInPinQuery request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId.Value}' not found.");

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		return opportunity.CheckInPin;
	}
}
