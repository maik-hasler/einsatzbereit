using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.ConfirmEngagement.v1;

public sealed record ConfirmEngagementCommand(EngagementId EngagementId, UserId RequestingUserId, string? Timezone = null)
	: ICommand<Engagement>;
