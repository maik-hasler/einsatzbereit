using Application.Common.Messaging;
using Domain.Engagements;
using Domain.Users;

namespace Application.Engagements.SubmitFeedback.v1;

public sealed record SubmitFeedbackCommand(
	EngagementId EngagementId,
	UserId RequestingUserId,
	int Rating,
	string? Comment)
	: ICommand<bool>;
