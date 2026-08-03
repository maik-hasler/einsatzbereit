using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.DeleteFeedback.v1;

public sealed record DeleteFeedbackCommand(
	EngagementId EngagementId,
	UserId RequestingUserId)
	: ICommand<bool>;
