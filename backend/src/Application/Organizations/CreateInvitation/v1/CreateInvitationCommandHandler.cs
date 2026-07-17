using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;
using Domain.Users;

namespace Application.Organizations.CreateInvitation.v1;

internal sealed class CreateInvitationCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService)
	: ICommandHandler<CreateInvitationCommand, OrganizationInvitationId>
{
	public async ValueTask<OrganizationInvitationId> Handle(
		CreateInvitationCommand request,
		CancellationToken cancellationToken = default)
	{
		var org = await dbContext.Organizations.FindAsync(request.OrganizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", "Organization not found."));

		await OwnershipGuard.EnsureIsOrgMemberAsync(
			keycloakOrganizationService,
			request.OrganizationId.Value,
			request.InvitedById,
			cancellationToken);

		var inviteeProfile = await keycloakUserService.GetUserAsync(request.InviteeId.Value, cancellationToken);

		var members = await keycloakOrganizationService.GetMembersAsync(request.OrganizationId.Value, cancellationToken);
		if (members.Any(m => m.UserId == request.InviteeId.Value))
			throw new ResultFailureException(Error.Conflict("OrganizationInvitation.AlreadyMember", "User is already a member of this organization."));

		var alreadyInvited = await dbContext.HasPendingInvitationAsync(
			request.OrganizationId, request.InviteeId, cancellationToken);
		if (alreadyInvited)
			throw new ResultFailureException(Error.Conflict("OrganizationInvitation.AlreadyInvited", "A pending invitation already exists for this user."));

		var inviteeName = inviteeProfile.FirstName is not null && inviteeProfile.LastName is not null
			? $"{inviteeProfile.FirstName} {inviteeProfile.LastName}"
			: inviteeProfile.Username;

		var invitation = OrganizationInvitation.Create(
			request.OrganizationId,
			org.Name,
			request.InviteeId,
			inviteeName,
			request.InvitedById);

		await dbContext.OrganizationInvitations.AddAsync(invitation, cancellationToken);

		var notification = Notification.Create(
			request.InviteeId,
			NotificationKind.InvitationReceived,
			invitation.Id.Value);
		await dbContext.Notifications.AddAsync(notification, cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		return invitation.Id;
	}
}
