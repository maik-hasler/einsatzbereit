using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UploadOpportunityBanner.v1;

internal sealed class UploadOpportunityBannerCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrgService)
	: ICommandHandler<UploadOpportunityBannerCommand, bool>
{
	public async ValueTask<bool> Handle(
		UploadOpportunityBannerCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			new VolunteerOpportunityId(request.OpportunityId), cancellationToken)
			?? throw new DomainException($"Volunteer opportunity '{request.OpportunityId}' not found.");

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrgService,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		opportunity.SetBannerImage(request.Content, request.ContentType);

		return true;
	}
}
