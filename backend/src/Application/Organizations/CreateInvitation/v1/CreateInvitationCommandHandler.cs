using Application.Common.Authorization;
using Application.Common.Email;
using Application.Common.Exceptions;
using Application.Common.Keycloak;
using Application.Common.Localization;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Notifications;
using Domain.Organizations;
using Domain.Primitives;

namespace Application.Organizations.CreateInvitation.v1;

internal sealed class CreateInvitationCommandHandler(
	IApplicationDbContext dbContext,
	IUnitOfWork unitOfWork,
	IKeycloakOrganizationService keycloakOrganizationService,
	IKeycloakUserService keycloakUserService,
	IEmailService emailService,
	IEmailTemplateRenderer emailTemplateRenderer)
	: ICommandHandler<CreateInvitationCommand, OrganizationInvitationId>
{
	public async ValueTask<OrganizationInvitationId> Handle(
		CreateInvitationCommand request,
		CancellationToken cancellationToken = default)
	{
		var org = await dbContext.Organizations.FindAsync(request.OrganizationId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Organization.NotFound", "Organization not found."));

		await OwnershipGuard.EnsureIsOrganizerAsync(
			dbContext,
			request.OrganizationId.Value,
			request.InvitedById,
			cancellationToken);

		var inviteeProfile = await keycloakUserService.GetUserAsync(request.InviteeId.Value, cancellationToken);

		var members = await keycloakOrganizationService.GetMembersAsync(request.OrganizationId.Value, cancellationToken);
		if (members.Any(m => m.UserId == request.InviteeId.Value))
			throw new ResultFailureException(Error.Conflict("OrganizationInvitation.AlreadyMember", "User is already a member of this organization."));

		var now = DateTimeOffset.UtcNow;
		var invitation = OrganizationInvitation.Create(
			request.OrganizationId,
			request.InviteeId,
			request.InvitedById,
			request.Role,
			now);

		var created = await dbContext.TryCreateInvitationAsync(invitation, cancellationToken);
		if (!created)
			throw new ResultFailureException(Error.Conflict("OrganizationInvitation.AlreadyInvited", "A pending invitation already exists for this user."));

		var notification = Notification.Create(
			request.InviteeId,
			NotificationKind.InvitationReceived,
			invitation.Id.Value);
		await dbContext.Notifications.AddAsync(notification, cancellationToken);

		await unitOfWork.SaveChangesAsync(cancellationToken);

		var inviteeUser = (await dbContext.GetOrCreateUsersAsync([request.InviteeId], cancellationToken))[0];
		var inviteeLanguage = SupportedLanguages.Resolve(inviteeUser.PreferredLanguage);

		var content = emailTemplateRenderer.Render(
			EmailTemplateKind.InvitationReceived,
			inviteeLanguage,
			new Dictionary<string, string>
			{
				["InviteeName"] = inviteeProfile.FirstName ?? inviteeProfile.Username,
				["OrganizationName"] = org.Name,
			});

		await emailService.SendAsync(
			inviteeProfile.Email,
			content.Subject,
			content.Body,
			invitation.Id.Value.ToString(),
			cancellationToken);

		return invitation.Id;
	}
}
