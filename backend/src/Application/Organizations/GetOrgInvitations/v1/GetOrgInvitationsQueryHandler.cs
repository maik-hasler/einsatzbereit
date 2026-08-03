using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Organizations.GetOrgInvitations.v1;

internal sealed class GetOrgInvitationsQueryHandler(
	IApplicationDbContext dbContext,
	IKeycloakUserService keycloakUserService)
	: IQueryHandler<GetOrgInvitationsQuery, List<OrgInvitationDto>>
{
	public async ValueTask<List<OrgInvitationDto>> Handle(
		GetOrgInvitationsQuery request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var invitations = await dbContext.GetInvitationsForOrganizationAsync(
			request.OrganizationId, cancellationToken);

		var inviteeIds = invitations.Select(i => i.InviteeId.Value).Distinct().ToList();
		var displayNames = await keycloakUserService.GetDisplayNamesAsync(inviteeIds, cancellationToken);

		return invitations
			.Select(i => new OrgInvitationDto(
				i.Id.Value,
				i.InviteeId.Value,
				displayNames.GetValueOrDefault(i.InviteeId.Value, "(unknown user)"),
				i.IntendedRole.ToString(),
				i.Status.ToString(),
				i.CreatedOn))
			.ToList();
	}
}
