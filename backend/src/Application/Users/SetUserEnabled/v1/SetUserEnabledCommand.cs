using Application.Common.Messaging;

namespace Application.Users.SetUserEnabled.v1;

public sealed record SetUserEnabledCommand(
	Guid TargetUserId,
	Guid ActingUserId,
	bool Enabled)
	: ICommand<bool>;
