namespace Application.Engagements;

public sealed record OpportunityFeedbackSummary(
	double? AverageRating,
	int FeedbackCount,
	IReadOnlyList<FeedbackItemDto> Items);

public sealed record FeedbackItemDto(
	int Rating,
	string? Comment,
	DateTimeOffset SubmittedAt);
