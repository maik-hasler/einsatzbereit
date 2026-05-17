using Application.Common.Messaging;
using Domain.Engagements;

namespace Application.Engagements.CheckInEngagement.v1;

public sealed record CheckInEngagementCommand(
	EngagementId EngagementId)
	: ICommand<Engagement>;
