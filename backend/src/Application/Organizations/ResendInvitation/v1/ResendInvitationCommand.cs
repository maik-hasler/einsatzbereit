using Application.Common.Messaging;
using Domain.Organizations;
using Domain.Users;

namespace Application.Organizations.ResendInvitation.v1;

public sealed record ResendInvitationCommand(
	OrganizationId OrganizationId,
	OrganizationInvitationId InvitationId,
	UserId RequestingUserId) : ICommand<bool>;
