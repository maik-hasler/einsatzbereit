using Application.Common.Messaging;
using Domain.Users;

namespace Application.Users.GetMyStreaks.v1;

public sealed record GetMyStreaksQuery(UserId UserId)
	: IQuery<StreakSummary>;
