using Domain.Primitives;
using Domain.Users;
using Domain.VolunteerOpportunities;

namespace Domain.Engagements;

public sealed class Engagement
	: AggregateRoot<EngagementId>,
		IAuditableEntity
{
	public VolunteerOpportunityId OpportunityId { get; private set; }

	public UserId VolunteerId { get; private set; }

	public TimeSlotId? TimeSlotId { get; private set; }

	public string? Message { get; private set; }

	public EngagementStatus Status { get; private set; }

	public string? CancellationReason { get; private set; }

	public bool IsCheckedIn { get; private set; }

	public DateTimeOffset? ReminderSentAt { get; private set; }

	public int? FeedbackRating { get; private set; }

	public string? FeedbackComment { get; private set; }

	public DateTimeOffset? FeedbackSubmittedAt { get; private set; }

	public DateTimeOffset CreatedOn { get; private set; }

	public DateTimeOffset? ModifiedOn { get; private set; }

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

	public static Engagement CreateWaitlistSignUp(
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		TimeSlotId timeSlotId)
	{
		return new Engagement(
			new EngagementId(Guid.CreateVersion7()),
			opportunityId,
			volunteerId,
			timeSlotId,
			message: null,
			EngagementStatus.Pending);
	}

	public static Engagement CreateIndividualContact(
		VolunteerOpportunityId opportunityId,
		UserId volunteerId,
		string message)
	{
		if (string.IsNullOrWhiteSpace(message))
			throw new DomainException("A message is required when expressing interest via individual contact.");

		return new Engagement(
			new EngagementId(Guid.CreateVersion7()),
			opportunityId,
			volunteerId,
			timeSlotId: null,
			message,
			EngagementStatus.Pending);
	}

	public void Confirm()
	{
		if (Status != EngagementStatus.Pending)
			throw new DomainException("Only pending engagements can be confirmed.");

		Status = EngagementStatus.Confirmed;
	}

	public void Cancel(string? reason = null)
	{
		if (Status is EngagementStatus.Withdrawn or EngagementStatus.Cancelled)
			throw new DomainException("Engagement is already terminated.");

		CancellationReason = reason;
		Status = EngagementStatus.Cancelled;
		AddEvent(new EngagementCancelledDomainEvent(Id, VolunteerId, OpportunityId, reason));
	}

	public void Withdraw()
	{
		if (Status is EngagementStatus.Cancelled or EngagementStatus.Withdrawn)
			throw new DomainException("Engagement is already terminated.");

		Status = EngagementStatus.Withdrawn;
	}

	public void Reactivate(TimeSlotId? timeSlotId, string? message)
	{
		if (Status is not (EngagementStatus.Withdrawn or EngagementStatus.Cancelled))
			throw new DomainException("Only withdrawn or cancelled engagements can be reactivated.");

		if (timeSlotId is null && string.IsNullOrWhiteSpace(message))
			throw new DomainException("Message is required for individual contact.");

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
	}

	public void CheckIn()
	{
		if (Status != EngagementStatus.Confirmed)
			throw new DomainException("Only confirmed engagements can be checked in.");

		IsCheckedIn = true;
	}

	public void MarkReminderSent(DateTimeOffset sentAt)
	{
		ReminderSentAt = sentAt;
	}

	public void SubmitFeedback(int rating, string? comment)
	{
		if (!IsCheckedIn)
			throw new DomainException("Feedback can only be submitted for checked-in engagements.");

		if (FeedbackSubmittedAt.HasValue)
			throw new DomainException("Feedback has already been submitted for this engagement.");

		if (rating is < 1 or > 5)
			throw new DomainException("Rating must be between 1 and 5.");

		if (comment is not null && comment.Length > 500)
			throw new DomainException("Comment must not exceed 500 characters.");

		FeedbackRating = rating;
		FeedbackComment = comment;
		FeedbackSubmittedAt = DateTimeOffset.UtcNow;
	}

	public void Anonymize()
	{
		VolunteerId = new UserId(Guid.Empty);
		Message = null;
		FeedbackComment = null;
		FeedbackRating = null;
		FeedbackSubmittedAt = null;
	}
}
