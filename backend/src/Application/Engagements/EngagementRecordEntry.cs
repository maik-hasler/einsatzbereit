namespace Application.Engagements;

public sealed record EngagementRecordEntry(
	Guid EngagementId,
	string? OpportunityTitle,
	string? OrganizationName,
	DateTimeOffset StartDateTime,
	DateTimeOffset EndDateTime,
	double Hours);
