using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CheckInEngagement.v1;

internal sealed class CheckInEngagementCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrgService)
	: ICommandHandler<CheckInEngagementCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CheckInEngagementCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new DomainException($"Engagement '{request.EngagementId.Value}' not found.");

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(engagement.OpportunityId, cancellationToken);
		if (opportunity is not null)
		{
			await OwnershipGuard.EnsureIsOrgMemberAsync(
				keycloakOrgService,
				opportunity.OrganizationId.Value,
				request.RequestingUserId,
				cancellationToken);
		}

		engagement.CheckIn();

		return engagement;
	}
}
