using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.Storage;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.UploadOrganizationLogo.v1;

internal sealed class UploadOrganizationLogoCommandHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrgService,
	IFileStorageService fileStorage)
	: ICommandHandler<UploadOrganizationLogoCommand, bool>
{
	private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
	{
		["image/jpeg"] = ".jpg",
		["image/png"] = ".png",
		["image/webp"] = ".webp",
	};

	public async ValueTask<bool> Handle(
		UploadOrganizationLogoCommand request,
		CancellationToken cancellationToken = default)
	{
		var organization = await dbContext.Organizations.FindAsync(
			new OrganizationId(request.OrganizationId), cancellationToken)
			?? throw new DomainException($"Organization '{request.OrganizationId}' not found.");

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrgService,
			request.OrganizationId,
			request.RequestingUserId,
			cancellationToken);

		var ext = Extensions.GetValueOrDefault(request.ContentType, ".jpg");
		var objectKey = $"organization-logos/{request.OrganizationId}{ext}";

		using var stream = new MemoryStream(request.Content);
		var url = await fileStorage.UploadAsync(objectKey, stream, request.Content.Length, request.ContentType, cancellationToken);

		organization.SetLogoUrl(url);

		return true;
	}
}
