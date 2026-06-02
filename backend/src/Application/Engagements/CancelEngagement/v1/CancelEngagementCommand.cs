using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.CancelEngagement.v1;

public sealed record CancelEngagementCommand(EngagementId EngagementId, UserId RequestingUserId, string? Reason = null)
	: ICommand<Engagement>;
