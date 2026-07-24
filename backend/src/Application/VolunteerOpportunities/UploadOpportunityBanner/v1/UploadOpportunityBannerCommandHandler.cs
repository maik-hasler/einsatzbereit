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
		ImageUploadValidator.EnsureValid(request.Content.Length, request.ContentType, "Banner");

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(
			VolunteerOpportunityId.Create(request.OpportunityId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", $"Volunteer opportunity '{request.OpportunityId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			opportunity.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var ext = ImageUploadValidator.GetExtension(request.ContentType);
		var objectKey = $"opportunity-banners/{request.OpportunityId}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, request.ContentType, cancellationToken);

		opportunity.SetBannerImageUrl(url).ThrowIfFailure();

		return true;
	}
}
