using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.DeleteOrganization.v1;

public sealed record DeleteOrganizationCommand(
	Guid OrganizationId,
	UserId RequestingUserId,
	bool IsAdmin = false)
	: ICommand<bool>;
