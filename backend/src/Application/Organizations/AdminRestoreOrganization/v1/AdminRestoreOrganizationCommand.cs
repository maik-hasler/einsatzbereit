using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.AdminRestoreOrganization.v1;

public sealed record AdminRestoreOrganizationCommand(
	Guid OrganizationId,
	UserId AdminUserId)
	: ICommand<bool>;
