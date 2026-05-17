using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.RecordActivity.v1;

public sealed record RecordActivityCommand(UserId UserId, int IsoYear, int IsoWeek)
	: ICommand<bool>;
