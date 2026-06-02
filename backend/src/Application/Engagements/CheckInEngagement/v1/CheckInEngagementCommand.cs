using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.CheckInEngagement.v1;

public sealed record CheckInEngagementCommand(
	EngagementId EngagementId,
	UserId RequestingUserId)
	: ICommand<Engagement>;
