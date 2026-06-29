using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteOpportunityBanner.v1;

internal sealed class DeleteOpportunityBannerCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrgService,
	IFileStorageService fileStorage)
	: ICommandHandler<DeleteOpportunityBannerCommand, bool>
{
	private static readonly string[] BannerExtensions = [".jpg", ".png", ".webp"];

	public async ValueTask<bool> Handle(
		DeleteOpportunityBannerCommand request,
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

		foreach (var ext in BannerExtensions)
		{
			try
			{
				await fileStorage.DeleteAsync($"opportunity-banners/{request.OpportunityId}{ext}", cancellationToken);
			}
			catch
			{
				// Object may not exist for this extension; continue
			}
		}

		opportunity.ClearBannerImageUrl();

		return true;
	}
}
