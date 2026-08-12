namespace Application.Engagements;

public sealed record EngagementSummary(
	Guid Id,
	Guid OpportunityId,
	string? OpportunityTitle,
	Guid? OrganizationId,
	string? OrganizationName,
	Guid? VolunteerId,
	Guid? TimeSlotId,
	string? Message,
	string Status,
	bool IsCheckedIn,
	bool HasFeedback,
	DateTimeOffset CreatedOn,
	string? VolunteerName = null,
	DateTimeOffset? TimeSlotStartDateTime = null,
	DateTimeOffset? TimeSlotEndDateTime = null,
	string? Location = null,
	string? VolunteerEmail = null,
	string? VolunteerPhone = null,
	string? CancellationReason = null,
	int? FeedbackRating = null,
	string? FeedbackComment = null,
	DateTimeOffset? FeedbackSubmittedAt = null,
	string CheckInMethod = "None",
	// The opportunity's application deadline, so /my-signups can state one for a
	// sign-up that has no time slot: an interest-based engagement has no date of
	// its own, and the deadline is the only date that applies to it (#1777).
	DateTimeOffset? OpportunityValidUntil = null);
