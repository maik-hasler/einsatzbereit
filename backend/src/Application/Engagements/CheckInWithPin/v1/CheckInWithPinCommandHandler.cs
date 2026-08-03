using System.Security.Cryptography;
using System.Text;
using Application.Common.Exceptions;
using Application.Common.Messaging;
using Application.Common.Persistence;
using Application.Common.RateLimiting;
using Domain.Engagements;
using Domain.Primitives;
using Domain.VolunteerOpportunities;

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

		if (engagement.IsAnonymized)
			throw new ResultFailureException(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer account has been deleted."));

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

		// CheckInPin is only ever populated for PINCode opportunities - every other
		// CheckInMethod leaves it null, which would otherwise let a null request.Pin
		// (an empty JSON body) pass a plain `!=` comparison as "null == null" (#1139).
		if (opportunity.CheckInMethod != CheckInMethod.PINCode)
			throw new ResultFailureException(Error.Conflict("Engagement.CheckInMethodNotPin", "This opportunity does not use PIN check-in."));

		if (string.IsNullOrWhiteSpace(request.Pin))
			throw new ResultFailureException(Error.Validation("Engagement.PinRequired", "PIN is required."));

		if (!PinsMatch(opportunity.CheckInPin, request.Pin))
		{
			await attemptLimiter.RegisterFailedAttemptAsync(request.EngagementId, cancellationToken);
			throw new ResultFailureException(Error.Validation("Engagement.InvalidPin", "Invalid PIN."));
		}

		await attemptLimiter.ResetAsync(request.EngagementId, cancellationToken);

		engagement.CheckIn().ThrowIfFailure();

		return engagement;
	}

	// CryptographicOperations.FixedTimeEquals instead of string.Equals/== (#1176)
	// so a wrong guess can't be timed digit-by-digit to narrow down the real PIN.
	// It requires equal-length spans, so a length mismatch is checked upfront -
	// that alone leaks nothing an attacker doesn't already know (PIN length is
	// public: EnsureValidPin only ever allows 4-6 digits).
	private static bool PinsMatch(string? storedPin, string suppliedPin)
	{
		if (storedPin is null)
			return false;

		var storedBytes = Encoding.UTF8.GetBytes(storedPin);
		var suppliedBytes = Encoding.UTF8.GetBytes(suppliedPin);

		return storedBytes.Length == suppliedBytes.Length
			&& CryptographicOperations.FixedTimeEquals(storedBytes, suppliedBytes);
	}
}
