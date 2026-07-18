using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.DeleteOpportunityBanner.v1;

internal sealed class DeleteOpportunityBannerCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<DeleteOpportunityBannerCommand, bool>
{
	private static readonly string[] BannerExtensions = [".jpg", ".png", ".webp"];

	public async ValueTask<bool> Handle(
		DeleteOpportunityBannerCommand request,
		CancellationToken cancellationToken = default)
	{
		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
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
