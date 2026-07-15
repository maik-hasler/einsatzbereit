using Application.Common.Authorization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.PublishVolunteerOpportunity.v1;

internal sealed class PublishVolunteerOpportunityCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<PublishVolunteerOpportunityCommand, bool>
{
	public async ValueTask<bool> Handle(
		PublishVolunteerOpportunityCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		opportunity.Publish();

		return true;
	}
}
