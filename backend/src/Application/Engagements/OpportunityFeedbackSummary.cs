using Application.Common.Pagination;

namespace Application.Engagements;

public sealed record OpportunityFeedbackSummary(
	double? AverageRating,
	int FeedbackCount,
	PagedList<FeedbackItemDto> Items);

public sealed record FeedbackItemDto(
	int Rating,
	string? Comment,
	DateTimeOffset SubmittedAt);
