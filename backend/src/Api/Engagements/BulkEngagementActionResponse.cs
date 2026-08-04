namespace Api.Engagements;

public sealed record BulkEngagementActionResponse(
	IReadOnlyList<EngagementStatusResponse> Succeeded,
	IReadOnlyList<BulkEngagementActionFailureResponse> Failed);

public sealed record BulkEngagementActionFailureResponse(
	Guid EngagementId,
	string ErrorCode,
	string Message);
