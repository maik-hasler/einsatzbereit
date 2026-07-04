using Application.Common.Messaging;
using Domain.Organizations;
using Domain.Users;

namespace Application.Organizations.DismissInvitation.v1;

public sealed record DismissInvitationCommand(
	OrganizationId OrganizationId,
	OrganizationInvitationId InvitationId,
	UserId RequestingUserId) : ICommand<bool>;
