using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Microsoft.Extensions.Logging;

namespace Application.Organizations.GetOrganizationDetails.v1;

internal sealed class GetOrganizationDetailsQueryHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService,
	ILogger<GetOrganizationDetailsQueryHandler> logger)
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

		await OwnershipGuard.EnsureIsMemberAsync(
			dbContext,
			organization.Id.Value,
			request.RequestingUserId,
			cancellationToken);

		// Purely local - answered from organization_membership, so the requesting
		// user's own role never depends on the Keycloak call below (#1709).
		var isRequestingUserOrganizer = await dbContext.IsOrganizerAsync(
			organization.Id, request.RequestingUserId, cancellationToken);

		var (memberDtos, membersUnavailable) = await GetMemberRosterAsync(organization.Id, cancellationToken);

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
			organization.CreatedOn,
			memberDtos,
			(isRequestingUserOrganizer ? OrganizationMemberRole.Organizer : OrganizationMemberRole.Member).ToString(),
			membersUnavailable);
	}

	// The org app shell (navigation, dashboard, every tab besides Members) doesn't
	// need Keycloak's member roster to render - only the Members tab does. A
	// transient Keycloak failure here used to throw and 500 the whole shell along
	// with it (#1709); fall back to what organization_membership already knows
	// locally (id + role, no Keycloak-sourced username/email/name) instead.
	private async Task<(List<OrganizationMemberDto> Members, bool Unavailable)> GetMemberRosterAsync(
		OrganizationId organizationId,
		CancellationToken cancellationToken)
	{
		try
		{
			var members = await keycloakOrganizationService.GetMembersAsync(
				organizationId.Value, cancellationToken);

			return (
				members
					.Select(m => new OrganizationMemberDto(
						m.UserId,
						m.Username,
						m.FirstName,
						m.LastName,
						m.Email,
						m.IsOrganisator,
						(m.IsOrganisator ? OrganizationMemberRole.Organizer : OrganizationMemberRole.Member).ToString()))
					.ToList(),
				false);
		}
		catch (HttpRequestException ex)
		{
			logger.LogWarning(
				ex,
				"Keycloak member lookup failed for organization {OrganizationId}; falling back to a local-only roster.",
				organizationId.Value);

			var roles = await dbContext.GetMembershipRolesAsync(organizationId, cancellationToken);

			return (
				roles
					.Select(kvp => new OrganizationMemberDto(
						kvp.Key,
						string.Empty,
						null,
						null,
						string.Empty,
						kvp.Value == OrganizationMemberRole.Organizer,
						kvp.Value.ToString()))
					.ToList(),
				true);
		}
	}
}
