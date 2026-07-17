using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.DismissInvitation.v1;

internal sealed class DismissInvitationCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService)
	: ICommandHandler<DismissInvitationCommand, bool>
{
	public async ValueTask<bool> Handle(
		DismissInvitationCommand request,
		CancellationToken cancellationToken = default)
	{
		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrganizationService,
			request.OrganizationId.Value,
			request.RequestingUserId,
			cancellationToken);

		var invitation = await dbContext.OrganizationInvitations.FindAsync(request.InvitationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("OrganizationInvitation.NotFound", "Invitation not found."));

		if (invitation.OrganizationId != request.OrganizationId)
			throw new ResultFailureException(Error.Validation("OrganizationInvitation.WrongOrganization", "Invitation does not belong to this organization."));

		if (invitation.Status != InvitationStatus.Declined)
			throw new ResultFailureException(Error.Conflict("OrganizationInvitation.NotDeclined", "Only declined invitations can be dismissed."));

		dbContext.OrganizationInvitations.Delete(invitation);
		await unitOfWork.SaveChangesAsync(cancellationToken);

		return true;
	}
}
