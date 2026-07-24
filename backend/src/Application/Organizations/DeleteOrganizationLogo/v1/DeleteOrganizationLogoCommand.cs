using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.DeleteOrganizationLogo.v1;

public sealed record DeleteOrganizationLogoCommand(
	Guid OrganizationId,
	UserId RequestingUserId)
	: ICommand<bool>;
