using Application.Common.Messaging;
using Domain.Organizations;
using Domain.Users;

namespace Application.Organizations.CreateInvitation.v1;

public sealed record CreateInvitationCommand(
	OrganizationId OrganizationId,
	UserId InviteeId,
	UserId InvitedById) : ICommand<OrganizationInvitationId>;
