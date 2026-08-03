using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Domain.Primitives;

namespace Application.Engagements.DeleteFeedback.v1;

internal sealed class DeleteFeedbackCommandHandler(
	IApplicationDbContext dbContext)
	: ICommandHandler<DeleteFeedbackCommand, bool>
{
	public async ValueTask<bool> Handle(
		DeleteFeedbackCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		if (engagement.IsAnonymized)
			throw new ResultFailureException(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer account has been deleted."));

		if (engagement.VolunteerId!.Value.Value != request.RequestingUserId.Value)
			throw new ResultFailureException(Error.Forbidden("Engagement.NotOwner", "You can only delete feedback for your own engagements."));

		engagement.DeleteFeedback(DateTimeOffset.UtcNow).ThrowIfFailure();

		return true;
	}
}
