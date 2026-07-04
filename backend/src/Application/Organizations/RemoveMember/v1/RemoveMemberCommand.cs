using Application.Common.Messaging;
using Domain.Users;

namespace Application.Organizations.RemoveMember.v1;

public sealed record RemoveMemberCommand(
	Guid OrganizationId,
	Guid UserId,
	UserId RequestingUserId)
	: ICommand<bool>;
