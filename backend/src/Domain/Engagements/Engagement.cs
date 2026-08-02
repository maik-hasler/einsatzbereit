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
	private const int MaxReactivationCount = 5;

#pragma warning disable CS8618
	private Engagement() : base(default) { }
#pragma warning restore CS8618

	private Engagement(
		EngagementId id,
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		TimeSlotId? timeSlotId,
		string? message,
		EngagementStatus status)
		: base(id)
	{
		OpportunityId = opportunityId;
		VolunteerId = volunteerId;
		TimeSlotId = timeSlotId;
		Message = message;
		Status = status;
	}

	public static Engagement CreateSlotSignUp(
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		TimeSlotId timeSlotId)
	{
		var engagement = new Engagement(
			EngagementId.New(),
			opportunityId,
			volunteerId,
			timeSlotId,
			message: null,
			EngagementStatus.Pending);

		engagement.AddEvent(new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunityId));
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
			message,
			EngagementStatus.Pending);

		engagement.AddEvent(new EngagementCreatedDomainEvent(engagement.Id, volunteerId, opportunityId));
		return engagement;
	}

	public Result Confirm()
	{
		if (Status != EngagementStatus.Pending)
			return Result.Failure(Error.Conflict("Engagement.NotPending", "Only pending engagements can be confirmed."));

		Status = EngagementStatus.Confirmed;
		AddEvent(new EngagementConfirmedDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public Result Cancel(string? reason = null)
	{
		if (IsTerminated)
			return Result.Failure(Error.Conflict("Engagement.AlreadyTerminated", "Engagement is already terminated."));

		CancellationReason = reason;
		Status = EngagementStatus.Cancelled;
		AddEvent(new EngagementCancelledDomainEvent(Id, VolunteerId!.Value, OpportunityId, reason));
		return Result.Success();
	}

	public Result Withdraw()
	{
		if (IsTerminated)
			return Result.Failure(Error.Conflict("Engagement.AlreadyTerminated", "Engagement is already terminated."));

		if (IsCheckedIn)
			return Result.Failure(Error.Conflict("Engagement.CheckedIn", "A checked-in engagement can no longer be withdrawn."));

		Status = EngagementStatus.Withdrawn;
		AddEvent(new EngagementWithdrawnDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public Result Reactivate(TimeSlotId? timeSlotId, string? message)
	{
		if (!IsTerminated)
			return Result.Failure(Error.Conflict("Engagement.NotTerminated", "Only withdrawn or cancelled engagements can be reactivated."));

		if (ReactivationCount >= MaxReactivationCount)
			return Result.Failure(Error.Conflict(
				"Engagement.ReactivationLimitReached",
				"This engagement has been withdrawn and re-applied for too many times. Please contact the organizer directly."));

		if (timeSlotId is null && string.IsNullOrWhiteSpace(message))
			return Result.Failure(Error.Validation("Engagement.MessageRequired", "Message is required for individual contact."));

		TimeSlotId = timeSlotId;
		Message = message;
		CancellationReason = null;
		IsCheckedIn = false;
		FeedbackRating = null;
		FeedbackComment = null;
		FeedbackSubmittedAt = null;
		ReminderSentAt = null;
		Status = EngagementStatus.Pending;
		CreatedOn = DateTimeOffset.UtcNow;
		ReactivationCount++;
		AddEvent(new EngagementReactivatedDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public Result CheckIn()
	{
		if (Status != EngagementStatus.Confirmed)
			return Result.Failure(Error.Validation("Engagement.NotConfirmed", "Only confirmed engagements can be checked in."));

		IsCheckedIn = true;
		AddEvent(new EngagementCheckedInDomainEvent(Id, VolunteerId!.Value, OpportunityId));
		return Result.Success();
	}

	public Result UndoCheckIn()
	{
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

	public void Anonymize()
	{
		VolunteerId = null;
		Message = null;
		FeedbackComment = null;
		FeedbackRating = null;
		FeedbackSubmittedAt = null;
	}
}
