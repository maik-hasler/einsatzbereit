using Application.Common.Messaging;
using Domain.Organizations;
using Domain.Users;

namespace Application.Invitations.DeclineInvitation.v1;

public sealed record DeclineInvitationCommand(
	OrganizationInvitationId InvitationId,
	UserId UserId) : ICommand<bool>;
