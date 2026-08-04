using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.UpdateFeedback.v1;

public sealed record UpdateFeedbackCommand(
	EngagementId EngagementId,
	UserId RequestingUserId,
	int Rating,
	string? Comment)
	: ICommand<bool>;
