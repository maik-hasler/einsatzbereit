using Application.Common.Authorization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.SetOpportunityColor.v1;

internal sealed class SetOpportunityColorCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<SetOpportunityColorCommand, bool>
{
	public async ValueTask<bool> Handle(
		SetOpportunityColorCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunityId = new VolunteerOpportunityId(request.OpportunityId);

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			opportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		opportunity.SetColor(request.Color);

		return true;
	}
}
