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

		var ext = ImageUploadValidator.GetExtension(contentType);
		var objectKey = $"organization-logos/{request.OrganizationId}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, contentType, cancellationToken);

		organization.SetLogoUrl(url);

		return true;
	}
}
