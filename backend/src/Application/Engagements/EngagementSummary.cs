namespace Application.Engagements;

public sealed record EngagementSummary(
	Guid Id,
	Guid OpportunityId,
	string OpportunityTitle,
	Guid VolunteerId,
	Guid? TimeSlotId,
	string? Message,
	string Status,
	bool IsCheckedIn,
	DateTimeOffset CreatedOn);
