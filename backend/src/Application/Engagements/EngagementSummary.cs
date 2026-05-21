namespace Application.Engagements;

public sealed record EngagementSummary(
	Guid Id,
	Guid OpportunityId,
	Guid VolunteerId,
	Guid? TimeSlotId,
	string? Message,
	string Status,
	bool IsCheckedIn,
	DateTimeOffset CreatedOn);
