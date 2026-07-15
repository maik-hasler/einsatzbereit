using Application.Common.Authorization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

namespace Application.VolunteerOpportunities.UploadOpportunityBanner.v1;

internal sealed class UploadOpportunityBannerCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<UploadOpportunityBannerCommand, bool>
{
	private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
	{
		["image/jpeg"] = ".jpg",
		["image/png"] = ".png",
		["image/webp"] = ".webp",
	};

	public async ValueTask<bool> Handle(
		UploadOpportunityBannerCommand request,
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

		var ext = Extensions.GetValueOrDefault(request.ContentType, ".jpg");
		var objectKey = $"opportunity-banners/{request.OpportunityId}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, request.ContentType, cancellationToken);

		opportunity.SetBannerImageUrl(url);

		return true;
	}
}
