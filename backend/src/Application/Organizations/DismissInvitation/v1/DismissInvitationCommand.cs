using Application.Common.Messaging;
using Domain.Organizations;

namespace Application.Organizations.DismissInvitation.v1;

public sealed record DismissInvitationCommand(
	OrganizationId OrganizationId,
	OrganizationInvitationId InvitationId) : ICommand<bool>;
