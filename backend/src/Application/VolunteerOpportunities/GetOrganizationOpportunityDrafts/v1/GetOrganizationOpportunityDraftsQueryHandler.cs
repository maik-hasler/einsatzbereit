using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.VolunteerOpportunities.GetVolunteerOpportunities.v1;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.GetOrganizationOpportunityDrafts.v1;

internal sealed class GetOrganizationOpportunityDraftsQueryHandler(
	IVolunteerOpportunityReadRepository readRepository,
	IKeycloakOrganizationService keycloakOrgService)
	: IQueryHandler<GetOrganizationOpportunityDraftsQuery, IReadOnlyList<VolunteerOpportunitySummary>>
{
	public async ValueTask<IReadOnlyList<VolunteerOpportunitySummary>> Handle(
		GetOrganizationOpportunityDraftsQuery request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrgService,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		return await readRepository.GetSummariesByOrganizationAsync(
			request.OrganizationId,
			OpportunityStatus.Draft,
			cancellationToken);
	}
}
