using Application.Common.Authorization;
using Application.Common.Exceptions;
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
	public async ValueTask<bool> Handle(
		UploadOpportunityBannerCommand request,
		CancellationToken cancellationToken = default)
	{
		var contentType = ImageUploadValidator.EnsureValid(request.Content, request.ContentType, "Banner");

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var previousBannerUrl = opportunity.BannerImageUrl;

		var ext = ImageUploadValidator.GetExtension(contentType);
		var objectKey = $"opportunity-banners/{request.OpportunityId}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, contentType, cancellationToken);

		opportunity.SetBannerImageUrl(url).ThrowIfFailure();

		await DeletePreviousBannerIfOrphanedAsync(previousBannerUrl, objectKey, cancellationToken);

		return true;
	}

	private async Task DeletePreviousBannerIfOrphanedAsync(string? previousBannerUrl, string newObjectKey, CancellationToken cancellationToken)
	{
		if (previousBannerUrl is null)
			return;

		var previousObjectKey = fileStorage.GetObjectKeyFromPublicUrl(previousBannerUrl);
		// Extension unchanged - the previous upload lives at the same object key,
		// so this new upload already overwrote it; deleting it now would delete
		// the file just uploaded.
		if (previousObjectKey is null || previousObjectKey == newObjectKey)
			return;

		try
		{
			await fileStorage.DeleteAsync(previousObjectKey, cancellationToken);
		}
		catch
		{
			// Object may already be gone or storage may be transiently unavailable; continue.
		}
	}
}
