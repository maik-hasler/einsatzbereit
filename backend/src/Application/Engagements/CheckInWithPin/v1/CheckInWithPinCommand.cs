using Application.Common.Messaging;
using Domain.Engagements;

namespace Application.Engagements.CheckInWithPin.v1;

public sealed record CheckInWithPinCommand(
	EngagementId EngagementId,
	string Pin)
	: ICommand<Engagement>;
