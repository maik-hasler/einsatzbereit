using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.CheckInWithPin.v1;

public sealed record CheckInWithPinCommand(
	EngagementId EngagementId,
	string Pin,
	UserId RequestingUserId)
	: ICommand<Engagement>;
