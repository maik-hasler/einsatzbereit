using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Engagements.GetEngagements.v1;

internal sealed class GetEngagementsQueryHandler(
	IEngagementReadRepository readRepository,
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrgService)
	: IQueryHandler<GetEngagementsQuery, List<EngagementSummary>>
{
	public async ValueTask<List<EngagementSummary>> Handle(
		GetEngagementsQuery request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(request.OpportunityId, cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId.Value}' not found.");

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrgService,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		return await readRepository.GetByOpportunityAsync(request.OpportunityId, cancellationToken);
	}
}
