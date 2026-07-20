using Application.Common.Messaging;

namespace Application.Users.SetUserAdminStatus.v1;

public sealed record SetUserAdminStatusCommand(
	Guid TargetUserId,
	Guid ActingUserId,
	bool IsAdmin)
	: ICommand<bool>;
