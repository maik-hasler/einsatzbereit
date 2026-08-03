namespace Api.Engagements.UpdateFeedback.v1;

public sealed record UpdateFeedbackRequest(
	int Rating,
	string? Comment);
