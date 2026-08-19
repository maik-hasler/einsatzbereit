using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

public sealed class Engagement
	: AggregateRoot<EngagementId>,
		IAuditableEntity
{
	public VolunteerOpportunityId OpportunityId { get; private set; }

	public UserId? VolunteerId { get; private set; }

	public TimeSlotId? TimeSlotId { get; private set; }

	// Snapshotted from the TimeSlot at sign-up/reactivation time (#1203): TimeSlotId gets
	// nulled by engagement.time_slot_id's ON DELETE SET NULL once the slot is hard-deleted,
	// so this is what keeps a volunteer's past-engagement history showing when a shift was.
	// Prefer a live join to TimeSlot when TimeSlotId is present (it may have been
	// rescheduled since) - fall back to these only once the slot is gone.
	public DateTimeOffset? TimeSlotStartDateTime { get; private set; }

	public DateTimeOffset? TimeSlotEndDateTime { get; private set; }

	public string? Message { get; private set; }

	public EngagementStatus Status { get; private set; }

	public string? CancellationReason { get; private set; }

	public int ReactivationCount { get; private set; }

	public bool IsCheckedIn { get; private set; }

	public DateTimeOffset? ReminderSentAt { get; private set; }

	public int? FeedbackRating { get; private set; }

	public string? FeedbackComment { get; private set; }

	public DateTimeOffset? FeedbackSubmittedAt { get; private set; }

	public bool IsAnonymized => VolunteerId is null;

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

	private bool IsTerminated => Status is EngagementStatus.Withdrawn or EngagementStatus.Cancelled;

	// Bounds how many times a single withdrawn/cancelled engagement can be
	// reused via Reactivate: without a cap, a volunteer could loop create/withdraw
	// indefinitely against the same opportunity, and every cycle mails the
	// volunteer plus every organizer of the org (einsatzbereit#1174).
	public const int MaxReactivationCount = 5;

	// Reasonable window (#1069) for a volunteer to reconsider a submitted rating
	// or comment. Measured from the original FeedbackSubmittedAt rather than
	// reset by each edit, so editing repeatedly can't keep the window open
	// indefinitely.
	public const int FeedbackEditWindowDays = 14;

#pragma warning disable CS8618
	private Engagement() : base(default) { }
#pragma warning restore CS8618

	private Engagement(
		EngagementId id,
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		TimeSlotId? timeSlotId,
		DateTimeOffset? timeSlotStartDateTime,
		DateTimeOffset? timeSlotEndDateTime,
		string? message,
		EngagementStatus status)
		: base(id)
	{
		OpportunityId = opportunityId;
		VolunteerId = volunteerId;
		TimeSlotId = timeSlotId;
		TimeSlotStartDateTime = timeSlotStartDateTime;
		TimeSlotEndDateTime = timeSlotEndDateTime;
		Message = message;
		Status = status;
	}

	public static Engagement CreateSlotSignUp(
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		TimeSlotId timeSlotId,
		DateTimeOffset? timeSlotStartDateTime = null,
		DateTimeOffset? timeSlotEndDateTime = null)
	{
		var engagement = new Engagement(
			EngagementId.New(),
			opportunityId,
			volunteerId,
			timeSlotId,
			timeSlotStartDateTime,
			timeSlotEndDateTime,
			message: null,
			EngagementStatus.Pending);
		engagement.AddEvent(new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunityId, IsSlotSignUp: true));
		return engagement;
	}

	public static Result<Engagement> CreateIndividualContact(
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		string message)
	{
		if (string.IsNullOrWhiteSpace(message))
			return Result.Failure<Engagement>(Error.Validation(
				"Engagement.MessageRequired",
				"A message is required when expressing interest via individual contact."));

		var engagement = new Engagement(
			EngagementId.New(),
			opportunityId,
			volunteerId,
			timeSlotId: null,
			timeSlotStartDateTime: null,
			timeSlotEndDateTime: null,
			message,
			EngagementStatus.Pending);
		engagement.AddEvent(new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunityId, IsSlotSignUp: false));
		return engagement;
	}

	public Result Confirm()
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (Status != EngagementStatus.Pending)
			return Result.Failure(Error.Conflict("Engagement.NotPending", "Only pending engagements can be confirmed."));

		Status = EngagementStatus.Confirmed;
		AddEvent(new EngagementConfirmedDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	// opportunityTitle is denormalized onto EngagementCancelledDomainEvent (#1150)
	// rather than looked up from OpportunityId when the event is later dispatched -
	// several callers cancel engagements as part of deleting the opportunity itself
	// in the same transaction, so by dispatch time there would be nothing left to
	// look up. Optional only so existing callers/tests that don't have a title
	// handy (or don't care about the eventual notification) keep compiling.
	public Result Cancel(string? reason = null, string? opportunityTitle = null)
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (IsTerminated)
			return Result.Failure(Error.Conflict("Engagement.AlreadyTerminated", "Engagement is already terminated."));

		CancellationReason = reason;
		Status = EngagementStatus.Cancelled;
		AddEvent(new EngagementCancelledDomainEvent(Id, VolunteerId!.Value, OpportunityId, reason, opportunityTitle));
		return Result.Success();
	}

	public Result Withdraw()
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (IsTerminated)
			return Result.Failure(Error.Conflict("Engagement.AlreadyTerminated", "Engagement is already terminated."));

		if (IsCheckedIn)
			return Result.Failure(Error.Conflict("Engagement.CheckedIn", "A checked-in engagement can no longer be withdrawn."));

		Status = EngagementStatus.Withdrawn;
		AddEvent(new EngagementWithdrawnDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public Result Reactivate(
		TimeSlotId? timeSlotId,
		string? message,
		DateTimeOffset? timeSlotStartDateTime = null,
		DateTimeOffset? timeSlotEndDateTime = null)
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (!IsTerminated)
			return Result.Failure(Error.Conflict("Engagement.NotTerminated", "Only withdrawn or cancelled engagements can be reactivated."));

		if (ReactivationCount >= MaxReactivationCount)
			return Result.Failure(Error.Conflict(
				"Engagement.ReactivationLimitReached",
				"This engagement has been withdrawn and re-applied for too many times. Please contact the organizer directly."));

		if (timeSlotId is null && string.IsNullOrWhiteSpace(message))
			return Result.Failure(Error.Validation("Engagement.MessageRequired", "Message is required for individual contact."));

		TimeSlotId = timeSlotId;
		TimeSlotStartDateTime = timeSlotId is null ? null : timeSlotStartDateTime;
		TimeSlotEndDateTime = timeSlotId is null ? null : timeSlotEndDateTime;
		Message = message;
		CancellationReason = null;
		IsCheckedIn = false;
		FeedbackRating = null;
		FeedbackComment = null;
		FeedbackSubmittedAt = null;
		ReminderSentAt = null;
		Status = EngagementStatus.Pending;
		ReactivationCount++;
		AddEvent(new EngagementReactivatedDomainEvent(Id, VolunteerId!.Value, OpportunityId, IsSlotSignUp: timeSlotId is not null));
		return Result.Success();
	}

	public Result CheckIn()
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (Status != EngagementStatus.Confirmed)
			return Result.Failure(Error.Validation("Engagement.NotConfirmed", "Only confirmed engagements can be checked in."));

		if (IsCheckedIn)
			return Result.Failure(Error.Conflict("Engagement.AlreadyCheckedIn", "Engagement is already checked in."));

		IsCheckedIn = true;
		AddEvent(new EngagementCheckedInDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public Result UndoCheckIn()
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (IsTerminated)
			return Result.Failure(Error.Conflict("Engagement.AlreadyTerminated", "Engagement is already terminated."));

		if (!IsCheckedIn)
			return Result.Failure(Error.Conflict("Engagement.CheckInNotActive", "Only checked-in engagements can have their check-in undone."));

		IsCheckedIn = false;
		AddEvent(new EngagementCheckInUndoneDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public Result SubmitFeedback(int rating, string? comment, DateTimeOffset now)
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (!IsCheckedIn)
			return Result.Failure(Error.Conflict("Engagement.NotCheckedIn", "Feedback can only be submitted for checked-in engagements."));

		if (FeedbackSubmittedAt.HasValue)
			return Result.Failure(Error.Conflict("Engagement.FeedbackAlreadySubmitted", "Feedback has already been submitted for this engagement."));

		if (rating is < 1 or > 5)
			return Result.Failure(Error.Validation("Engagement.RatingOutOfRange", "Rating must be between 1 and 5."));

		if (comment is not null && comment.Length > 500)
			return Result.Failure(Error.Validation("Engagement.CommentTooLong", "Comment must not exceed 500 characters."));

		FeedbackRating = rating;
		FeedbackComment = comment;
		FeedbackSubmittedAt = now;
		AddEvent(new EngagementFeedbackSubmittedDomainEvent(Id, VolunteerId!.Value, OpportunityId, rating));
		return Result.Success();
	}

	public Result UpdateFeedback(int rating, string? comment, DateTimeOffset now)
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (!FeedbackSubmittedAt.HasValue)
			return Result.Failure(Error.Conflict("Engagement.FeedbackNotSubmitted", "Feedback has not been submitted yet."));

		if (now > FeedbackSubmittedAt.Value.AddDays(FeedbackEditWindowDays))
			return Result.Failure(Error.Conflict("Engagement.FeedbackEditWindowExpired", $"Feedback can no longer be edited more than {FeedbackEditWindowDays} days after it was submitted."));

		if (rating is < 1 or > 5)
			return Result.Failure(Error.Validation("Engagement.RatingOutOfRange", "Rating must be between 1 and 5."));

		if (comment is not null && comment.Length > 500)
			return Result.Failure(Error.Validation("Engagement.CommentTooLong", "Comment must not exceed 500 characters."));

		FeedbackRating = rating;
		FeedbackComment = comment;
		AddEvent(new EngagementFeedbackUpdatedDomainEvent(Id, VolunteerId!.Value, OpportunityId, rating));
		return Result.Success();
	}

	public Result DeleteFeedback(DateTimeOffset now)
	{
		if (IsAnonymized)
			return Result.Failure(Error.Conflict("Engagement.Anonymized", "This engagement's volunteer has deleted their account and can no longer be acted on."));

		if (!FeedbackSubmittedAt.HasValue)
			return Result.Failure(Error.Conflict("Engagement.FeedbackNotSubmitted", "Feedback has not been submitted yet."));

		if (now > FeedbackSubmittedAt.Value.AddDays(FeedbackEditWindowDays))
			return Result.Failure(Error.Conflict("Engagement.FeedbackEditWindowExpired", $"Feedback can no longer be edited more than {FeedbackEditWindowDays} days after it was submitted."));

		FeedbackRating = null;
		FeedbackComment = null;
		FeedbackSubmittedAt = null;
		AddEvent(new EngagementFeedbackDeletedDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public void Anonymize()
	{
		VolunteerId = null;
		Message = null;
		FeedbackComment = null;
		FeedbackRating = null;
		FeedbackSubmittedAt = null;
	}
}
