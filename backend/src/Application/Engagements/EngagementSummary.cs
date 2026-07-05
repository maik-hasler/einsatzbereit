namespace Application.Engagements;

public sealed record EngagementSummary(
	Guid Id,
	Guid OpportunityId,
	string OpportunityTitle,
	Guid OrganizationId,
	string OrganizationName,
	Guid VolunteerId,
	Guid? TimeSlotId,
	string? Message,
	string Status,
	bool IsCheckedIn,
	bool HasFeedback,
	DateTimeOffset CreatedOn,
	string? VolunteerName = null,
	DateTimeOffset? TimeSlotStartDateTime = null,
	DateTimeOffset? TimeSlotEndDateTime = null,
	string? Location = null);
