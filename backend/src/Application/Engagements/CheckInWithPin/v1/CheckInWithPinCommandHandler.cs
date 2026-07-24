using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.RateLimiting;
using Domain.Engagements;
using Domain.Primitives;

namespace Application.Engagements.CheckInWithPin.v1;

internal sealed class CheckInWithPinCommandHandler(
	IApplicationDbContext dbContext,
	ICheckInAttemptLimiter attemptLimiter)
	: ICommandHandler<CheckInWithPinCommand, Engagement>
{
	public async ValueTask<Engagement> Handle(
		CheckInWithPinCommand request,
		CancellationToken cancellationToken = default)
	{
		var engagement = await dbContext.Engagements.FindAsync(request.EngagementId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("Engagement.NotFound", $"Engagement '{request.EngagementId.Value}' not found."));

		// Ownership must be checked before the PIN is ever compared - otherwise the
		// "invalid PIN" vs "not owner" responses let any authenticated user tell
		// whether a guessed PIN was correct without owning the engagement (#806).
		if (engagement.VolunteerId!.Value.Value != request.RequestingUserId.Value)
			throw new ResultFailureException(Error.Validation("Engagement.NotOwner", "You can only check in your own engagement."));

		if (await attemptLimiter.IsLockedOutAsync(request.EngagementId, cancellationToken))
			throw new ResultFailureException(Error.Forbidden(
				"Engagement.CheckInLocked",
				"Too many failed PIN attempts. Try again later."));

		var opportunity = await dbContext.VolunteerOpportunities.FindAsync(engagement.OpportunityId, cancellationToken)
			?? throw new ResultFailureException(Error.NotFound("VolunteerOpportunity.NotFound", "Opportunity not found."));

		if (opportunity.CheckInPin != request.Pin)
		{
			await attemptLimiter.RegisterFailedAttemptAsync(request.EngagementId, cancellationToken);
			throw new ResultFailureException(Error.Validation("Engagement.InvalidPin", "Invalid PIN."));
		}

		await attemptLimiter.ResetAsync(request.EngagementId, cancellationToken);

		engagement.CheckIn().ThrowIfFailure();

		return engagement;
	}
}
