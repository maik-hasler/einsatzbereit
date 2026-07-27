using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.AdminDeleteOrganization.v1;

public sealed record AdminDeleteOrganizationCommand(
	Guid OrganizationId,
	UserId AdminUserId)
	: ICommand<bool>;
