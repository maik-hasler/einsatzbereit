using Application.Common.Messaging;
using Domain.Organizations;
using Domain.Users;

namespace Application.Invitations.AcceptInvitation.v1;

public sealed record AcceptInvitationCommand(
	OrganizationInvitationId InvitationId,
	UserId UserId) : ICommand<bool>;
