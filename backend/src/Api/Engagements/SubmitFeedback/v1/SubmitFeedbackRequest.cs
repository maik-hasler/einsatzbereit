namespace Api.Engagements.SubmitFeedback.v1;

public sealed record SubmitFeedbackRequest(
	int Rating,
	string? Comment);
