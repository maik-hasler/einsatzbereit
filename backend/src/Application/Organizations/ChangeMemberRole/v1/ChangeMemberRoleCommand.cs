using Application.Common.Messaging;
using Domain.Organizations;
using Domain.Users;

namespace Application.Organizations.ChangeMemberRole.v1;

public sealed record ChangeMemberRoleCommand(
	OrganizationId OrganizationId,
	UserId TargetUserId,
	OrganizationMemberRole Role,
	UserId RequestingUserId) : ICommand<bool>;
