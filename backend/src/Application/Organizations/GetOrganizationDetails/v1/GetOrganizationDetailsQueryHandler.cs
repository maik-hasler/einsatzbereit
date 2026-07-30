using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;

namespace Application.Organizations.GetOrganizationDetails.v1;

internal sealed class GetOrganizationDetailsQueryHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService)
	: IQueryHandler<GetOrganizationDetailsQuery, OrganizationDetailsResponse?>
{
	public async ValueTask<OrganizationDetailsResponse?> Handle(
		GetOrganizationDetailsQuery request,
		CancellationToken cancellationToken = default)
	{
		var organization = await dbContext.Organizations.FindAsync(
			OrganizationId.Create(request.OrganizationId).GetValueOrThrow(), cancellationToken);

		if (organization is null)
			return null;

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			organization.Id.Value,
			request.RequestingUserId,
			cancellationToken);

		var members = await keycloakOrganizationService.GetMembersAsync(
			organization.Id.Value, cancellationToken);

		var address = organization.Address is null
			? null
			: new AddressDto(
				organization.Address.Street,
				organization.Address.HouseNumber,
				organization.Address.ZipCode,
				organization.Address.City);

		return new OrganizationDetailsResponse(
			organization.Id.Value,
			organization.Name,
			organization.Description,
			organization.ContactEmail,
			organization.ContactPhone,
			organization.Website,
			organization.LogoUrl,
			address,
			organization.IsVerified,
			organization.CreatedOn,
			organization.ModifiedOn,
			members
				.Select(m => new OrganizationMemberDto(
					m.UserId,
					m.Username,
					m.FirstName,
					m.LastName,
					m.Email,
					m.IsOrganisator,
					(m.IsOrganisator ? OrganizationMemberRole.Organizer : OrganizationMemberRole.Member).ToString()))
				.ToList());
	}
}
