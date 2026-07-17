using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Common;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;

namespace Application.Organizations.CreateOrganization.v1;

internal sealed class CreateOrganizationCommandHandler(
	IKeycloakOrganizationService keycloakOrganizationService,
	IApplicationDbContext dbContext)
	: ICommandHandler<CreateOrganizationCommand, Organization>
{
	public async ValueTask<Organization> Handle(
		CreateOrganizationCommand request,
		CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
			throw new ResultFailureException(Error.Validation("Organization.NameRequired", "Name must not be empty."));

		if (request.Name.Length > 100)
			throw new ResultFailureException(Error.Validation("Organization.NameTooLong", "Organization name must not exceed 100 characters."));

		var keycloakId = await keycloakOrganizationService.CreateOrganizationAsync(
			request.Name, cancellationToken);

		await keycloakOrganizationService.AddMemberAsync(
			keycloakId, request.UserId, cancellationToken);

		await keycloakOrganizationService.AssignOrganizerRoleAsync(
			request.UserId, cancellationToken);

		var slug = await GenerateUniqueSlugAsync(request.Name, cancellationToken);

		var organization = Organization.Create(OrganizationId.Create(keycloakId).GetValueOrThrow(), request.Name, slug)
			.GetValueOrThrow();

		var address = request.Address is null
			? null
			: Address.Create(
				request.Address.Street,
				request.Address.HouseNumber,
				request.Address.ZipCode,
				request.Address.City).GetValueOrThrow();

		organization.ChangeDescription(request.Description);
		organization.ChangeContactInfo(request.ContactEmail, request.ContactPhone, request.Website);
		organization.Relocate(address);

		await dbContext.Organizations.AddAsync(organization, cancellationToken);

		var membership = OrganizationMembership.Create(
			organization.Id, UserId.Create(request.UserId).GetValueOrThrow(), OrganizationMemberRole.Organizer);

		await dbContext.OrganizationMemberships.AddAsync(membership, cancellationToken);

		return organization;
	}

	private async Task<string?> GenerateUniqueSlugAsync(
		string name,
		CancellationToken cancellationToken)
	{
		var baseSlug = SlugGenerator.Generate(name);

		if (string.IsNullOrEmpty(baseSlug))
			return null;

		var candidate = baseSlug;
		var suffix = 2;

		while (await dbContext.OrganizationSlugExistsAsync(candidate, cancellationToken))
		{
			candidate = $"{baseSlug}-{suffix}";
			suffix++;
		}

		return candidate;
	}
}
