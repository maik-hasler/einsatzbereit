using Application.Common.Authorization;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;

namespace Application.Organizations.GetOrgInvitations.v1;

internal sealed class GetOrgInvitationsQueryHandler(
	IApplicationDbContext dbContext,
	IKeycloakOrganizationService keycloakOrganizationService)
	: IQueryHandler<GetOrgInvitationsQuery, List<OrgInvitationDto>>
{
	public async ValueTask<List<OrgInvitationDto>> Handle(
		GetOrgInvitationsQuery request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrganizationService,
			request.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var invitations = await dbContext.GetInvitationsForOrganizationAsync(
			request.OrganizationId, cancellationToken);

		return invitations
			.Select(i => new OrgInvitationDto(
				i.Id.Value,
				i.InviteeId.Value,
				i.InviteeName,
				i.Status.ToString(),
				i.CreatedOn))
			.ToList();
	}
}
