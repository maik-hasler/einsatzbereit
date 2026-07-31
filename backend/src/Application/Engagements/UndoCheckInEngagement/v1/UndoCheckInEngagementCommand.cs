using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.UndoCheckInEngagement.v1;

public sealed record UndoCheckInEngagementCommand(
	EngagementId EngagementId,
	UserId RequestingUserId)
	: ICommand<Engagement>;
