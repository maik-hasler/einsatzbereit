using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Domain.Primitives;
using Domain.Users;

namespace Application.Common.Authorization;

public static class OwnershipGuard
{
	public static async Task EnsureIsOrgMemberAsync(
		IKeycloakOrganizationService keycloak,
		Guid organizationId,
		UserId requestingUserId,
		CancellationToken cancellationToken)
	{
		var orgs = await keycloak.GetUserOrganizationsAsync(requestingUserId.Value, cancellationToken);
		if (!orgs.Any(o => o.Id == organizationId))
			throw new ResultFailureException(Error.Forbidden(
				"Organization.NotAMember",
				"You do not have permission to modify this resource."));
	}
}
