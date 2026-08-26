using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.UploadOrganizationLogo.v1;

internal sealed class UploadOrganizationLogoCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<UploadOrganizationLogoCommand, bool>
{
	public async ValueTask<bool> Handle(
		UploadOrganizationLogoCommand request,
		CancellationToken cancellationToken = default)
	{
		var contentType = ImageUploadValidator.EnsureValid(request.Content, request.ContentType, "Logo");

		var organization = await dbContext.Organizations.FindAsync(
			OrganizationId.Create(request.OrganizationId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		var previousLogoUrl = organization.LogoUrl;

		var ext = ImageUploadValidator.GetExtension(contentType);
		var objectKey = $"organization-logos/{request.OrganizationId}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, contentType, cancellationToken);

		organization.SetLogoUrl(url);

		await DeletePreviousLogoIfOrphanedAsync(previousLogoUrl, objectKey, cancellationToken);

		return true;
	}

	private async Task DeletePreviousLogoIfOrphanedAsync(string? previousLogoUrl, string newObjectKey, CancellationToken cancellationToken)
	{
		if (previousLogoUrl is null)
			return;

		var previousObjectKey = fileStorage.GetObjectKeyFromPublicUrl(previousLogoUrl);
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
