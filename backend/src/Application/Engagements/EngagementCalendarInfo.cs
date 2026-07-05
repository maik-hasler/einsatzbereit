namespace Application.Engagements;

public sealed record EngagementCalendarInfo(
	Guid EngagementId,
	Guid OpportunityId,
	string OpportunityTitle,
	string OpportunityDescription,
	string? Location,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime);
