using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.AdminShadowDeleteOrganization.v1;

public sealed record AdminShadowDeleteOrganizationCommand(
	Guid OrganizationId,
	UserId AdminUserId)
	: ICommand<bool>;
