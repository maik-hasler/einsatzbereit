using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.DeleteOrganizationLogo.v1;

internal sealed class DeleteOrganizationLogoCommandHandler(
	IApplicationDbContext dbContext,
	IFileStorageService fileStorage)
	: ICommandHandler<DeleteOrganizationLogoCommand, bool>
{
	private static readonly string[] LogoExtensions = [".jpg", ".png", ".webp"];

	public async ValueTask<bool> Handle(
		DeleteOrganizationLogoCommand request,
		CancellationToken cancellationToken = default)
	{
		var organization = await dbContext.Organizations.FindAsync(
			OrganizationId.Create(request.OrganizationId).GetValueOrThrow(), cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", $"Organization '{request.OrganizationId}' not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		foreach (var ext in LogoExtensions)
		{
			try
			{
				await fileStorage.DeleteAsync($"organization-logos/{request.OrganizationId}{ext}", cancellationToken);
			}
			catch
			{
				// Object may not exist for this extension; continue
			}
		}

		organization.SetLogoUrl(null);

		return true;
	}
}
