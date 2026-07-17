using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Engagements.SubmitFeedback.v1;

internal sealed class SubmitFeedbackCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<SubmitFeedbackCommand, bool>
{
	public async ValueTask<bool> Handle(
		SubmitFeedbackCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		if (engagement.VolunteerId!.Value.Value != request.RequestingUserId.Value)
			throw new ResultFailureException(Error.Forbidden("Engagement.NotOwner", "You can only submit feedback for your own engagements."));

		engagement.SubmitFeedback(request.Rating, request.Comment, DateTimeOffset.UtcNow).ThrowIfFailure();

		return true;
	}
}
